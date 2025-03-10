using System;
using System.Text;
using System.Timers;
using System.Windows.Forms;
using MongoDB.Driver;
using System.Drawing;
using MongoDB.Bson;
using System.Threading;

namespace BrewScada
{
    public partial class Form1 : Form
    {
        private MongoDBConnection _dbConnection;
        private IMongoCollection<Produccion> _produccionCollection;
        private IMongoCollection<Counter> _counterCollection;
        private IMongoCollection<Ingrediente> _ingredientesCollection;
        private IMongoCollection<BsonDocument> _batchesBotellasCollection;
        private System.Windows.Forms.Timer _processTimer; // Timer para las etapas principales (7.5 segundos por hora)
        private System.Windows.Forms.Timer _delayTimer;   // Timer para los 10 segundos de retraso en embotellado
        private Random _random;
        private bool isRunning;
        private bool isPaused;
        private string batchDetails;
        private decimal cantidadMalta;
        private decimal cantidadAgua;
        private decimal cantidadLevadura;
        private decimal cantidadBotellas;
        private int stage;
        private DateTime batchStartTime;
        private DateTime[] stageStartTimes;
        private string currentBatchName;
        private int batchCount;
        private const int TOTAL_BATCHES = 10;

        // Tiempos en horas para cada etapa
        private readonly int[] stageDurations = new int[] { 1, 2, 96, 4 }; // Molienda: 1h, Cocción: 2h, Fermentación: 96h, Embotellado: 4h
        private int[] stageUpdates; // Contador de actualizaciones por etapa
        private const int TOTAL_UPDATES = 103; // 1 + 2 + 96 + 4
        private int lastBatchNumber; // Para rastrear el último batch procesado

        private decimal almacenMalta = 2000m;
        private decimal almacenAgua = 12000m;
        private decimal almacenLevadura = 7.5m;
        private decimal almacenBotellas = 10000m;

        private InventoryManager _inventoryManager;
        private ProductionLog _productionLog;

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
            _batchesBotellasCollection = _dbConnection.GetCollection<BsonDocument>("BatchesBotellas");

            _random = new Random();
            InitializeTimers();
            isRunning = false;
            isPaused = false;
            batchDetails = string.Empty;
            stage = 0;
            stageStartTimes = new DateTime[4];
            batchCount = 0;
            stageUpdates = new int[4]; // Inicializamos el contador de actualizaciones por etapa
            lastBatchNumber = 0; // Inicializamos el último batch número

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

            // Depuración de asignación de labels
            Console.WriteLine($"moliendaStartLabel: {(moliendaStartLabel != null ? "OK" : "NULL")}");
            Console.WriteLine($"moliendaEndLabel: {(moliendaEndLabel != null ? "OK" : "NULL")}");
            Console.WriteLine($"coccionStartLabel: {(coccionStartLabel != null ? "OK" : "NULL")}");
            Console.WriteLine($"coccionEndLabel: {(coccionEndLabel != null ? "OK" : "NULL")}");
            Console.WriteLine($"fermentacionStartLabel: {(fermentacionStartLabel != null ? "OK" : "NULL")}");
            Console.WriteLine($"fermentacionEndLabel: {(fermentacionEndLabel != null ? "OK" : "NULL")}");
            Console.WriteLine($"embotelladoStartLabel: {(embotelladoStartLabel != null ? "OK" : "NULL")}");
            Console.WriteLine($"embotelladoEndLabel: {(embotelladoEndLabel != null ? "OK" : "NULL")}");
        }

        private string GetConnectionStringFromConfig()
        {
            return "mongodb://localhost:27017";
        }

        private void InitializeTimers()
        {
            _processTimer = new System.Windows.Forms.Timer();
            _processTimer.Interval = 7500; // 7.5 segundos por hora
            _processTimer.Tick += OnTimedEvent;
            _processTimer.Enabled = false;

            _delayTimer = new System.Windows.Forms.Timer();
            _delayTimer.Interval = 10000; // 10 segundos de retraso
            _delayTimer.Tick += OnDelayTimerTick;
            _delayTimer.Enabled = false;
        }

