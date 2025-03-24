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
        private System.Windows.Forms.Timer _progressTimer; // Timer para actualizar el progreso de forma suave
        private Random _random;
        private bool isRunning;
        private bool isPaused;
        private bool isFirstBatch; // Nueva variable para controlar si es el primer batch
        private string batchDetails;
        private decimal cantidadMalta;
        private decimal cantidadAgua;
        private decimal cantidadLevadura;
        private decimal cantidadBotellas;
        private decimal cantidadLupulo; // Nueva variable para el Lúpulo
        private int stage;
        private DateTime batchStartTime;
        private DateTime lastBatchEndTime; // Almacena la fecha de fin del último batch
        private DateTime[] stageStartTimes;
        private DateTime[] stageEndTimes; // Almacena las fechas de fin de cada etapa
        private DateTime pauseStartTime; // Para rastrear el inicio de una pausa
        private double pausedTime; // Tiempo acumulado en pausa (en segundos)
        private string currentBatchName;
        private int batchCount;
        private const int TOTAL_BATCHES = 10;
        private const double MERMA_FACTOR = 0.9; // Factor de merma: 10% de pérdida
        private const double TOTAL_BATCH_DURATION_SECONDS = 78.5; // Duración total de un batch en segundos (67.5s de etapas + 10s de retraso + 1s de pausa)

        // Tiempos en horas para cada etapa
        private readonly int[] stageDurations = new int[] { 1, 2, 96, 4 }; // Molienda: 1h, Cocción: 2h, Fermentación: 96h, Embotellado: 4h
        private int[] stageUpdates; // Contador de actualizaciones por etapa
        private int lastBatchNumber; // Para rastrear el último batch procesado

        private decimal almacenMalta = 2000m;
        private decimal almacenAgua = 12000m;
        private decimal almacenLevadura = 7.5m;
        private decimal almacenBotellas = 10000m;
        private decimal almacenLupulo = 30m; // Nueva variable para el inventario de Lúpulo

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
            isFirstBatch = true; // Inicializamos como true para el primer batch
            batchDetails = string.Empty;
            stage = 0;
            batchStartTime = DateTime.MinValue; // Inicializamos como vacío
            lastBatchEndTime = DateTime.MinValue; // Inicializamos como vacío
            pauseStartTime = DateTime.MinValue; // Inicializamos
            pausedTime = 0; // Inicializamos el tiempo pausado
            stageStartTimes = new DateTime[4];
            stageEndTimes = new DateTime[4]; // Inicializamos el arreglo para las fechas de fin
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

            // Timer para actualizar el progreso de forma suave (cada 50 ms para mayor precisión)
            _progressTimer = new System.Windows.Forms.Timer();
            _progressTimer.Interval = 50; // Actualiza cada 50 ms
            _progressTimer.Tick += OnProgressTimerTick;
            _progressTimer.Enabled = false;
        }

        private void OnTimedEvent(object sender, EventArgs e)
        {
            if (!isRunning || isPaused) return;

            // Precálculo de todas las fechas al inicio del batch
            if (stage == 0 && stageUpdates[stage] == 0)
            {
                stageStartTimes[0] = batchStartTime;

                // Precalculamos todas las fechas de inicio y fin de cada etapa
                for (int i = 1; i < stageStartTimes.Length; i++)
                {
                    int horasAcumuladas = 0;
                    for (int j = 0; j < i; j++)
                    {
                        horasAcumuladas += stageDurations[j];
                    }
                    stageStartTimes[i] = batchStartTime.AddHours(horasAcumuladas);
                    stageEndTimes[i] = stageStartTimes[i].AddHours(stageDurations[i]);
                }

                UpdateUI(() =>
                {
                    label24.Text = $"Batch: {currentBatchName}";
                    label25.Text = $"Batch: {currentBatchName}";
                    label26.Text = $"Batch: {currentBatchName}";
                    label27.Text = $"Batch: {currentBatchName}";
                    // Solo reiniciamos las etiquetas de las etapas no completadas
                    if (moliendaStartLabel.Text == "Inicio: --/--/-- --:--:--")
                        moliendaStartLabel.Text = "Inicio: --/--/-- --:--:--";
                    if (moliendaEndLabel.Text == "Fin: --/--/-- --:--:--")
                        moliendaEndLabel.Text = "Fin: --/--/-- --:--:--";
                    if (coccionStartLabel.Text == "Inicio: --/--/-- --:--:--")
                        coccionStartLabel.Text = "Inicio: --/--/-- --:--:--";
                    if (coccionEndLabel.Text == "Fin: --/--/-- --:--:--")
                        coccionEndLabel.Text = "Fin: --/--/-- --:--:--";
                    if (fermentacionStartLabel.Text == "Inicio: --/--/-- --:--:--")
                        fermentacionStartLabel.Text = "Inicio: --/--/-- --:--:--";
                    if (fermentacionEndLabel.Text == "Fin: --/--/-- --:--:--")
                        fermentacionEndLabel.Text = "Fin: --/--/-- --:--:--";
                    if (embotelladoStartLabel.Text == "Inicio: --/--/-- --:--:--")
                        embotelladoStartLabel.Text = "Inicio: --/--/-- --:--:--";
                    if (embotelladoEndLabel.Text == "Fin: --/--/-- --:--:--")
                        embotelladoEndLabel.Text = "Fin: --/--/-- --:--:--";
                    progressBar1.Value = 0;
                    progressLabel.Text = $"Progreso: 0%";
                    label33.Text = "0.000 kg de Lúpulo"; // Reiniciamos la etiqueta de Lúpulo consumido con formato F3
                });
                Array.Clear(stageUpdates, 0, stageUpdates.Length); // Reiniciamos contadores de actualizaciones
                _progressTimer.Enabled = true; // Activamos el timer de progreso
            }

            // Avanzamos según el número de actualizaciones por etapa
            int updatesNeeded = stage == 2 ? 2 : stageDurations[stage]; // Fermentación solo necesita 2 actualizaciones
            if (stageUpdates[stage] < updatesNeeded)
            {
                stageUpdates[stage]++;
            }

            // Calculamos los tiempos y actualizamos las etiquetas solo al completar cada etapa
            switch (stage)
            {
                case 0: // Molienda (1 hora = 1 actualización)
                    if (stageUpdates[stage] == 1)
                    {
                        batchStartTime = stageStartTimes[stage]; // Usamos la fecha precalculada
                        cantidadMalta = 200m;
                        decimal molienda = ProcesarMolienda(cantidadMalta);
                        decimal sobranteMalta = almacenMalta - cantidadMalta;
                        almacenMalta = sobranteMalta;
                        batchDetails = FormatearDetallesProceso($"Molienda: {cantidadMalta:F3} kg de Malta consumidos, {molienda:F3} kg procesados (merma 10%)", $"Sobrante Malta: {sobranteMalta:F3} kg");
                        DateTime moliendaEnd = batchStartTime.AddHours(1); // 1 hora = 7.5 segundos
                        stageEndTimes[0] = moliendaEnd; // Guardamos la fecha de fin
                        UpdateUI(() =>
                        {
                            label7.Text = $"{molienda:F3} kg de Malta";
                            label7.Location = new Point(250, 339);
                            label15.Text = $"Malta de cebada: {almacenMalta:F3} kg";
                            if (moliendaStartLabel != null)
                            {
                                moliendaStartLabel.Text = $"Inicio: {batchStartTime.ToString("dd/MM/yyyy HH:mm:ss")}";
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
                        stageStartTimes[stage] = batchStartTime.AddHours(1); // Continuamos desde el fin de Molienda
                        cantidadAgua = 1200m;
                        cantidadLupulo = 3m; // Consumimos 3 kg de Lúpulo por batch
                        decimal coccion = ProcesarCoccion(cantidadAgua);
                        decimal sobranteAgua = almacenAgua - cantidadAgua;
                        decimal sobranteLupulo = almacenLupulo - cantidadLupulo;
                        almacenAgua = sobranteAgua;
                        almacenLupulo = sobranteLupulo;
                        batchDetails = FormatearDetallesProceso(batchDetails, $"Cocción: {cantidadAgua:F3} L de Agua consumidos, {coccion:F3} L procesados (merma 10%)", $"Sobrante Agua: {sobranteAgua:F3} L", $"Lúpulo consumido: {cantidadLupulo:F3} kg (merma 10%)", $"Sobrante Lúpulo: {sobranteLupulo:F3} kg");
                        UpdateUI(() =>
                        {
                            label8.Text = $"{coccion:F3} L de Agua";
                            label8.Location = new Point(630, 339);
                            label16.Text = $"Agua: {almacenAgua:F3} L";
                            label33.Text = $"{cantidadLupulo * (decimal)MERMA_FACTOR:F3} kg de Lúpulo"; // Mostramos Lúpulo consumido con merma
                            label32.Text = $"Lúpulo: {almacenLupulo:F3} kg"; // Actualizamos inventario de Lúpulo
                            if (coccionStartLabel != null)
                            {
                                coccionStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                Console.WriteLine($"Cocción Start set to: {coccionStartLabel.Text}");
                            }
                        });
                    }
                    if (stageUpdates[stage] == 2)
                    {
                        DateTime coccionEnd = stageStartTimes[stage].AddHours(2); // 2 horas = 15 segundos
                        stageEndTimes[1] = coccionEnd; // Guardamos la fecha de fin
                        UpdateUI(() =>
                        {
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

                case 2: // Fermentación (96 horas = 2 actualizaciones)
                    if (stageUpdates[stage] == 1)
                    {
                        stageStartTimes[stage] = batchStartTime.AddHours(3); // Continuamos desde el fin de Cocción
                        cantidadLevadura = 0.75m;
                        decimal fermentacion = ProcesarFermentacion(cantidadLevadura);
                        decimal sobranteLevadura = almacenLevadura - cantidadLevadura;
                        almacenLevadura = sobranteLevadura;
                        batchDetails = FormatearDetallesProceso(batchDetails, $"Fermentación: {cantidadLevadura:F3} kg de Levadura consumidos, {fermentacion:F3} kg procesados (merma 10%)", $"Sobrante Levadura: {sobranteLevadura:F3} kg");
                        UpdateUI(() =>
                        {
                            label9.Text = $"{fermentacion:F3} kg de Levadura";
                            label9.Location = new Point(1100, 339);
                            label17.Text = $"Levadura: {almacenLevadura:F3} kg";
                            if (fermentacionStartLabel != null)
                            {
                                fermentacionStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                Console.WriteLine($"Fermentación Start set to: {fermentacionStartLabel.Text}");
                            }
                        });
                    }
                    if (stageUpdates[stage] == 2)
                    {
                        DateTime fermentacionEnd = stageStartTimes[stage].AddHours(96); // 96 horas totales simuladas
                        stageEndTimes[2] = fermentacionEnd; // Guardamos la fecha de fin
                        UpdateUI(() =>
                        {
                            if (fermentacionEndLabel != null)
                            {
                                fermentacionEndLabel.Text = $"Fin: {fermentacionEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                                Console.WriteLine($"Fermentación End set to: {fermentacionEndLabel.Text}");
                            }
                            fermentacionStartLabel?.Refresh();
                            fermentacionEndLabel?.Refresh();
                        });
                        stage++;
                    }
                    break;

                case 3: // Embotellado (4 horas = 4 actualizaciones)
                    if (stageUpdates[stage] == 1)
                    {
                        stageStartTimes[stage] = batchStartTime.AddHours(99); // Continuamos desde el fin de Fermentación
                        cantidadBotellas = 1000m;
                        decimal embotellado = ProcesarEmbotellado(cantidadBotellas);
                        decimal sobranteBotellas = almacenBotellas - embotellado; // Restamos las botellas realmente utilizadas (con merma)
                        almacenBotellas = sobranteBotellas;
                        batchDetails = FormatearDetallesProceso(batchDetails, $"Embotellado: {cantidadBotellas:F0} botellas previstas, {Math.Round(embotellado):F0} botellas procesadas (merma 10%)", $"Sobrante Botellas: {sobranteBotellas:F0} botellas");
                        UpdateUI(() =>
                        {
                            label10.Text = $"{Math.Round(embotellado):F0} botellas";
                            label10.Location = new Point(1100, 758);
                            label18.Text = $"Botellas: {almacenBotellas:F0}";
                            if (embotelladoStartLabel != null)
                            {
                                embotelladoStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                Console.WriteLine($"Embotellado Start set to: {embotelladoStartLabel.Text}");
                            }
                        });
                    }
                    if (stageUpdates[stage] == 4)
                    {
                        DateTime embotelladoEnd = stageStartTimes[stage].AddHours(4); // 4 horas = 30 segundos
                        stageEndTimes[3] = embotelladoEnd; // Guardamos la fecha de fin
                        UpdateUI(() =>
                        {
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
                        // NO desactivamos _progressTimer aquí para que el progreso siga subiendo durante el retraso
                    }
                    break;
            }
        }

        private void OnProgressTimerTick(object sender, EventArgs e)
        {
            UpdateProgress();
        }

        private void OnDelayTimerTick(object sender, EventArgs e)
        {
            Console.WriteLine("OnDelayTimerTick: Iniciando...");
            _delayTimer.Enabled = false;

            lastBatchEndTime = stageEndTimes[3]; // Guardamos la fecha de fin del batch actual

            // Guardamos las botellas en la base de datos y actualizamos textBox1 ANTES de iniciar un nuevo batch
            if (isRunning)
            {
                Console.WriteLine($"OnDelayTimerTick: Guardando botellas para {currentBatchName}...");
                GuardarBotellasLlenas(currentBatchName, Math.Round(cantidadBotellas * (decimal)MERMA_FACTOR), lastBatchEndTime);
                lastBatchNumber = int.Parse(currentBatchName.Replace("Batch_", ""));
            }

            UpdateUI(() =>
            {
                if (textBox1 != null)
                {
                    Console.WriteLine($"OnDelayTimerTick: Agregando a textBox1: {currentBatchName}: (Lager) {Math.Round(cantidadBotellas * (decimal)MERMA_FACTOR):F0} botellas ➞ 1L");
                    textBox1.Text += $"{currentBatchName}: (Lager) {Math.Round(cantidadBotellas * (decimal)MERMA_FACTOR):F0} botellas ➞ 1L\r\n";
                    textBox1.Refresh(); // Forzamos la actualización de textBox1
                }
                // Aseguramos que la barra de progreso llegue al 100% al final del batch
                progressBar1.Value = 100;
                progressLabel.Text = "Progreso: 100%";
            });

            // Desactivamos el timer de progreso ahora que el batch ha terminado
            _progressTimer.Enabled = false;
            Console.WriteLine("OnDelayTimerTick: _progressTimer desactivado.");

            batchCount++;
            if (batchCount < TOTAL_BATCHES)
            {
                Console.WriteLine("OnDelayTimerTick: Iniciando un nuevo batch...");
                // Generamos un nuevo nombre de batch y lo registramos en la base de datos
                int currentBatchCount = GetCurrentBatchCount();
                int nextBatchNumber = currentBatchCount + 1;
                UpdateBatchCounter(nextBatchNumber);
                currentBatchName = "Batch_" + nextBatchNumber.ToString("D4");

                // Registramos el nuevo batch en la base de datos
                var produccion = new Produccion
                {
                    BatchName = currentBatchName,
                    Status = "Started",
                    StartDate = lastBatchEndTime,
                    EndDate = lastBatchEndTime.AddHours(103) // Aproximado para 103 horas totales
                };
                _produccionCollection.InsertOne(produccion);

                // Reiniciamos para el próximo batch usando la fecha de fin del anterior
                stage = 0;
                Array.Clear(stageUpdates, 0, stageUpdates.Length);

                // Usamos la fecha de fin del batch anterior como inicio del siguiente
                batchStartTime = DateTime.Now; // Usamos DateTime.Now para un nuevo inicio preciso
                stageStartTimes[0] = batchStartTime;

                // Precalculamos todas las fechas para el nuevo batch
                for (int i = 1; i < stageStartTimes.Length; i++)
                {
                    int horasAcumuladas = 0;
                    for (int j = 0; j < i; j++)
                    {
                        horasAcumuladas += stageDurations[j];
                    }
                    stageStartTimes[i] = batchStartTime.AddHours(horasAcumuladas);
                    stageEndTimes[i] = stageStartTimes[i].AddHours(stageDurations[i]);
                }

                UpdateUI(() =>
                {
                    Console.WriteLine("OnDelayTimerTick: Reiniciando etiquetas para el nuevo batch...");
                    // Reinicio de etiquetas y valores visuales
                    label7.Text = "0.000 kg de Malta";
                    label8.Text = "0.000 L de Agua";
                    label9.Text = "0.000 kg de Levadura";
                    label10.Text = "0 botellas";
                    label33.Text = "0.000 kg de Lúpulo"; // Reiniciamos la etiqueta de Lúpulo consumido
                    progressBar1.Value = 0; // Reiniciamos la barra de progreso
                    progressLabel.Text = "Progreso: 0%";

                    // No tocamos las etiquetas de las etapas ya completadas
                    if (stage < 1)
                    {
                        moliendaStartLabel.Text = "Inicio: --/--/-- --:--:--";
                        moliendaEndLabel.Text = "Fin: --/--/-- --:--:--";
                    }
                    if (stage < 2)
                    {
                        coccionStartLabel.Text = "Inicio: --/--/-- --:--:--";
                        coccionEndLabel.Text = "Fin: --/--/-- --:--:--";
                    }
                    if (stage < 3)
                    {
                        fermentacionStartLabel.Text = "Inicio: --/--/-- --:--:--";
                        fermentacionEndLabel.Text = "Fin: --/--/-- --:--:--";
                    }
                    if (stage < 4)
                    {
                        embotelladoStartLabel.Text = "Inicio: --/--/-- --:--:--";
                        embotelladoEndLabel.Text = "Fin: --/--/-- --:--:--";
                    }

                    // Actualizamos las etiquetas que muestran el nombre del batch
                    label24.Text = $"Batch: {currentBatchName}";
                    label25.Text = $"Batch: {currentBatchName}";
                    label26.Text = $"Batch: {currentBatchName}";
                    label27.Text = $"Batch: {currentBatchName}";
                    label32.Text = $"Lúpulo: {almacenLupulo:F3} kg"; // Actualizamos inventario de Lúpulo
                });

                Thread.Sleep(1000);
                _processTimer.Enabled = true;
                _progressTimer.Enabled = true; // Reactivamos el timer de progreso
                Console.WriteLine("OnDelayTimerTick: _progressTimer reactivado para el nuevo batch.");
            }
            else
            {
                Console.WriteLine("OnDelayTimerTick: Proceso completado (10 batches).");
                isRunning = false;
                UpdateUI(() =>
                {
                    button1.Text = "Empezar";
                    button2.Text = "Pausar";
                    progressLabel.Text = "Progreso: 100% (10,000 botellas completadas)";
                    progressBar1.Value = 100; // Aseguramos que la barra llegue al 100%
                });
                batchCount = 0;
                isFirstBatch = true; // Reiniciamos para que el próximo ciclo comience desde DateTime.Now
                batchStartTime = DateTime.MinValue; // Reiniciamos batchStartTime para el próximo ciclo
                lastBatchEndTime = DateTime.MinValue; // Reiniciamos lastBatchEndTime
                pausedTime = 0; // Reiniciamos el tiempo pausado
            }

            CheckAndNotifyInventory();
            Console.WriteLine("OnDelayTimerTick: Finalizado.");
        }

        private void StartNextBatch()
        {
            UpdateUI(() =>
            {
                button1.Text = "Empezar";
                label7.Text = "0.000 kg de Malta";
                label7.Location = new Point(250, 339);
                label8.Text = "0.000 L de Agua";
                label8.Location = new Point(630, 339);
                label9.Text = "0.000 kg de Levadura";
                label9.Location = new Point(1100, 339);
                label10.Text = "0 botellas";
                label10.Location = new Point(1100, 758);
                label33.Text = "0.000 kg de Lúpulo"; // Reiniciamos la etiqueta de Lúpulo consumido
                progressBar1.Value = 0; // Reiniciamos la barra al 0% al iniciar un nuevo batch
                progressLabel.Text = $"Progreso: 0%";
                label24.Text = $"Batch: {currentBatchName}";
                label25.Text = $"Batch: {currentBatchName}";
                label26.Text = $"Batch: {currentBatchName}";
                label27.Text = $"Batch: {currentBatchName}";
                label32.Text = $"Lúpulo: {almacenLupulo:F3} kg";
                // No reiniciamos las etiquetas de las etapas ya completadas
                if (stage < 1)
                {
                    moliendaStartLabel.Text = "Inicio: --/--/-- --:--:--";
                    moliendaEndLabel.Text = "Fin: --/--/-- --:--:--";
                }
                if (stage < 2)
                {
                    coccionStartLabel.Text = "Inicio: --/--/-- --:--:--";
                    coccionEndLabel.Text = "Fin: --/--/-- --:--:--";
                }
                if (stage < 3)
                {
                    fermentacionStartLabel.Text = "Inicio: --/--/-- --:--:--";
                    fermentacionEndLabel.Text = "Fin: --/--/-- --:--:--";
                }
                if (stage < 4)
                {
                    embotelladoStartLabel.Text = "Inicio: --/--/-- --:--:--";
                    embotelladoEndLabel.Text = "Fin: --/--/-- --:--:--";
                }
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
            _progressTimer.Enabled = true; // Activamos el timer de progreso
            Console.WriteLine("StartNextBatch: _progressTimer activado.");
            isRunning = true;
            stage = 0;
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
            if (batchStartTime == DateTime.MinValue || !isRunning)
            {
                Console.WriteLine("UpdateProgress: Saliendo porque batchStartTime no está inicializado o no está en ejecución.");
                return; // No actualizamos el progreso si el batch no ha comenzado o no está en ejecución
            }

            // Calculamos el tiempo transcurrido desde el inicio del batch (en segundos)
            double elapsedSeconds = (DateTime.Now - batchStartTime).TotalSeconds;

            // Restamos el tiempo acumulado en pausa
            elapsedSeconds -= pausedTime;

            // Calculamos el porcentaje de progreso basado en el tiempo transcurrido
            double progressPercentage = (elapsedSeconds / TOTAL_BATCH_DURATION_SECONDS) * 100;

            // Aseguramos que el porcentaje esté entre 0 y 100
            progressPercentage = Math.Min(progressPercentage, 100);
            progressPercentage = Math.Max(progressPercentage, 0);

            UpdateUI(() =>
            {
                progressBar1.Value = (int)progressPercentage;
                progressLabel.Text = $"Progreso: {(int)progressPercentage}%";
            });

            Console.WriteLine($"UpdateProgress: elapsedSeconds={elapsedSeconds:F2}, pausedTime={pausedTime:F2}, progressPercentage={progressPercentage:F2}%");
        }

        private decimal ProcesarMolienda(decimal cantidadMalta)
        {
            return cantidadMalta * (decimal)MERMA_FACTOR; // Aplicamos merma del 10%
        }

        private decimal ProcesarCoccion(decimal cantidadAgua)
        {
            return cantidadAgua * (decimal)MERMA_FACTOR; // Aplicamos merma del 10%
        }

        private decimal ProcesarFermentacion(decimal cantidadLevadura)
        {
            return cantidadLevadura * (decimal)MERMA_FACTOR; // Aplicamos merma del 10%
        }

        private decimal ProcesarEmbotellado(decimal cantidadBotellas)
        {
            return cantidadBotellas * (decimal)MERMA_FACTOR; // Aplicamos merma del 10%
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
                MessageBox.Show("Un lote está ya en ejecución.");
                return;
            }

            isRunning = true;
            isPaused = false; // Aseguramos que no esté pausado
            pausedTime = 0; // Reiniciamos el tiempo pausado

            // Establecemos la hora inicial solo si es el primer batch
            if (isFirstBatch)
            {
                batchStartTime = DateTime.Now;
                isFirstBatch = false; // Marcamos que ya no es el primer batch
            }
            else
            {
                batchStartTime = DateTime.Now; // Usamos DateTime.Now para un nuevo inicio preciso
            }

            // Obtenemos el siguiente número de batch desde la base de datos
            int currentBatchCount = GetCurrentBatchCount();
            int nextBatchNumber = currentBatchCount + 1;
            UpdateBatchCounter(nextBatchNumber);
            currentBatchName = "Batch_" + nextBatchNumber.ToString("D4");

            var produccion = new Produccion
            {
                BatchName = currentBatchName,
                Status = "Started",
                StartDate = batchStartTime,
                EndDate = batchStartTime.AddHours(103) // Aproximado para 103 horas totales
            };

            _produccionCollection.InsertOne(produccion);
            StartNextBatch();
            UpdateUI(() =>
            {
                batchDetails = $"Batch {currentBatchName} Details:\n";
                button1.Text = "En ejecución";
                label7.Text = "0.000 kg de Malta";
                label7.Location = new Point(250, 339);
                label8.Text = "0.000 L de Agua";
                label8.Location = new Point(630, 339);
                label9.Text = "0.000 kg de Levadura";
                label9.Location = new Point(1100, 339);
                label10.Text = "0 botellas";
                label10.Location = new Point(1100, 758);
                label33.Text = "0.000 kg de Lúpulo"; // Reiniciamos la etiqueta de Lúpulo consumido
                label24.Text = $"Batch: {currentBatchName}";
                label25.Text = $"Batch: {currentBatchName}";
                label26.Text = $"Batch: {currentBatchName}";
                label27.Text = $"Batch: {currentBatchName}";
                progressBar1.Value = 0;
                progressLabel.Text = $"Progreso: 0%";
            });
            _processTimer.Enabled = true;
            stage = 0;
            CheckAndNotifyInventory();
            Console.WriteLine($"button1_Click: batchStartTime={batchStartTime}, isRunning={isRunning}, _progressTimer.Enabled={_progressTimer.Enabled}");
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
                _progressTimer.Enabled = true;
                // Calculamos el tiempo que estuvo pausado y lo añadimos a pausedTime
                if (pauseStartTime != DateTime.MinValue)
                {
                    pausedTime += (DateTime.Now - pauseStartTime).TotalSeconds;
                    pauseStartTime = DateTime.MinValue;
                }
                Console.WriteLine($"button2_Click: Reanudando, pausedTime={pausedTime:F2}, _progressTimer.Enabled={_progressTimer.Enabled}");
            }
            else
            {
                isPaused = true;
                button2.Text = "Continuar";
                _processTimer.Enabled = false;
                _progressTimer.Enabled = false;
                pauseStartTime = DateTime.Now; // Registramos el inicio de la pausa
                Console.WriteLine($"button2_Click: Pausando, pauseStartTime={pauseStartTime}, _progressTimer.Enabled={_progressTimer.Enabled}");
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (isRunning || isPaused)
            {
                // Completar las etapas restantes automáticamente y consumir todas las materias primas como un batch normal
                while (stage < 4) // 4 etapas: Molienda (0), Cocción (1), Fermentación (2), Embotellado (3)
                {
                    switch (stage)
                    {
                        case 0: // Molienda
                            stageStartTimes[stage] = batchStartTime;
                            cantidadMalta = 200m; // Cantidad fija por batch
                            decimal molienda = ProcesarMolienda(cantidadMalta);
                            decimal sobranteMalta = almacenMalta - cantidadMalta;
                            almacenMalta = sobranteMalta;
                            DateTime moliendaEnd = stageStartTimes[stage].AddHours(1); // 1 hora
                            stageEndTimes[0] = moliendaEnd; // Guardamos la fecha de fin
                            UpdateUI(() =>
                            {
                                label7.Text = $"{molienda:F3} kg de Malta";
                                moliendaStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                moliendaEndLabel.Text = $"Fin: {moliendaEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                            });
                            break;
                        case 1: // Cocción
                            stageStartTimes[stage] = batchStartTime.AddHours(1); // Después de Molienda
                            cantidadAgua = 1200m; // Cantidad fija por batch
                            cantidadLupulo = 3m; // Consumimos 3 kg de Lúpulo por batch
                            decimal coccion = ProcesarCoccion(cantidadAgua);
                            decimal sobranteAgua = almacenAgua - cantidadAgua;
                            decimal sobranteLupulo = almacenLupulo - cantidadLupulo;
                            almacenAgua = sobranteAgua;
                            almacenLupulo = sobranteLupulo;
                            DateTime coccionEnd = stageStartTimes[stage].AddHours(2); // 2 horas
                            stageEndTimes[1] = coccionEnd; // Guardamos la fecha de fin
                            UpdateUI(() =>
                            {
                                label8.Text = $"{coccion:F3} L de Agua";
                                label33.Text = $"{cantidadLupulo * (decimal)MERMA_FACTOR:F3} kg de Lúpulo"; // Mostramos Lúpulo consumido con merma
                                label32.Text = $"Lúpulo: {almacenLupulo:F3} kg"; // Actualizamos inventario de Lúpulo
                                coccionStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                coccionEndLabel.Text = $"Fin: {coccionEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                            });
                            break;
                        case 2: // Fermentación
                            stageStartTimes[stage] = batchStartTime.AddHours(3); // Después de Cocción
                            cantidadLevadura = 0.75m; // Cantidad fija por batch
                            decimal fermentacion = ProcesarFermentacion(cantidadLevadura);
                            decimal sobranteLevadura = almacenLevadura - cantidadLevadura;
                            almacenLevadura = sobranteLevadura;
                            DateTime fermentacionEnd = stageStartTimes[stage].AddHours(96); // 96 horas
                            stageEndTimes[2] = fermentacionEnd; // Guardamos la fecha de fin
                            UpdateUI(() =>
                            {
                                label9.Text = $"{fermentacion:F3} kg de Levadura";
                                fermentacionStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                fermentacionEndLabel.Text = $"Fin: {fermentacionEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                            });
                            break;
                        case 3: // Embotellado
                            stageStartTimes[stage] = batchStartTime.AddHours(99); // Después de Fermentación
                            cantidadBotellas = 1000m; // Cantidad fija por batch
                            decimal embotellado = ProcesarEmbotellado(cantidadBotellas);
                            decimal sobranteBotellas = almacenBotellas - embotellado; // Restamos las botellas realmente utilizadas (con merma)
                            almacenBotellas = sobranteBotellas;
                            DateTime embotelladoEnd = stageStartTimes[stage].AddHours(4); // 4 horas
                            stageEndTimes[3] = embotelladoEnd; // Guardamos la fecha de fin
                            UpdateUI(() =>
                            {
                                label10.Text = $"{Math.Round(embotellado):F0} botellas";
                                embotelladoStartLabel.Text = $"Inicio: {stageStartTimes[stage].ToString("dd/MM/yyyy HH:mm:ss")}";
                                embotelladoEndLabel.Text = $"Fin: {embotelladoEnd.ToString("dd/MM/yyyy HH:mm:ss")}";
                                if (textBox1 != null)
                                    textBox1.Text += $"{currentBatchName}: (Lager) {Math.Round(embotellado):F0} botellas ➞ 1L\r\n";
                            });
                            GuardarBotellasLlenas(currentBatchName, Math.Round(embotellado), embotelladoEnd);
                            break;
                    }
                    stage++; // Avanzar a la siguiente etapa
                }

                // Guardamos la fecha de fin del batch para usarla en el siguiente
                lastBatchEndTime = stageEndTimes[3]; // Usamos la fecha de fin del embotellado almacenada
                batchStartTime = lastBatchEndTime; // Preparamos el siguiente batch

                // Actualizar la interfaz con el inventario final después de completar todas las etapas
                UpdateUI(() =>
                {
                    label15.Text = $"Malta de cebada: {almacenMalta:F3} kg";
                    label16.Text = $"Agua: {almacenAgua:F3} L";
                    label17.Text = $"Levadura: {almacenLevadura:F3} kg";
                    label18.Text = $"Botellas: {almacenBotellas:F0}";
                    label32.Text = $"Lúpulo: {almacenLupulo:F3} kg"; // Actualizamos inventario de Lúpulo
                    progressBar1.Value = 100;
                    progressLabel.Text = $"Progreso: 100%";
                });

                // Detener el proceso completamente sin activar el delay timer
                isRunning = false;
                isPaused = false;
                _processTimer.Enabled = false;
                _delayTimer.Enabled = false;
                _progressTimer.Enabled = false; // Desactivamos el timer de progreso
                UpdateUI(() =>
                {
                    button1.Text = "Empezar";
                });

                // Reiniciamos las variables para el próximo batch
                stage = 0;
                Array.Clear(stageUpdates, 0, stageUpdates.Length);
                // No reiniciamos stageStartTimes ni stageEndTimes para preservar las fechas
                pausedTime = 0; // Reiniciamos el tiempo pausado
                pauseStartTime = DateTime.MinValue; // Reiniciamos

                // Guardar el fin del lote en la base de datos
                UpdateBatchEndTime(lastBatchEndTime);

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
                label32.Text = $"Lúpulo: {almacenLupulo:F3} kg"; // Mostramos inventario inicial de Lúpulo
                progressBar1.Value = 0;
                progressLabel.Text = $"Progreso: 0%";
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
                            case "lúpulo": // Nueva condición para Lúpulo
                                label32.ForeColor = Color.Red;
                                statusMessage = $"Alerta: Lúpulo bajo ({ingrediente.Cantidad:F3} kg)";
                                break;
                        }
                    }
                    else
                    {
                        label15.ForeColor = Color.White;
                        label16.ForeColor = Color.White;
                        label17.ForeColor = Color.White;
                        label18.ForeColor = Color.White;
                        label32.ForeColor = Color.White; // Restablecemos el color de Lúpulo
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
                _inventoryManager.AddIngrediente(new Ingrediente { Nombre = "Botellas", Cantidad = 10000m, UmbralMinimo = 900m }); // Ajustamos el umbral a 900
            if (!ingredientes.Exists(i => i.Nombre.ToLower() == "lúpulo")) // Añadimos Lúpulo
                _inventoryManager.AddIngrediente(new Ingrediente { Nombre = "Lúpulo", Cantidad = 30m, UmbralMinimo = 3m });
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
                { "CantidadBotellas", $"{batchName}: (Lager) {Math.Round(cantidad):F0} botellas ➞ 1L" },
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

        private void button4_Click(object sender, EventArgs e)
        {
            // Cerrar la aplicación
            Application.Exit();
        }
        private void label1_Click(object sender, EventArgs e)
        {
        }

        private void label24_Click(object sender, EventArgs e)
        {
        }

        private void label5_Click(object sender, EventArgs e)
        {
        }

        private void label28_Click(object sender, EventArgs e)
        {
        }

        private void label29_Click(object sender, EventArgs e)
        {
        }

        private void label41_Click(object sender, EventArgs e)
        {

        }
    }
}