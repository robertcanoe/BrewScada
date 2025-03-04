using System;
using System.Text;
using System.Timers;
using System.Windows.Forms;
using MongoDB.Driver;
using System.Drawing;
using MongoDB.Bson;

namespace BrewScada
{
    public partial class Form1 : Form
    {
        private MongoDBConnection _dbConnection;
        private IMongoCollection<Produccion> _produccionCollection;
        private IMongoCollection<Counter> _counterCollection;
        private IMongoCollection<Ingrediente> _ingredientesCollection;
        private IMongoCollection<BsonDocument> _batchesBotellasCollection; // Para el "Almacén de productos terminados"
        private System.Timers.Timer _timer; // Usamos System.Timers.Timer para los 20 segundos por etapa
        private Random _random; // Mantengo Random por si lo necesitamos para algo más adelante, aunque ahora usaremos valores fijos
        private bool isRunning;
        private bool isPaused;
        private string batchDetails;
        private decimal cantidadMalta;
        private decimal cantidadAgua;
        private decimal cantidadLevadura;
        private decimal cantidadBotellas;
        private int stage;
        private DateTime batchStartTime; // Tiempo de inicio del lote
        private DateTime[] stageStartTimes; // Tiempos de inicio de cada etapa (simulados realistas)
        private string currentBatchName; // Nombre del lote actual para buscar en la base de datos
        private int batchCount; // Contador para los 10 batches
        private const int TOTAL_BATCHES = 10; // Número total de batches para 10,000 botellas

        // Valores iniciales del "Almacén de materias primas"
        private decimal almacenMalta = 2000m; // 2,000 kg
        private decimal almacenAgua = 12000m;  // 12,000 L
        private decimal almacenLevadura = 30m; // 30 kg
        private decimal almacenBotellas = 10000m; // 10,000 botellas vacías

        private InventoryManager _inventoryManager;
        private ProductionLog _productionLog;

        // Declaramos los labels como campos privados, mapeando a los nombres reales en Designer.cs
        private Label moliendaStartLabel; // Mapeado a label2
        private Label moliendaEndLabel;   // Mapeado a label11
        private Label coccionStartLabel;  // Mapeado a label12
        private Label coccionEndLabel;    // Mapeado a label19
        private Label fermentacionStartLabel; // Mapeado a label21
        private Label fermentacionEndLabel;   // Mapeado a label20
        private Label embotelladoStartLabel;  // Mapeado a label23
        private Label embotelladoEndLabel;    // Mapeado a label22

        public Form1()
        {
            InitializeComponent();
            var connectionString = GetConnectionStringFromConfig();
            var databaseName = "BrewScada";
            _dbConnection = new MongoDBConnection(connectionString, databaseName);
            _produccionCollection = _dbConnection.GetCollection<Produccion>("Produccion");
            _counterCollection = _dbConnection.GetCollection<Counter>("Counters");
            _ingredientesCollection = _dbConnection.GetCollection<Ingrediente>("Ingredientes");
            _batchesBotellasCollection = _dbConnection.GetCollection<BsonDocument>("BatchesBotellas"); // Nueva colección para botellas llenas

            _random = new Random(); // Mantengo por compatibilidad, aunque no lo usaremos
            InitializeTimer();
            isRunning = false;
            isPaused = false;
            batchDetails = string.Empty;
            stage = 0;
            stageStartTimes = new DateTime[4]; // 4 etapas: Molienda, Cocción, Fermentación, Embotellado
            batchCount = 0;

            _inventoryManager = new InventoryManager(_ingredientesCollection);
            _productionLog = new ProductionLog(_produccionCollection);

            // Mapeamos los labels a los nombres reales del Designer
            moliendaStartLabel = label2;
            moliendaEndLabel = label11;
            coccionStartLabel = label12;
            coccionEndLabel = label19;
            fermentacionStartLabel = label21;
            fermentacionEndLabel = label20;
            embotelladoStartLabel = label23;
            embotelladoEndLabel = label22;
        }

        private string GetConnectionStringFromConfig()
        {
            return "mongodb://localhost:27017";
        }