        private void OnTimedEvent(object sender, EventArgs e)
        {
            if (!isRunning || isPaused) return;

            Console.WriteLine($"OnTimedEvent triggered at {DateTime.Now}, Stage: {stage}, Updates: {stageUpdates[stage]}"); // Depuración

            // Generamos un nuevo currentBatchName para cada batch al inicio
            if (stage == 0 && stageUpdates[stage] == 0)
            {
                currentBatchName = "Batch_" + GetNextBatchNumber().ToString("D4");
                UpdateUI(() =>
                {
                    label24.Text = $"Batch: {currentBatchName}";
                    label25.Text = $"Batch: {currentBatchName}";
                    label26.Text = $"Batch: {currentBatchName}";
                    label27.Text = $"Batch: {currentBatchName}";
                    moliendaStartLabel.Text = "Inicio: --/--/-- --:--:--";
                    moliendaEndLabel.Text = "Fin: --/--/-- --:--:--";
                    coccionStartLabel.Text = "Inicio: --/--/-- --:--:--";
                    coccionEndLabel.Text = "Fin: --/--/-- --:--:--";
                    fermentacionStartLabel.Text = "Inicio: --/--/-- --:--:--";
                    fermentacionEndLabel.Text = "Fin: --/--/-- --:--:--";
                    embotelladoStartLabel.Text = "Inicio: --/--/-- --:--:--";
                    embotelladoEndLabel.Text = "Fin: --/--/-- --:--:--";
                    progressBar1.Value = 0;
                    progressLabel.Text = $"Progreso: 0%";
                    Array.Clear(stageUpdates, 0, stageUpdates.Length); // Reiniciamos contadores de actualizaciones
                });
            }

            // Avanzamos según el número de actualizaciones por etapa
            if (stageUpdates[stage] < stageDurations[stage])
            {
                stageUpdates[stage]++;
                UpdateProgress();
            }

            // Calculamos los tiempos y actualizamos las etiquetas solo al completar cada etapa
            switch (stage)
            {
                case 0: // Molienda (1 hora = 1 actualización)
                    if (stageUpdates[stage] == 1)
                    {
                        batchStartTime = DateTime.Now;
                        stageStartTimes[stage] = batchStartTime;
                        cantidadMalta = 200m;
                        decimal molienda = ProcesarMolienda(cantidadMalta);
                        decimal sobranteMalta = almacenMalta - cantidadMalta;
                        almacenMalta = sobranteMalta;
                        batchDetails = FormatearDetallesProceso($"Molienda:{molienda:F3} kg de Malta", $"Sobrante Malta: {sobranteMalta:F3} kg");
                        DateTime moliendaEnd = stageStartTimes[stage].AddHours(1); // 1 hora = 7.5 segundos
                        UpdateUI(() =>
                        {
                            label7.Text = $"{molienda:F3} kg de Malta";
                            label7.Location = new Point(250, 339);
                            label15.Text = $"Malta de cebada: {almacenMalta:F3} kg";
                            if (moliendaStartLabel != null)
                            {
                                moliendaStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                Console.WriteLine($"Molienda Start set to: {moliendaStartLabel.Text}");
                            }
                            if (moliendaEndLabel != null)
                            {
                                moliendaEndLabel.Text = $"Fin: {moliendaEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                                Console.WriteLine($"Molienda End set to: {moliendaEndLabel.Text}");
                            }
                            moliendaStartLabel?.Refresh();
                            moliendaEndLabel?.Refresh();
                        });
                        stage++;
                    }
                    break;

                case 1: // Cocción (2 horas = 2 actualizaciones)
                    if (stageUpdates[stage] == 1)
                    {
                        stageStartTimes[stage] = DateTime.Now;
                        cantidadAgua = 1200m;
                        decimal coccion = ProcesarCoccion(cantidadAgua);
                        decimal sobranteAgua = almacenAgua - cantidadAgua;
                        almacenAgua = sobranteAgua;
                        batchDetails = FormatearDetallesProceso(batchDetails, $"Cocción: {coccion:F3} L de Agua", $"Sobrante Agua: {sobranteAgua:F3} L");
                    }
                    if (stageUpdates[stage] == 2)
                    {
                        DateTime coccionEnd = stageStartTimes[stage].AddHours(2); // 2 horas = 15 segundos
                        UpdateUI(() =>
                        {
                            label8.Text = $"{cantidadAgua:F3} L de Agua";
                            label8.Location = new Point(630, 339);
                            label16.Text = $"Agua: {almacenAgua:F3} L";
                            if (coccionStartLabel != null)
                            {
                                coccionStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                Console.WriteLine($"Cocción Start set to: {coccionStartLabel.Text}");
                            }
                            if (coccionEndLabel != null)
                            {
                                coccionEndLabel.Text = $"Fin: {coccionEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                                Console.WriteLine($"Cocción End set to: {coccionEndLabel.Text}");
                            }
                            coccionStartLabel?.Refresh();
                            coccionEndLabel?.Refresh();
                        });
                        stage++;
                    }
                    break;

                case 2: // Fermentación (96 horas = 96 actualizaciones)
                    if (stageUpdates[stage] == 1)
                    {
                        stageStartTimes[stage] = DateTime.Now;
                        cantidadLevadura = 0.75m;
                        decimal fermentacion = ProcesarFermentacion(cantidadLevadura);
                        decimal sobranteLevadura = almacenLevadura - cantidadLevadura;
                        almacenLevadura = sobranteLevadura;
                        batchDetails = FormatearDetallesProceso(batchDetails, $"Fermentación: {fermentacion:F3} kg de Levadura", $"Sobrante Levadura: {sobranteLevadura:F3} kg");
                    }
                    if (stageUpdates[stage] == 96)
                    {
                        DateTime fermentacionEnd = stageStartTimes[stage].AddHours(96); // 96 horas = 720 segundos
                        UpdateUI(() =>
                        {
                            label9.Text = $"{cantidadLevadura:F3} kg de Levadura";
                            label9.Location = new Point(1100, 339);
                            label17.Text = $"Levadura: {almacenLevadura:F3} kg";
                            if (fermentacionStartLabel != null)
                            {
                                fermentacionStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                Console.WriteLine($"Fermentación Start set to: {fermentacionStartLabel.Text}");
                            }
                            if (fermentacionEndLabel != null)
                            {
                                fermentacionEndLabel.Text = $"Fin: {fermentacionEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                                Console.WriteLine($"Fermentación End set to: {fermentacionEndLabel.Text}");
                            }
                            fermentacionStartLabel?.Refresh();
                            fermentacionEndLabel?.Refresh(); // Corrección aplicada
                        });
                        stage++;
                    }
                    break;

                case 3: // Embotellado (4 horas = 4 actualizaciones)
                    if (stageUpdates[stage] == 1)
                    {
                        stageStartTimes[stage] = DateTime.Now;
                        cantidadBotellas = 1000m;
                        decimal embotellado = ProcesarEmbotellado(cantidadBotellas);
                        decimal sobranteBotellas = almacenBotellas - cantidadBotellas;
                        almacenBotellas = sobranteBotellas;
                        batchDetails = FormatearDetallesProceso(batchDetails, $"Embotellado: {Math.Round(embotellado):F0} botellas", $"Sobrante Botellas: {sobranteBotellas:F0} botellas");
                    }
                    if (stageUpdates[stage] == 4)
                    {
                        DateTime embotelladoEnd = stageStartTimes[stage].AddHours(4); // 4 horas = 30 segundos
                        UpdateUI(() =>
                        {
                            label10.Text = $"{Math.Round(cantidadBotellas):F0} botellas";
                            label10.Location = new Point(1100, 738);
                            label18.Text = $"Botellas: {almacenBotellas:F0}";
                            if (embotelladoStartLabel != null)
                            {
                                embotelladoStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                Console.WriteLine($"Embotellado Start set to: {embotelladoStartLabel.Text}");
                            }
                            if (embotelladoEndLabel != null)
                            {
                                embotelladoEndLabel.Text = $"Fin: {embotelladoEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                                Console.WriteLine($"Embotellado End set to: {embotelladoEndLabel.Text}");
                            }
                            embotelladoStartLabel?.Refresh();
                            embotelladoEndLabel?.Refresh();
                        });
                        // Iniciamos el timer de retraso de 10 segundos
                        _delayTimer.Enabled = true;
                        _processTimer.Enabled = false; // Pausamos el timer principal
                    }
                    break;
            }
        }

