namespace BrewScada
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.TextBox txtCantidadMalta;
        private System.Windows.Forms.TextBox txtCantidadAgua;
        private System.Windows.Forms.TextBox txtCantidadLevadura;
        private System.Windows.Forms.TextBox txtCantidadBotellas;
        private System.Windows.Forms.Button btnMolienda;
        private System.Windows.Forms.Button btnCoccion;
        private System.Windows.Forms.Button btnFermentacion;
        private System.Windows.Forms.Button btnEmbotellado;
        private System.Windows.Forms.Label lblResultadoMolienda;
        private System.Windows.Forms.Label lblResultadoCoccion;
        private System.Windows.Forms.Label lblResultadoFermentacion;
        private System.Windows.Forms.Label lblResultadoEmbotellado;

        private void InitializeComponent()
        {
            this.txtCantidadMalta = new System.Windows.Forms.TextBox();
            this.txtCantidadAgua = new System.Windows.Forms.TextBox();
            this.txtCantidadLevadura = new System.Windows.Forms.TextBox();
            this.txtCantidadBotellas = new System.Windows.Forms.TextBox();
            this.btnMolienda = new System.Windows.Forms.Button();
            this.btnCoccion = new System.Windows.Forms.Button();
            this.btnFermentacion = new System.Windows.Forms.Button();
            this.btnEmbotellado = new System.Windows.Forms.Button();
            this.lblResultadoMolienda = new System.Windows.Forms.Label();
            this.lblResultadoCoccion = new System.Windows.Forms.Label();
            this.lblResultadoFermentacion = new System.Windows.Forms.Label();
            this.lblResultadoEmbotellado = new System.Windows.Forms.Label();

            // 
            // txtCantidadMalta
            // 
            this.txtCantidadMalta.Location = new System.Drawing.Point(12, 12);
            this.txtCantidadMalta.Name = "txtCantidadMalta";
            this.txtCantidadMalta.Size = new System.Drawing.Size(100, 20);
            this.txtCantidadMalta.TabIndex = 0;
            this.txtCantidadMalta.PlaceholderText = "Cantidad Malta";

            // 
            // txtCantidadAgua
            // 
            this.txtCantidadAgua.Location = new System.Drawing.Point(12, 38);
            this.txtCantidadAgua.Name = "txtCantidadAgua";
            this.txtCantidadAgua.Size = new System.Drawing.Size(100, 20);
            this.txtCantidadAgua.TabIndex = 1;
            this.txtCantidadAgua.PlaceholderText = "Cantidad Agua";

            // 
            // txtCantidadLevadura
            // 
            this.txtCantidadLevadura.Location = new System.Drawing.Point(12, 64);
            this.txtCantidadLevadura.Name = "txtCantidadLevadura";
            this.txtCantidadLevadura.Size = new System.Drawing.Size(100, 20);
            this.txtCantidadLevadura.TabIndex = 2;
            this.txtCantidadLevadura.PlaceholderText = "Cantidad Levadura";

            // 
            // txtCantidadBotellas
            // 
            this.txtCantidadBotellas.Location = new System.Drawing.Point(12, 90);
            this.txtCantidadBotellas.Name = "txtCantidadBotellas";
            this.txtCantidadBotellas.Size = new System.Drawing.Size(100, 20);
            this.txtCantidadBotellas.TabIndex = 3;
            this.txtCantidadBotellas.PlaceholderText = "Cantidad Botellas";

            // 
            // btnMolienda
            // 
            this.btnMolienda.Location = new System.Drawing.Point(150, 10);
            this.btnMolienda.Name = "btnMolienda";
            this.btnMolienda.Size = new System.Drawing.Size(75, 23);
            this.btnMolienda.TabIndex = 4;
            this.btnMolienda.Text = "Molienda";
            this.btnMolienda.Click += new System.EventHandler(this.btnMolienda_Click);

            // 
            // btnCoccion
            // 
            this.btnCoccion.Location = new System.Drawing.Point(150, 36);
            this.btnCoccion.Name = "btnCoccion";
            this.btnCoccion.Size = new System.Drawing.Size(75, 23);
            this.btnCoccion.TabIndex = 5;
            this.btnCoccion.Text = "Cocción";
            this.btnCoccion.Click += new System.EventHandler(this.btnCoccion_Click);

            // 
            // btnFermentacion
            // 
            this.btnFermentacion.Location = new System.Drawing.Point(150, 62);
            this.btnFermentacion.Name = "btnFermentacion";
            this.btnFermentacion.Size = new System.Drawing.Size(75, 23);
            this.btnFermentacion.TabIndex = 6;
            this.btnFermentacion.Text = "Fermentación";
            this.btnFermentacion.Click += new System.EventHandler(this.btnFermentacion_Click);

            // 
            // btnEmbotellado
            // 
            this.btnEmbotellado.Location = new System.Drawing.Point(150, 88);
            this.btnEmbotellado.Name = "btnEmbotellado";
            this.btnEmbotellado.Size = new System.Drawing.Size(75, 23);
            this.btnEmbotellado.TabIndex = 7;
            this.btnEmbotellado.Text = "Embotellado";
            this.btnEmbotellado.Click += new System.EventHandler(this.btnEmbotellado_Click);

            // 
            // lblResultadoMolienda
            // 
            this.lblResultadoMolienda.Location = new System.Drawing.Point(250, 12);
            this.lblResultadoMolienda.Name = "lblResultadoMolienda";
            this.lblResultadoMolienda.Size = new System.Drawing.Size(200, 20);

            // 
            // lblResultadoCoccion
            // 
            this.lblResultadoCoccion.Location = new System.Drawing.Point(250, 38);
            this.lblResultadoCoccion.Name = "lblResultadoCoccion";
            this.lblResultadoCoccion.Size = new System.Drawing.Size(200, 20);

            // 
            // lblResultadoFermentacion
            // 
            this.lblResultadoFermentacion.Location = new System.Drawing.Point(250, 64);
            this.lblResultadoFermentacion.Name = "lblResultadoFermentacion";
            this.lblResultadoFermentacion.Size = new System.Drawing.Size(200, 20);

            // 
            // lblResultadoEmbotellado
            // 
            this.lblResultadoEmbotellado.Location = new System.Drawing.Point(250, 90);
            this.lblResultadoEmbotellado.Name = "lblResultadoEmbotellado";
            this.lblResultadoEmbotellado.Size = new System.Drawing.Size(200, 20);

            this.Controls.Add(this.txtCantidadMalta);
            this.Controls.Add(this.txtCantidadAgua);
            this.Controls.Add(this.txtCantidadLevadura);
            this.Controls.Add(this.txtCantidadBotellas);
            this.Controls.Add(this.btnMolienda);
            this.Controls.Add(this.btnCoccion);
            this.Controls.Add(this.btnFermentacion);
            this.Controls.Add(this.btnEmbotellado);
            this.Controls.Add(this.lblResultadoMolienda);
            this.Controls.Add(this.lblResultadoCoccion);
            this.Controls.Add(this.lblResultadoFermentacion);
            this.Controls.Add(this.lblResultadoEmbotellado);
        }
    }
}