        private void InitializeTimer()
        {
            _timer = new System.Timers.Timer(20000); // 20 segundos por etapa (simulación rápida)
            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = true;
            _timer.Enabled = false;
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            if (!isRunning || isPaused) return;

            // Generamos un nuevo currentBatchName para cada batch al inicio
            if (stage == 0)
            {
                currentBatchName = "Batch_" + GetNextBatchNumber().ToString("D4");
                UpdateUI(() =>
                {
                    label24.Text = $"Batch: {currentBatchName}"; // Molienda
                    label25.Text = $"Batch: {currentBatchName}"; // Cocción
                    label26.Text = $"Batch: {currentBatchName}"; // Fermentación
                    label27.Text = $"Batch: {currentBatchName}"; // Embotellado
                    // Reiniciamos los labels de inicio y fin
                    moliendaStartLabel.Text = "--/--/---- --:--:--";
                    moliendaEndLabel.Text = "--/--/---- --:--:--";
                    coccionStartLabel.Text = "--/--/---- --:--:--";
                    coccionEndLabel.Text = "--/--/---- --:--:--";
                    fermentacionStartLabel.Text = "--/--/---- --:--:--";
                    fermentacionEndLabel.Text = "--/--/---- --:--:--";
                    embotelladoStartLabel.Text = "--/--/---- --:--:--";
                    embotelladoEndLabel.Text = "--/--/---- --:--:--";
                });
            }

            // Calculamos los tiempos simulados realistas para los labels
            switch (stage)
            {
                case 0: // Molienda
                    batchStartTime = DateTime.Now; // Establecemos el inicio del lote al comenzar Molienda
                    stageStartTimes[stage] = batchStartTime;
                    cantidadMalta = 200m; // Fijo: 200 kg de Malta (para 1,000 botellas por batch)
                    decimal molienda = ProcesarMolienda(cantidadMalta); // Procesamos 200 kg
                    decimal sobranteMalta = almacenMalta - cantidadMalta;
                    almacenMalta = sobranteMalta; // Actualizamos el inventario
                    batchDetails = FormatearDetallesProceso($"Molienda:{molienda:F3} kg de Malta", $"Sobrante Malta: {sobranteMalta:F3} kg");
                    DateTime moliendaEnd = batchStartTime.AddMinutes(15); // Fin de Molienda después de 15 minutos
                    UpdateUI(() =>
                    {
                        label7.Text = $"{molienda:F3} kg de Malta"; // Usamos label7 para Molienda
                        label7.Location = new Point(250, 339); // Mantemos posición original
                        label15.Text = $"Malta de cebada: {almacenMalta:F3} kg";
                        progressBar1.Value = 25; // Progreso inicial (25% para Molienda)
                        progressLabel.Text = $"Progreso: {progressBar1.Value}%";
                        moliendaStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                        moliendaEndLabel.Text = $"Fin: {moliendaEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                    });
                    stage++;
                    CheckAndNotifyInventory();
                    break;
                case 1: // Cocción
                    stageStartTimes[stage] = batchStartTime.AddMinutes(15); // Inicio de Cocción después de 15 min (fin de Molienda)
                    cantidadAgua = 1200m; // Fijo: 1,200 L de Agua (para 1,000 botellas por batch)
                    decimal coccion = ProcesarCoccion(cantidadAgua); // Procesamos 1,200 L
                    decimal sobranteAgua = almacenAgua - cantidadAgua;
                    almacenAgua = sobranteAgua; // Actualizamos el inventario
                    batchDetails = FormatearDetallesProceso(batchDetails, $"Cocción: {coccion:F3} L de Agua", $"Sobrante Agua: {sobranteAgua:F3} L");
                    DateTime coccionEnd = stageStartTimes[stage].AddMinutes(60); // Fin de Cocción después de 60 minutos
                    UpdateUI(() =>
                    {
                        label8.Text = $"{coccion:F3} L de Agua"; // Usamos label8 para Cocción
                        label8.Location = new Point(630, 339); // Mantemos posición original
                        label16.Text = $"Agua: {almacenAgua:F3} L";
                        progressBar1.Value = 50; // Progreso inicial (50% para Cocción)
                        progressLabel.Text = $"Progreso: {progressBar1.Value}%";
                        coccionStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                        coccionEndLabel.Text = $"Fin: {coccionEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                    });
                    stage++;
                    CheckAndNotifyInventory();
                    break;
                case 2: // Fermentación
                    stageStartTimes[stage] = batchStartTime.AddMinutes(75); // Inicio de Fermentación después de 75 min (fin de Cocción)
                    cantidadLevadura = 0.75m; // Fijo: 0.75 kg de Levadura (para 1,000 botellas por batch)
                    decimal fermentacion = ProcesarFermentacion(cantidadLevadura); // Procesamos 0.75 kg de Levadura
                    decimal sobranteLevadura = almacenLevadura - cantidadLevadura;
                    almacenLevadura = sobranteLevadura; // Actualizamos el inventario de Levadura
                    batchDetails = FormatearDetallesProceso(batchDetails, $"Fermentación: {fermentacion:F3} kg de Levadura",
                        $"Sobrante Levadura: {sobranteLevadura:F3} kg");
                    DateTime fermentacionEnd = stageStartTimes[stage].AddMinutes(240); // Fin de Fermentación después de 240 minutos
                    UpdateUI(() =>
                    {
                        label9.Text = $"{fermentacion:F3} kg de Levadura"; // Usamos label9 para Fermentación
                        label9.Location = new Point(1100, 339); // Mantemos posición original
                        label17.Text = $"Levadura: {almacenLevadura:F3} kg"; // Mostramos el inventario en kg
                        progressBar1.Value = 75; // Progreso inicial (75% para Fermentación)
                        progressLabel.Text = $"Progreso: {progressBar1.Value}%";
                        fermentacionStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                        fermentacionEndLabel.Text = $"Fin: {fermentacionEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                    });
                    stage++;
                    CheckAndNotifyInventory();
                    break;
                case 3: // Embotellado
                    stageStartTimes[stage] = batchStartTime.AddMinutes(315); // Inicio de Embotellado después de 315 min (fin de Fermentación)
                    cantidadBotellas = 1000m; // Fijo: 1,000 botellas por batch
                    decimal embotellado = ProcesarEmbotellado(cantidadBotellas); // Procesamos 1,000 botellas
                    decimal sobranteBotellas = almacenBotellas - cantidadBotellas;
                    almacenBotellas = sobranteBotellas; // Actualizamos el inventario de botellas vacías
                    batchDetails = FormatearDetallesProceso(batchDetails, $"Embotellado: {Math.Round(embotellado):F0} botellas", $"Sobrante Botellas: {sobranteBotellas:F0} botellas");
                    DateTime embotelladoEnd = stageStartTimes[stage].AddMinutes(30); // Fin de Embotellado después de 30 minutos
                    GuardarBotellasLlenas(currentBatchName, Math.Round(embotellado), embotelladoEnd); // Guardamos las botellas llenas
                    UpdateUI(() =>
                    {
                        label10.Text = $"{Math.Round(embotellado):F0} botellas"; // Usamos label10 para Embotellado
                        label10.Location = new Point(1100, 738); // Mantemos posición original
                        label18.Text = $"Botellas: {almacenBotellas:F0}"; // Mostramos las botellas vacías restantes
                        progressBar1.Value = 100; // 100% de progreso (completado)
                        progressLabel.Text = $"Progreso: {progressBar1.Value}%";
                        embotelladoStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                        embotelladoEndLabel.Text = $"Fin: {embotelladoEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                        // Actualizamos textBox1 con el batch actual sin duplicación
                        if (textBox1 != null)
                        {
                            textBox1.Text += $"{currentBatchName}: {Math.Round(embotellado):F0} botellas\r\n";
                        }
                    });
                    // Reiniciamos los tiempos para el próximo batch, pero no las cantidades procesadas
                    UpdateUI(() =>
                    {
                        moliendaStartLabel.Text = "--/--/---- --:--:--";
                        moliendaEndLabel.Text = "--/--/---- --:--:--";
                        coccionStartLabel.Text = "--/--/---- --:--:--";
                        coccionEndLabel.Text = "--/--/---- --:--:--";
                        fermentacionStartLabel.Text = "--/--/---- --:--:--";
                        fermentacionEndLabel.Text = "--/--/---- --:--:--";
                        embotelladoStartLabel.Text = "--/--/---- --:--:--";
                        embotelladoEndLabel.Text = "--/--/---- --:--:--";
                    });
                    // Incrementamos el contador de batches
                    batchCount++;
                    if (batchCount < TOTAL_BATCHES)
                    {
                        stage = 0; // Reiniciamos el stage para el próximo batch
                    }
                    else
                    {
                        // Completados los 10 batches
                        isRunning = false;
                        _timer.Enabled = false;
                        UpdateUI(() =>
                        {
                            button1.Text = "Empezar";
                            button2.Text = "Pausar";
                            progressLabel.Text = "Progreso: 100% (10,000 botellas completadas)";
                        });
                        batchCount = 0; // Reiniciamos el contador para un nuevo ciclo
                    }
                    CheckAndNotifyInventory();
                    break;
            }
        }

        private void StartNextBatch()
        {
            batchStartTime = DateTime.Now; // Registramos el inicio del nuevo lote
            currentBatchName = "Batch_" + GetNextBatchNumber().ToString("D4"); // Guardamos el nombre del lote actual
            UpdateUI(() =>
            {
                button1.Text = "Empezar";
                label7.Text = "0 kg de Malta"; // Reiniciamos con valor inicial
                label7.Location = new Point(250, 339); // Mantemos posición original
                label8.Text = "0 L de Agua";  // Reiniciamos con valor inicial
                label8.Location = new Point(630, 339); // Mantemos posición original
                label9.Text = "0 kg de Levadura"; // Reiniciamos con valor inicial
                label9.Location = new Point(1100, 339); // Mantemos posición original
                label10.Text = "0 botellas"; // Reiniciamos con valor inicial
                label10.Location = new Point(1100, 738); // Mantemos posición original
                progressBar1.Value = 0; // Reiniciamos la barra de progreso
                progressLabel.Text = "Progreso: 0%";
                // No cambiamos label1, lo mantenemos como título fijo "Almacén de productos terminados:"
                // Reiniciamos textBox1 a vacío para el nuevo ciclo de batches
                if (textBox1 != null)
                {
                    textBox1.Text = "";
                }
                // Actualizamos los labels de "Batch:" con el currentBatchName
                label24.Text = $"Batch: {currentBatchName}"; // Molienda
                label25.Text = $"Batch: {currentBatchName}"; // Cocción
                label26.Text = $"Batch: {currentBatchName}"; // Fermentación
                label27.Text = $"Batch: {currentBatchName}"; // Embotellado
                // Reiniciamos los labels de inicio y fin de etapas
                moliendaStartLabel.Text = "--/--/---- --:--:--";
                moliendaEndLabel.Text = "--/--/---- --:--:--";
                coccionStartLabel.Text = "--/--/---- --:--:--";
                coccionEndLabel.Text = "--/--/---- --:--:--";
                fermentacionStartLabel.Text = "--/--/---- --:--:--";
                fermentacionEndLabel.Text = "--/--/---- --:--:--";
                embotelladoStartLabel.Text = "--/--/---- --:--:--";
                embotelladoEndLabel.Text = "--/--/---- --:--:--";
            });

            var produccion = new Produccion
            {
                BatchName = currentBatchName,
                Status = "Started",
                StartDate = batchStartTime,
                EndDate = batchStartTime.AddMinutes(345) // Duración total estimada (15 + 60 + 240 + 30 = 345 minutos)
            };

            _produccionCollection.InsertOne(produccion);
            UpdateUI(() =>
            {
                batchDetails = $"Batch {currentBatchName} Details:\n";
                button1.Text = "En ejecución";
            });
            _timer.Enabled = true;
            isRunning = true;
            stage = 0;
            batchCount = 0; // Reiniciamos el contador de batches
            CheckAndNotifyInventory(); // Verificamos inventario al iniciar un nuevo lote
            Array.Clear(stageStartTimes, 0, stageStartTimes.Length); // Reiniciamos los tiempos de las etapas
        }

        private void UpdateUI(Action action)
        {
            if (InvokeRequired)
            {
                Invoke(action);
            }
            else
            {
                action();
            }
        }

        private decimal ProcesarMolienda(decimal cantidadMalta)
        {
            return cantidadMalta; // Usamos toda la cantidad de Malta (200 kg) sin pérdida
        }

        private decimal ProcesarCoccion(decimal cantidadAgua)
        {
            return cantidadAgua; // Usamos toda la cantidad de Agua (1,200 L) sin pérdida
        }

        private decimal ProcesarFermentacion(decimal cantidadLevadura)
        {
            return cantidadLevadura; // Usamos toda la cantidad de Levadura (0.75 kg) sin pérdida
        }

        private decimal ProcesarEmbotellado(decimal cantidadBotellas)
        {
            return cantidadBotellas; // Usamos exactamente 1,000 botellas sin pérdida
        }

        private Tuple<decimal, decimal, decimal, decimal, decimal> CalcularTiemposDeProceso()
        {
            decimal molienda = 15m; // 15 minutos
            decimal coccion = 60m; // 60 minutos
            decimal fermentacion = 240m; // 240 minutos
            decimal embotellado = 30m; // 30 minutos
            decimal fermentacionTanque = 5m;
            decimal maduracionTanque = 10m;
            decimal totalTanque = fermentacionTanque + maduracionTanque;

            return new Tuple<decimal, decimal, decimal, decimal, decimal>(molienda, coccion, fermentacion, embotellado, totalTanque);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (isRunning)
            {
                MessageBox.Show("A batch is already running.");
                return;
            }

            isRunning = true;
            int nextBatchNumber = GetNextBatchNumber();
            var produccion = new Produccion
            {
                BatchName = "Batch_" + nextBatchNumber.ToString("D4"),
                Status = "Started",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMinutes(345) // Duración total estimada (15 + 60 + 240 + 30 = 345 minutos)
            };

            _produccionCollection.InsertOne(produccion);
            UpdateUI(() =>
            {
                // No cambiamos label1, lo mantenemos como "Almacén de productos terminados:"
                batchDetails = $"Batch {produccion.BatchName} Details:\n";
                button1.Text = "En ejecución";
                label7.Text = "0 kg de Malta"; // Reiniciamos con valor inicial
                label7.Location = new Point(250, 339); // Mantemos posición original
                label8.Text = "0 L de Agua";  // Reiniciamos con valor inicial
                label8.Location = new Point(630, 339); // Mantemos posición original
                label9.Text = "0 kg de Levadura"; // Reiniciamos con valor inicial
                label9.Location = new Point(1100, 339); // Mantemos posición original
                label10.Text = "0 botellas"; // Reiniciamos con valor inicial
                label10.Location = new Point(1100, 738); // Mantemos posición original
                // Reiniciamos textBox1 a vacío para el nuevo batch
                if (textBox1 != null)
                {
                    textBox1.Text = "";
                }
                // Actualizamos los labels de "Batch:" con el currentBatchName
                label24.Text = $"Batch: {produccion.BatchName}"; // Molienda
                label25.Text = $"Batch: {produccion.BatchName}"; // Cocción
                label26.Text = $"Batch: {produccion.BatchName}"; // Fermentación
                label27.Text = $"Batch: {produccion.BatchName}"; // Embotellado
            });
            _timer.Enabled = true;
            stage = 0;
            batchCount = 0; // Reiniciamos el contador de batches
            CheckAndNotifyInventory(); // Verificamos inventario al iniciar
        }

        private int GetNextBatchNumber()
        {
            var filter = Builders<Counter>.Filter.Eq(c => c.Name, "BatchNumber");
            var update = Builders<Counter>.Update.Inc(c => c.Value, 1);
            var options = new FindOneAndUpdateOptions<Counter> { ReturnDocument = ReturnDocument.After };

            var counter = _counterCollection.FindOneAndUpdate(filter, update, options);

            if (counter == null)
            {
                counter = new Counter { Name = "BatchNumber", Value = 1 };
                _counterCollection.InsertOne(counter);
            }

            return counter.Value;
        }

        private string FormatearDetallesProceso(params string[] detalles)
        {
            var sb = new StringBuilder();
            foreach (var detalle in detalles)
            {
                sb.AppendLine(detalle);
            }
            return sb.ToString();
        }

        private void exportToPDFButton_Click(object sender, EventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                Title = "Guardar como PDF"
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                _productionLog.ExportProductionLogsToPDF(saveFileDialog.FileName);
                MessageBox.Show("Exportado a PDF exitosamente");
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!isRunning)
            {
                MessageBox.Show("No hay ningún proceso en ejecución para pausar.");
                return;
            }

            if (isPaused)
            {
                isPaused = false;
                button2.Text = "Pausar";
                _timer.Enabled = true; // Reanudamos el timer
            }
            else
            {
                isPaused = true;
                button2.Text = "Continuar";
                _timer.Enabled = false; // Pausamos el timer
            }
        }

        // Botón de "Parar" (Autocompletar etapas restantes)
        private void button3_Click(object sender, EventArgs e)
        {
            if (isRunning || isPaused)
            {
                // Si el proceso está en ejecución, completar las etapas restantes automáticamente
                while (stage < 4) // 4 etapas: Molienda (0), Cocción (1), Fermentación (2), Embotellado (3)
                {
                    switch (stage)
                    {
                        case 0: // Molienda
                            batchStartTime = DateTime.Now; // Reiniciamos el tiempo de inicio para simular la etapa
                            stageStartTimes[stage] = batchStartTime;
                            cantidadMalta = 200m; // Fijo: 200 kg de Malta por batch
                            decimal molienda = ProcesarMolienda(cantidadMalta);
                            decimal sobranteMalta = almacenMalta - cantidadMalta;
                            almacenMalta = sobranteMalta; // Actualizamos el inventario
                            batchDetails = FormatearDetallesProceso($"Molienda: {molienda:F3} kg de Malta", $"Sobrante Malta: {sobranteMalta:F3} kg");
                            DateTime moliendaEnd = batchStartTime.AddMinutes(15); // Fin de Molienda después de 15 minutos
                            UpdateUI(() =>
                            {
                                label7.Text = $"{molienda:F3} kg de Malta"; // Mostramos la cantidad procesada
                                moliendaStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                moliendaEndLabel.Text = $"Fin: {moliendaEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                            });
                            break;
                        case 1: // Cocción
                            stageStartTimes[stage] = batchStartTime.AddMinutes(15); // Inicio de Cocción
                            cantidadAgua = 1200m; // Fijo: 1200 L de Agua por batch
                            decimal coccion = ProcesarCoccion(cantidadAgua);
                            decimal sobranteAgua = almacenAgua - cantidadAgua;
                            almacenAgua = sobranteAgua; // Actualizamos el inventario
                            batchDetails = FormatearDetallesProceso(batchDetails, $"Cocción: {coccion:F3} L de Agua", $"Sobrante Agua: {sobranteAgua:F3} L");
                            DateTime coccionEnd = stageStartTimes[stage].AddMinutes(60); // Fin de Cocción después de 60 minutos
                            UpdateUI(() =>
                            {
                                label8.Text = $"{coccion:F3} L de Agua"; // Mostramos la cantidad procesada
                                coccionStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                coccionEndLabel.Text = $"Fin: {coccionEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                            });
                            break;
                        case 2: // Fermentación
                            stageStartTimes[stage] = batchStartTime.AddMinutes(75); // Inicio de Fermentación
                            cantidadLevadura = 0.75m; // Fijo: 0.75 kg de Levadura por batch
                            decimal fermentacion = ProcesarFermentacion(cantidadLevadura);
                            decimal sobranteLevadura = almacenLevadura - cantidadLevadura;
                            almacenLevadura = sobranteLevadura; // Actualizamos el inventario
                            batchDetails = FormatearDetallesProceso(batchDetails, $"Fermentación: {fermentacion:F3} kg de Levadura", $"Sobrante Levadura: {sobranteLevadura:F3} kg");
                            DateTime fermentacionEnd = stageStartTimes[stage].AddMinutes(240); // Fin de Fermentación después de 240 minutos
                            UpdateUI(() =>
                            {
                                label9.Text = $"{fermentacion:F3} kg de Levadura"; // Mostramos la cantidad procesada
                                fermentacionStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                fermentacionEndLabel.Text = $"Fin: {fermentacionEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                            });
                            break;
                        case 3: // Embotellado
                            stageStartTimes[stage] = batchStartTime.AddMinutes(315); // Inicio de Embotellado
                            cantidadBotellas = 1000m; // Fijo: 1000 botellas por batch
                            decimal embotellado = ProcesarEmbotellado(cantidadBotellas);
                            decimal sobranteBotellas = almacenBotellas - cantidadBotellas;
                            almacenBotellas = sobranteBotellas; // Actualizamos el inventario
                            batchDetails = FormatearDetallesProceso(batchDetails, $"Embotellado: {Math.Round(embotellado):F0} botellas", $"Sobrante Botellas: {sobranteBotellas:F0} botellas");
                            DateTime embotelladoEnd = stageStartTimes[stage].AddMinutes(30); // Fin de Embotellado después de 30 minutos
                            GuardarBotellasLlenas(currentBatchName, Math.Round(embotellado), embotelladoEnd); // Guardamos en la base de datos
                            UpdateUI(() =>
                            {
                                label10.Text = $"{Math.Round(embotellado):F0} botellas"; // Mostramos la cantidad procesada
                                embotelladoStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                embotelladoEndLabel.Text = $"Fin: {embotelladoEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                            });
                            break;
                    }
                    stage++; // Avanzar a la siguiente etapa
                }

                // Actualizar la interfaz con los detalles del batch completado
                UpdateUI(() =>
                {
                    textBox1.Text = batchDetails; // Mostrar todos los detalles del proceso
                    label15.Text = $"Malta de cebada: {almacenMalta:F3} kg"; // Actualizamos el inventario de malta
                    label16.Text = $"Agua: {almacenAgua:F3} L"; // Actualizamos el inventario de agua
                    label17.Text = $"Levadura: {almacenLevadura:F3} kg"; // Actualizamos el inventario de levadura
                    label18.Text = $"Botellas: {almacenBotellas:F0}"; // Actualizamos el inventario de botellas
                    progressBar1.Value = 100; // Completar el progreso
                    progressLabel.Text = "Progreso: 100%";
                });

                // Actualizar el fin del lote en la base de datos
                UpdateBatchEndTime(DateTime.Now);

                // Incrementamos el contador de batches (para respetar los 10 batches)
                batchCount++;
                if (batchCount < TOTAL_BATCHES)
                {
                    stage = 0; // Reiniciamos el stage para el próximo batch si no se han completado los 10
                }
                else
                {
                    isRunning = false;
                    _timer.Enabled = false;
                    UpdateUI(() =>
                    {
                        button1.Text = "Empezar";
                        button2.Text = "Pausar";
                        progressLabel.Text = "Progreso: 100% (10,000 botellas completadas)";
                    });
                    batchCount = 0; // Reiniciamos el contador para un nuevo ciclo
                }

                // Detener el proceso
                isRunning = false;
                isPaused = false;
                _timer.Stop();

                // Mostrar el mensaje de batch completado
                MessageBox.Show($"El batch {currentBatchName} se ha completado exitosamente.", "Proceso Finalizado", MessageBoxButtons.OK, MessageBoxIcon.Information);

                CheckAndNotifyInventory(); // Verificamos el estado del inventario tras el proceso
            }
            else
            {
                MessageBox.Show("No hay ningún proceso en ejecución para detener.");
            }
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            // Lógica para el evento del timer (si aplica)
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            // Lógica para el evento del picture box
        }

        private void label7_Click(object sender, EventArgs e)
        {
            // Lógica para el evento del label
        }

        private void label3_Click(object sender, EventArgs e)
        {
            // Lógica para el evento del label (aunque ya no usamos label3 para detalles)
        }

        private void pictureBox5_Click(object sender, EventArgs e)
        {
            // Lógica para el evento del picture box
        }

        private void label11_Click(object sender, EventArgs e)
        {
            // Lógica para el evento del label (este es el nuevo label11, ajusta según necesites)
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var dateTimeTimer = new System.Windows.Forms.Timer(); // Usamos System.Windows.Forms.Timer explícitamente
            dateTimeTimer.Interval = 1000; // 1 segundo
            dateTimeTimer.Tick += UpdateDateTimeLabel;
            dateTimeTimer.Start();
            CheckAndNotifyInventory(); // Verificamos inventario al cargar
            // Actualizamos los labels del almacén con los valores iniciales al cargar
            UpdateUI(() =>
            {
                label15.Text = $"Malta de cebada: {almacenMalta:F3} kg";
                label16.Text = $"Agua: {almacenAgua:F3} L";
                label17.Text = $"Levadura: {almacenLevadura:F3} kg";
                label18.Text = $"Botellas: {almacenBotellas:F0}";
            });

            // Inicializamos los ingredientes en la base de datos si no existen
            InitializeInventory();
        }

        private void UpdateDateTimeLabel(object sender, EventArgs e)
        {
            dateTimeLabel.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        }

        private void dateTimeLabel_Click(object sender, EventArgs e)
        {
            // Lógica para el evento del dateTimeLabel (si aplica)
        }

        private void CheckAndNotifyInventory()
        {
            _inventoryManager.CheckInventoryLevels();
            string statusMessage = "Estado del inventario: Normal";
            UpdateUI(() =>
            {
                var ingredientes = _inventoryManager.GetAllIngredientes();
                bool hasLowInventory = false;
                foreach (var ingrediente in ingredientes)
                {
                    if (ingrediente.Cantidad < ingrediente.UmbralMinimo)
                    {
                        hasLowInventory = true;
                        switch (ingrediente.Nombre.ToLower())
                        {
                            case "malta":
                                label15.ForeColor = Color.Red;
                                statusMessage = $"Alerta: Malta bajo ({ingrediente.Cantidad:F3} kg)";
                                break;
                            case "agua":
                                label16.ForeColor = Color.Red;
                                statusMessage = $"Alerta: Agua bajo ({ingrediente.Cantidad:F3} L)";
                                break;
                            case "levadura":
                                label17.ForeColor = Color.Red;
                                statusMessage = $"Alerta: Levadura bajo ({ingrediente.Cantidad:F3} kg)";
                                break;
                            case "botellas":
                                label18.ForeColor = Color.Red;
                                statusMessage = $"Alerta: Botellas bajo ({ingrediente.Cantidad:F0} unidades)";
                                break;
                        }
                    }
                    else
                    {
                        // Restaurar color por defecto si el inventario está bien
                        label15.ForeColor = SystemColors.ControlText;
                        label16.ForeColor = SystemColors.ControlText;
                        label17.ForeColor = SystemColors.ControlText;
                        label18.ForeColor = SystemColors.ControlText;
                    }
                }
                inventoryStatusLabel.Text = statusMessage;
                inventoryStatusLabel.ForeColor = hasLowInventory ? Color.Red : SystemColors.ControlText;
            });
        }

        private void InitializeInventory()
        {
            var ingredientes = _inventoryManager.GetAllIngredientes();
            if (!ingredientes.Exists(i => i.Nombre.ToLower() == "malta"))
            {
                _inventoryManager.AddIngrediente(new Ingrediente { Nombre = "Malta", Cantidad = 2000m, UmbralMinimo = 200m }); // Umbral mínimo ajustado
            }
            if (!ingredientes.Exists(i => i.Nombre.ToLower() == "agua"))
            {
                _inventoryManager.AddIngrediente(new Ingrediente { Nombre = "Agua", Cantidad = 12000m, UmbralMinimo = 1200m }); // Umbral mínimo ajustado
            }
            if (!ingredientes.Exists(i => i.Nombre.ToLower() == "levadura"))
            {
                _inventoryManager.AddIngrediente(new Ingrediente { Nombre = "Levadura", Cantidad = 30m, UmbralMinimo = 0.75m }); // Umbral mínimo ajustado al uso por batch
            }
            if (!ingredientes.Exists(i => i.Nombre.ToLower() == "botellas"))
            {
                _inventoryManager.AddIngrediente(new Ingrediente { Nombre = "Botellas", Cantidad = 10000m, UmbralMinimo = 1000m }); // Mantenemos umbral mínimo
            }
        }

        // Comentado porque restockButton no está en el diseñador
        /*
        private void restockButton_Click(object sender, EventArgs e)
        {
            // Reabastecemos los inventarios a sus valores iniciales
            almacenMalta = 2000m;
            almacenAgua = 12000m;
            almacenLevadura = 30m;
            almacenBotellas = 10000m;

            // Actualizamos los labels y la base de datos
            UpdateUI(() =>
            {
                label15.Text = $"Malta de cebada: {almacenMalta:F3} kg";
                label16.Text = $"Agua: {almacenAgua:F3} L";
                label17.Text = $"Levadura: {almacenLevadura:F3} kg";
                label18.Text = $"Botellas: {almacenBotellas:F0}";
                label15.ForeColor = SystemColors.ControlText;
                label16.ForeColor = SystemColors.ControlText;
                label17.ForeColor = SystemColors.ControlText;
                label18.ForeColor = SystemColors.ControlText;
                inventoryStatusLabel.Text = "Estado del inventario: Normal";
                inventoryStatusLabel.ForeColor = SystemColors.ControlText;
            });

            // Actualizamos la base de datos
            var ingredientes = _inventoryManager.GetAllIngredientes();
            foreach (var ingrediente in ingredientes)
            {
                decimal cantidadInicial = 0m;
                switch (ingrediente.Nombre.ToLower())
                {
                    case "malta":
                        cantidadInicial = 2000m;
                        break;
                    case "agua":
                        cantidadInicial = 12000m;
                        break;
                    case "levadura":
                        cantidadInicial = 30m;
                        break;
                    case "botellas":
                        cantidadInicial = 10000m;
                        break;
                }
                _inventoryManager.UpdateIngredienteQuantity(ingrediente.Nombre, cantidadInicial);
            }

            MessageBox.Show("Inventario reabastecido exitosamente.");
        }
        */

        private void restockButton_Click_2(object sender, EventArgs e) // Método para reiniciar el "Almacén de productos terminados"
        {
            UpdateUI(() =>
            {
                if (textBox1 != null)
                {
                    textBox1.Text = ""; // Reiniciamos textBox1 a vacío
                }
            });
            // Limpiamos los datos en MongoDB (opcional, para reinicio completo)
            var filter = Builders<BsonDocument>.Filter.Empty;
            _batchesBotellasCollection.DeleteMany(filter);
            MessageBox.Show("Almacén de botellas llenas reiniciado exitosamente.");
        }

        private void UpdateBatchEndTime(DateTime endTime)
        {
            // Eliminamos referencias a batchEndLabel para evitar NullReferenceException
            // Actualizamos el EndDate en la base de datos para el lote actual
            var filter = Builders<Produccion>.Filter.Eq(p => p.BatchName, currentBatchName);
            var update = Builders<Produccion>.Update.Set(p => p.EndDate, endTime);
            _produccionCollection.UpdateOne(filter, update);
        }

        private void GuardarBotellasLlenas(string batchName, decimal cantidad, DateTime fechaProduccion)
        {
            if (string.IsNullOrEmpty(batchName))
            {
                batchName = "Batch_Unknown"; // Valor por defecto si batchName es nulo
            }
            var document = new BsonDocument
            {
                { "BatchName", batchName },
                { "CantidadBotellas", cantidad },
                { "FechaProduccion", fechaProduccion.ToString("dd/MM/yyyy HH:mm:ss") }
            };
            _batchesBotellasCollection.InsertOne(document);
        }

        private void GeneratePDFOnPauseOrStop()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                Title = "Guardar informe del proceso pausado/parado",
                FileName = $"{currentBatchName}_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.pdf"
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                _productionLog.ExportProductionLogsToPDF(saveFileDialog.FileName);
                MessageBox.Show("Informe exportado a PDF exitosamente.");
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {
        }

        private void label24_Click(object sender, EventArgs e)
        {
        }
    }
}