        private void OnDelayTimerTick(object sender, EventArgs e)
        {
            _delayTimer.Enabled = false; // Desactivamos el timer de retraso

            // Solo guardamos el batch si el proceso no fue detenido con "Parar"
            if (isRunning)
            {
                GuardarBotellasLlenas(currentBatchName, Math.Round(cantidadBotellas), DateTime.Now);
                // Actualizamos el último batch número procesado
                lastBatchNumber = int.Parse(currentBatchName.Replace("Batch_", ""));
            }

            UpdateUI(() =>
            {
                if (textBox1 != null)
                    textBox1.Text += $"{currentBatchName}: {Math.Round(cantidadBotellas):F0} botellas\r\n";
                progressBar1.Value = 0; // Reiniciamos la barra al 0% al final del batch
                progressLabel.Text = $"Progreso: 0%"; // Mostramos 0% explícitamente
            });

            // Reiniciamos para el próximo batch sin limpiar el historial
            UpdateUI(() =>
            {
                label7.Text = "0 kg de Malta";
                label8.Text = "0 L de Agua";
                label9.Text = "0 kg de Levadura";
                label10.Text = "0 botellas";
                moliendaStartLabel.Text = "Inicio: --/--/-- --:--:--";
                moliendaEndLabel.Text = "Fin: --/--/-- --:--:--";
                coccionStartLabel.Text = "Inicio: --/--/-- --:--:--";
                coccionEndLabel.Text = "Fin: --/--/-- --:--:--";
                fermentacionStartLabel.Text = "Inicio: --/--/-- --:--:--";
                fermentacionEndLabel.Text = "Fin: --/--/-- --:--:--";
                embotelladoStartLabel.Text = "Inicio: --/--/-- --:--:--";
                embotelladoEndLabel.Text = "Fin: --/--/-- --:--:--";
            });
            batchCount++;
            if (batchCount < TOTAL_BATCHES)
            {
                stage = 0;
                _processTimer.Enabled = true; // Reiniciamos el timer principal
            }
            else
            {
                isRunning = false;
                UpdateUI(() =>
                {
                    button1.Text = "Empezar";
                    button2.Text = "Pausar";
                    progressLabel.Text = "Progreso: 100% (10,000 botellas completadas)"; // Mensaje final claro
                });
                batchCount = 0;
            }
            CheckAndNotifyInventory();
        }

