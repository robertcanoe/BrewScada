using System;
using System.Windows.Forms;

namespace BrewScada
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void btnMolienda_Click(object sender, EventArgs e)
        {
            decimal cantidadMalta = Convert.ToDecimal(txtCantidadMalta.Text);
            decimal resultado = Molienda(cantidadMalta);
            lblResultadoMolienda.Text = $"Cantidad procesada: {resultado} kg";
        }

        private void btnCoccion_Click(object sender, EventArgs e)
        {
            decimal cantidadAgua = Convert.ToDecimal(txtCantidadAgua.Text);
            decimal resultado = Coccion(cantidadAgua);
            lblResultadoCoccion.Text = $"Cantidad procesada: {resultado} litros";
        }

        private void btnFermentacion_Click(object sender, EventArgs e)
        {
            decimal cantidadLevadura = Convert.ToDecimal(txtCantidadLevadura.Text);
            decimal resultado = Fermentacion(cantidadLevadura);
            lblResultadoFermentacion.Text = $"Cantidad utilizada: {resultado} gramos";
        }

        private void btnEmbotellado_Click(object sender, EventArgs e)
        {
            decimal cantidadBotellas = Convert.ToDecimal(txtCantidadBotellas.Text);
            decimal resultado = Embotellado(cantidadBotellas);
            lblResultadoEmbotellado.Text = $"Cantidad embotellada: {resultado} botellas";
        }

        public decimal Molienda(decimal cantidadMalta)
        {
            return cantidadMalta * 0.9m; // Ejemplo de procesamiento
        }

        public decimal Coccion(decimal cantidadAgua)
        {
            return cantidadAgua * 0.85m; // Ejemplo de procesamiento
        }

        public decimal Fermentacion(decimal cantidadLevadura)
        {
            return cantidadLevadura * 1.1m; // Ejemplo de procesamiento
        }

        public decimal Embotellado(decimal cantidadBotellas)
        {
            return cantidadBotellas * 1.0m; // Ejemplo de procesamiento
        }
    }
}
