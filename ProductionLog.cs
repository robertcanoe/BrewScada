using System;
using MongoDB.Driver;
using System.Collections.Generic;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace BrewScada
{
    public class ProductionLog
    {
        private IMongoCollection<Produccion> _produccionCollection;

        public ProductionLog(IMongoCollection<Produccion> produccionCollection)
        {
            _produccionCollection = produccionCollection;
        }

        public List<Produccion> GetProductionLogs()
        {
            return _produccionCollection.Find(_ => true).ToList();
        }

        public void ExportProductionLogsToPDF(string filePath)
        {
            var logs = GetProductionLogs();
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var document = new Document();
                PdfWriter.GetInstance(document, stream);
                document.Open();

                document.Add(new Paragraph("Production Logs"));
                document.Add(new Paragraph(" "));

                var table = new PdfPTable(4);
                table.AddCell("BatchName");
                table.AddCell("Status");
                table.AddCell("StartDate");
                table.AddCell("EndDate");

                foreach (var log in logs)
                {
                    table.AddCell(log.BatchName);
                    table.AddCell(log.Status);
                    table.AddCell(log.StartDate.ToString());
                    table.AddCell(log.EndDate.ToString());
                }

                document.Add(table);
                document.Close();
            }
        }
    }
}