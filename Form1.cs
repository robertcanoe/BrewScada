using System;
using System.Text;
using System.Windows.Forms;
using MongoDB.Driver;
using System.Timers;

namespace BrewScada
{
    public partial class Form1 : Form
    {
        private MongoDBConnection _dbConnection;
        private IMongoCollection<Produccion> _produccionCollection;
        private IMongoCollection<Counter> _counterCollection;
        private IMongoCollection<Ingrediente> _ingredientesCollection;
        private System.Timers.Timer _timer;
        private Random _random;
        private bool isRunning;
        private string batchDetails;
        private decimal cantidadMalta;
        private decimal cantidadAgua;
        private decimal cantidadLevadura;
        private decimal cantidadBotellas;
        private int stage;

        private decimal almacenMalta = 200m;
        private decimal almacenAgua = 1200m;
        private decimal almacenLevadura = 150m;
        private decimal almacenBotellas = 1000m;

        private InventoryManager _inventoryManager;
        private ProductionLog _productionLog;

        public Form1()
        {
            InitializeComponent();
            var connectionString = GetConnectionStringFromConfig();
            var databaseName = "BrewScada";
            _dbConnection = new MongoDBConnection(connectionString, databaseName);
            _produccionCollection = _dbConnection.GetCollection<Produccion>("Produccion");
            _counterCollection = _dbConnection.GetCollection<Counter>("Counters");
            _ingredientesCollection = _dbConnection.GetCollection<Ingrediente>("Ingredientes");

            _random = new Random();
            InitializeTimer();
            isRunning = false;
            batchDetails = string.Empty;
            stage = 0;

            _inventoryManager = new InventoryManager(_ingredientesCollection);
            _productionLog = new ProductionLog(_produccionCollection);
        }

        private string GetConnectionStringFromConfig()
        {
            return "mongodb://localhost:27017";
        }

        private void InitializeTimer()
        {
            _timer = new System.Timers.Timer(10000); // 10 segundos por etapa
            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = true;
            _timer.Enabled = false;
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            if (!isRunning) return;

            switch (stage)
            {
                case 0:
                    cantidadMalta = _random.Next(1, 10);
                    decimal molienda = ProcesarMolienda(cantidadMalta);
                    decimal sobranteMalta = almacenMalta - cantidadMalta;
                    almacenMalta -= cantidadMalta;
                    batchDetails = FormatearDetallesProceso($"Molienda: {molienda} kg", $"Sobrante Malta: {sobranteMalta} kg");
                    UpdateUI(() =>
                    {
                        label7.Text = molienda.ToString("F2");
                        textBox1.Text = batchDetails;
                        label15.Text = $"Malta de cebada: {almacenMalta} kg";
                    });
                    stage++;
                    break;
                case 1:
                    cantidadAgua = _random.Next(10, 100);
                    decimal coccion = ProcesarCoccion(cantidadAgua);
                    decimal sobranteAgua = almacenAgua - cantidadAgua;
                    almacenAgua -= cantidadAgua;
                    batchDetails = FormatearDetallesProceso(batchDetails, $"Cocción: {coccion} L", $"Sobrante Agua: {sobranteAgua} L");
                    UpdateUI(() =>
                    {
                        label8.Text = coccion.ToString("F2");
                        textBox1.Text = batchDetails;
                        label16.Text = $"Agua: {almacenAgua} L";
                    });
                    stage++;
                    break;
                case 2:
                    cantidadLevadura = _random.Next(1, 10) / 10m;
                    decimal fermentacion = ProcesarFermentacion(cantidadLevadura);
                    decimal sobranteLevadura = almacenLevadura - cantidadLevadura;
                    almacenLevadura -= cantidadLevadura;
                    batchDetails = FormatearDetallesProceso(batchDetails, $"Fermentación: {fermentacion} g", $"Sobrante Levadura: {sobranteLevadura} kg");
                    UpdateUI(() =>
                    {
                        label9.Text = fermentacion.ToString("F2");
                        textBox1.Text = batchDetails;
                        label17.Text = $"Levadura: {almacenLevadura} kg";
                    });
                    stage++;
                    break;
                case 3:
                    cantidadBotellas = _random.Next(1, 10);
                    decimal embotellado = ProcesarEmbotellado(cantidadBotellas);
                    decimal sobranteBotellas = almacenBotellas - cantidadBotellas;
                    almacenBotellas -= cantidadBotellas;
                    batchDetails = FormatearDetallesProceso(batchDetails, $"Embotellado: {Math.Round(embotellado)} botellas", $"Sobrante Botellas: {sobranteBotellas} botellas");
                    UpdateUI(() =>
                    {
                        label10.Text = Math.Round(embotellado).ToString("F0");
                        textBox1.Text = batchDetails;
                        label18.Text = $"Botellas: {almacenBotellas}";
                        var tiempos = CalcularTiemposDeProceso();
                        label12.Text = tiempos.Item5.ToString("F2");
                    });
                    isRunning = false;
                    _timer.Enabled = false;
                    StartNextBatch();
                    break;
            }
        }

        private void StartNextBatch()
        {
            UpdateUI(() =>
            {
                button1.Text = "Empezar";
                label7.Text = "00:00";
                label8.Text = "00:00";
                label9.Text = "00:00";
                label10.Text = "00:00";
                label12.Text = "00:00";
            });

            int nextBatchNumber = GetNextBatchNumber();
            var produccion = new Produccion
            {
                BatchName = "Batch_" + nextBatchNumber.ToString("D4"),
                Status = "Started",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMinutes(15) // Example duration
            };

            _produccionCollection.InsertOne(produccion);
            UpdateUI(() =>
            {
                label1.Text = "Producción en ejecución: " + produccion.BatchName;
                batchDetails = $"Batch {produccion.BatchName} Details:\n";
                button1.Text = "En ejecución";
            });
            _timer.Enabled = true;
            isRunning = true;
            stage = 0;
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
            return cantidadMalta * 0.9m; // Ejemplo de procesamiento
        }

        private decimal ProcesarCoccion(decimal cantidadAgua)
        {
            return cantidadAgua * 0.8m; // Ejemplo de procesamiento
        }

        private decimal ProcesarFermentacion(decimal cantidadLevadura)
        {
            return cantidadLevadura * 0.95m; // Ejemplo de procesamiento
        }

        private decimal ProcesarEmbotellado(decimal cantidadBotellas)
        {
            return Math.Round(cantidadBotellas * 0.98m); // Ejemplo de procesamiento redondeado
        }

        private Tuple<decimal, decimal, decimal, decimal, decimal> CalcularTiemposDeProceso()
        {
            decimal molienda = 1m; // 1 minuto
            decimal coccion = 2m; // 2 minutos
            decimal fermentacion = 3m; // 3 minutos
            decimal embotellado = 1m; // 1 minuto
            decimal fermentacionTanque = 5m; // 5 minutos
            decimal maduracionTanque = 10m; // 10 minutos
            decimal totalTanque = fermentacionTanque + maduracionTanque; // 15 minutos

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
                EndDate = DateTime.Now.AddMinutes(15) // Example duration
            };

            _produccionCollection.InsertOne(produccion);
            UpdateUI(() =>
            {
                label1.Text = "Producción en ejecución: " + produccion.BatchName;
                batchDetails = $"Batch {produccion.BatchName} Details:\n";
                button1.Text = "En ejecución";
            });
            _timer.Enabled = true;
            stage = 0;
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
    }
}