        private void StartNextBatch()
        {
            batchStartTime = DateTime.Now;
            currentBatchName = "Batch_" + GetNextBatchNumber().ToString("D4");
            UpdateUI(() =>
            {
                button1.Text = "Empezar";
                label7.Text = "0 kg de Malta";
                label7.Location = new Point(250, 339);
                label8.Text = "0 L de Agua";
                label8.Location = new Point(630, 339);
                label9.Text = "0 kg de Levadura";
                label9.Location = new Point(1100, 339);
                label10.Text = "0 botellas";
                label10.Location = new Point(1100, 738);
                progressBar1.Value = 0; // Reiniciamos la barra al 0% al iniciar un nuevo batch
                progressLabel.Text = $"Progreso: 0%";
                // Eliminamos la limpieza de textBox1 al iniciar un nuevo batch
                label24.Text = $"Batch: {currentBatchName}";
                label25.Text = $"Batch: {currentBatchName}";
                label26.Text = $"Batch: {currentBatchName}";
                label27.Text = $"Batch: {currentBatchName}";
                moliendaStartLabel.Text = "Inicio: --/--/-- --:--:--";
                moliendaEndLabel.Text = "Fin: --/--/-- --:--:--";
                coccionStartLabel.Text = "Inicio: --/--/-- --:--:--";
                coccionEndLabel.Text = "Fin: --/--/-- --:--:--";
                fermentacionStartLabel.Text = "Inicio: --/--/-- --:--:--";
                fermentacionEndLabel.Text = "Fin: --/--/-- --:--:--";
                embotelladoStartLabel.Text = "Inicio: --/--/-- --:--:--";
                embotelladoEndLabel.Text = "Fin: --/--/-- --:--:--";
            });

            var produccion = new Produccion
            {
                BatchName = currentBatchName,
                Status = "Started",
                StartDate = batchStartTime,
                EndDate = batchStartTime.AddHours(103) // Aproximado para 103 horas totales
            };

            _produccionCollection.InsertOne(produccion);
            UpdateUI(() =>
            {
                batchDetails = $"Batch {currentBatchName} Details:\n";
                button1.Text = "En ejecución";
            });
            _processTimer.Enabled = true;
            isRunning = true;
            stage = 0;
            batchCount = 0;
            CheckAndNotifyInventory();
            Array.Clear(stageStartTimes, 0, stageStartTimes.Length);
            Array.Clear(stageUpdates, 0, stageUpdates.Length); // Reiniciamos contadores
            lastBatchNumber = int.Parse(currentBatchName.Replace("Batch_", "")); // Actualizamos el último batch
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

        private void UpdateProgress()
        {
            int totalUpdatesSoFar = 0;
            for (int i = 0; i < stage; i++)
            {
                totalUpdatesSoFar += stageDurations[i];
            }
            totalUpdatesSoFar += stageUpdates[stage];
            int progressPercentage = (int)((totalUpdatesSoFar * 100) / TOTAL_UPDATES);
            UpdateUI(() =>
            {
                progressBar1.Value = Math.Min(progressPercentage, 100);
                progressLabel.Text = $"Progreso: {progressPercentage}%";
            });
        }

        private decimal ProcesarMolienda(decimal cantidadMalta)
        {
            return cantidadMalta;
        }

        private decimal ProcesarCoccion(decimal cantidadAgua)
        {
            return cantidadAgua;
        }

        private decimal ProcesarFermentacion(decimal cantidadLevadura)
        {
            return cantidadLevadura;
        }

        private decimal ProcesarEmbotellado(decimal cantidadBotellas)
        {
            return cantidadBotellas;
        }

        private Tuple<decimal, decimal, decimal, decimal, decimal> CalcularTiemposDeProceso()
        {
            decimal molienda = 1m;  // 1 hora
            decimal coccion = 2m;   // 2 horas
            decimal fermentacion = 96m; // 96 horas
            decimal embotellado = 4m; // 4 horas
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
            // Obtenemos el siguiente número de batch desde la base de datos
            int currentBatchCount = GetCurrentBatchCount();
            int nextBatchNumber = currentBatchCount + 1;
            // Actualizamos el contador en la base de datos solo si es un nuevo inicio o tras "Parar"
            UpdateBatchCounter(nextBatchNumber);
            currentBatchName = "Batch_" + nextBatchNumber.ToString("D4");
            var produccion = new Produccion
            {
                BatchName = currentBatchName,
                Status = "Started",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddHours(103) // Aproximado para 103 horas totales
            };

            _produccionCollection.InsertOne(produccion);
            UpdateUI(() =>
            {
                batchDetails = $"Batch {currentBatchName} Details:\n";
                button1.Text = "En ejecución";
                label7.Text = "0 kg de Malta";
                label7.Location = new Point(250, 339);
                label8.Text = "0 L de Agua";
                label8.Location = new Point(630, 339);
                label9.Text = "0 kg de Levadura";
                label9.Location = new Point(1100, 339);
                label10.Text = "0 botellas";
                label10.Location = new Point(1100, 738);
                // Reiniciamos las etiquetas para el nuevo batch
                moliendaStartLabel.Text = "Inicio: --/--/-- --:--:--";
                moliendaEndLabel.Text = "Fin: --/--/-- --:--:--";
                coccionStartLabel.Text = "Inicio: --/--/-- --:--:--";
                coccionEndLabel.Text = "Fin: --/--/-- --:--:--";
                fermentacionStartLabel.Text = "Inicio: --/--/-- --:--:--";
                fermentacionEndLabel.Text = "Fin: --/--/-- --:--:--";
                embotelladoStartLabel.Text = "Inicio: --/--/-- --:--:--";
                embotelladoEndLabel.Text = "Fin: --/--/-- --:--:--";
                // Eliminamos textBox1.Text = ""; para preservar el historial
                label24.Text = $"Batch: {currentBatchName}";
                label25.Text = $"Batch: {currentBatchName}";
                label26.Text = $"Batch: {currentBatchName}";
                label27.Text = $"Batch: {currentBatchName}";
                progressBar1.Value = 0; // Aseguramos que la barra comience en 0
                progressLabel.Text = $"Progreso: 0%"; // Aseguramos que el texto comience en 0%
            });
            _processTimer.Enabled = true;
            stage = 0;
            batchCount = 0;
            CheckAndNotifyInventory();
        }

        private void UpdateBatchCounter(int newValue)
        {
            var filter = Builders<Counter>.Filter.Eq(c => c.Name, "BatchNumber");
            var update = Builders<Counter>.Update.Set(c => c.Value, newValue);
            var options = new FindOneAndUpdateOptions<Counter> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
            var counter = _counterCollection.FindOneAndUpdate(filter, update, options);

            if (counter == null)
            {
                _counterCollection.InsertOne(new Counter { Name = "BatchNumber", Value = newValue });
            }
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

        private int GetCurrentBatchCount()
        {
            var counter = _counterCollection.Find(Builders<Counter>.Filter.Eq(c => c.Name, "BatchNumber")).FirstOrDefault();
            return counter?.Value ?? 0;
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
                _processTimer.Enabled = true;
            }
            else
            {
                isPaused = true;
                button2.Text = "Continuar";
                _processTimer.Enabled = false;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (isRunning || isPaused)
            {
                // Completar las etapas restantes automáticamente
                while (stage < 4) // 4 etapas: Molienda (0), Cocción (1), Fermentación (2), Embotellado (3)
                {
                    switch (stage)
                    {
                        case 0: // Molienda
                            batchStartTime = DateTime.Now;
                            stageStartTimes[stage] = batchStartTime;
                            cantidadMalta = 200m;
                            decimal molienda = ProcesarMolienda(cantidadMalta);
                            decimal sobranteMalta = almacenMalta - cantidadMalta;
                            almacenMalta = sobranteMalta;
                            DateTime moliendaEnd = batchStartTime.AddHours(1); // 1 hora
                            UpdateUI(() =>
                            {
                                label7.Text = $"{molienda:F2} kg";
                                moliendaStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                moliendaEndLabel.Text = $"Fin: {moliendaEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                            });
                            break;
                        case 1: // Cocción
                            stageStartTimes[stage] = batchStartTime.AddHours(1); // Después de Molienda
                            cantidadAgua = 1200m;
                            decimal coccion = ProcesarCoccion(cantidadAgua);
                            decimal sobranteAgua = almacenAgua - cantidadAgua;
                            almacenAgua = sobranteAgua;
                            DateTime coccionEnd = stageStartTimes[stage].AddHours(2); // 2 horas
                            UpdateUI(() =>
                            {
                                label8.Text = $"{coccion:F2} L";
                                coccionStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                coccionEndLabel.Text = $"Fin: {coccionEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                            });
                            break;
                        case 2: // Fermentación
                            stageStartTimes[stage] = batchStartTime.AddHours(3); // Después de Cocción
                            cantidadLevadura = 0.75m;
                            decimal fermentacion = ProcesarFermentacion(cantidadLevadura);
                            decimal sobranteLevadura = almacenLevadura - cantidadLevadura;
                            almacenLevadura = sobranteLevadura;
                            DateTime fermentacionEnd = stageStartTimes[stage].AddHours(96); // 96 horas
                            UpdateUI(() =>
                            {
                                label9.Text = $"{fermentacion:F2} kg";
                                fermentacionStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                fermentacionEndLabel.Text = $"Fin: {fermentacionEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                            });
                            break;
                        case 3: // Embotellado
                            stageStartTimes[stage] = batchStartTime.AddHours(99); // Después de Fermentación
                            cantidadBotellas = 1000m;
                            decimal embotellado = ProcesarEmbotellado(cantidadBotellas);
                            decimal sobranteBotellas = almacenBotellas - cantidadBotellas;
                            almacenBotellas = sobranteBotellas;
                            DateTime embotelladoEnd = stageStartTimes[stage].AddHours(4); // 4 horas
                            UpdateUI(() =>
                            {
                                label10.Text = $"{Math.Round(embotellado):F0} botellas";
                                embotelladoStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                embotelladoEndLabel.Text = $"Fin: {embotelladoEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                                if (textBox1 != null)
                                    textBox1.Text += $"{currentBatchName}: {Math.Round(embotellado):F0} botellas\r\n"; // Usamos += para acumular
                            });
                            // Guardar el batch aquí para evitar duplicación en OnDelayTimerTick
                            GuardarBotellasLlenas(currentBatchName, Math.Round(embotellado), DateTime.Now);
                            break;
                    }
                    stage++; // Avanzar a la siguiente etapa
                }

                // Actualizar la interfaz con el progreso completo y reiniciar
                UpdateUI(() =>
                {
                    label15.Text = $"Malta de cebada: {almacenMalta:F3} kg";
                    label16.Text = $"Agua: {almacenAgua:F3} L";
                    label17.Text = $"Levadura: {almacenLevadura:F3} kg";
                    label18.Text = $"Botellas: {almacenBotellas:F0}";
                    progressBar1.Value = 100;
                    progressLabel.Text = $"Progreso: 100%"; // Aseguramos que el texto sea 100% al completar
                });

                // Detener el proceso completamente sin activar el delay timer
                isRunning = false;
                isPaused = false;
                UpdateUI(() =>
                {
                    button1.Text = "Empezar"; // Cambiamos el botón a "Empezar"
                });

                // Guardar el fin del lote en la base de datos
                UpdateBatchEndTime(DateTime.Now);

                // Mostrar mensaje de finalización
                MessageBox.Show($"El batch {currentBatchName} se ha completado exitosamente.", "Proceso Finalizado", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            // Lógica para el evento del label
        }

        private void pictureBox5_Click(object sender, EventArgs e)
        {
            // Lógica para el evento del picture box
        }

        private void label11_Click(object sender, EventArgs e)
        {
            // Lógica para el evento del label
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var dateTimeTimer = new System.Windows.Forms.Timer();
            dateTimeTimer.Interval = 1000;
            dateTimeTimer.Tick += UpdateDateTimeLabel;
            dateTimeTimer.Start();
            CheckAndNotifyInventory();
            UpdateUI(() =>
            {
                label15.Text = $"Malta de cebada: {almacenMalta:F3} kg";
                label16.Text = $"Agua: {almacenAgua:F3} L";
                label17.Text = $"Levadura: {almacenLevadura:F3} kg";
                label18.Text = $"Botellas: {almacenBotellas:F0}";
                progressBar1.Value = 0; // Aseguramos que la barra comience en 0 al cargar
                progressLabel.Text = $"Progreso: 0%"; // Aseguramos que el texto comience en 0%
            });
            InitializeInventory();
        }

        private void UpdateDateTimeLabel(object sender, EventArgs e)
        {
            dateTimeLabel.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        }

        private void dateTimeLabel_Click(object sender, EventArgs e)
        {
            // Lógica para el evento del dateTimeLabel
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
                        label15.ForeColor = Color.White;
                        label16.ForeColor = Color.White;
                        label17.ForeColor = Color.White;
                        label18.ForeColor = Color.White;
                    }
                }
                inventoryStatusLabel.Text = statusMessage;
                inventoryStatusLabel.ForeColor = hasLowInventory ? Color.Red : Color.White;
            });
        }

        private void InitializeInventory()
        {
            var ingredientes = _inventoryManager.GetAllIngredientes();
            if (!ingredientes.Exists(i => i.Nombre.ToLower() == "malta"))
                _inventoryManager.AddIngrediente(new Ingrediente { Nombre = "Malta", Cantidad = 2000m, UmbralMinimo = 200m });
            if (!ingredientes.Exists(i => i.Nombre.ToLower() == "agua"))
                _inventoryManager.AddIngrediente(new Ingrediente { Nombre = "Agua", Cantidad = 12000m, UmbralMinimo = 1200m });
            if (!ingredientes.Exists(i => i.Nombre.ToLower() == "levadura"))
                _inventoryManager.AddIngrediente(new Ingrediente { Nombre = "Levadura", Cantidad = 7.5m, UmbralMinimo = 0.75m });
            if (!ingredientes.Exists(i => i.Nombre.ToLower() == "botellas"))
                _inventoryManager.AddIngrediente(new Ingrediente { Nombre = "Botellas", Cantidad = 10000m, UmbralMinimo = 1000m });
        }

        private void restockButton_Click_2(object sender, EventArgs e)
        {
            UpdateUI(() =>
            {
                if (textBox1 != null)
                    textBox1.Text = "";
            });
            var filter = Builders<BsonDocument>.Filter.Empty;
            _batchesBotellasCollection.DeleteMany(filter);
            MessageBox.Show("Almacén de botellas llenas reiniciado exitosamente.");
        }

        private void UpdateBatchEndTime(DateTime endTime)
        {
            var filter = Builders<Produccion>.Filter.Eq(p => p.BatchName, currentBatchName);
            var update = Builders<Produccion>.Update.Set(p => p.EndDate, endTime);
            _produccionCollection.UpdateOne(filter, update);
        }

        private void GuardarBotellasLlenas(string batchName, decimal cantidad, DateTime fechaProduccion)
        {
            if (string.IsNullOrEmpty(batchName))
                batchName = "Batch_Unknown";
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