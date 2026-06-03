using MySql.Data.MySqlClient;
using System;
using System.Windows.Forms;

namespace BlzSoftTerminal
{
    public partial class ConfigDb : Form
    {
        public ConfigDb()
        {
            InitializeComponent();
            CargarConfiguracion();
        }

        private void CargarConfiguracion()
        {
            DbConfig cfg = ConfigManager.Load();
            txtServer.Text = cfg.Server;
            txtDatabase.Text = cfg.Database;
            txtUser.Text = cfg.UserId;
            txtPassword.Text = cfg.Password;
        }

        private void btnProbar_Click(object sender, EventArgs e)
        {
            string connStr = ObtenerConnectionString();
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    lblEstado.Text = "Conexion exitosa";
                    lblEstado.ForeColor = System.Drawing.Color.Green;
                }
            }
            catch (Exception ex)
            {
                lblEstado.Text = "Error: " + ex.Message;
                lblEstado.ForeColor = System.Drawing.Color.Red;
            }
        }

        private void btnGuardar_Click(object sender, EventArgs e)
        {
            DbConfig cfg = new DbConfig
            {
                Server = txtServer.Text.Trim(),
                Database = txtDatabase.Text.Trim(),
                UserId = txtUser.Text.Trim(),
                Password = txtPassword.Text.Trim()
            };

            ConfigManager.Save(cfg);
            lblEstado.Text = "Configuracion guardada";
            lblEstado.ForeColor = System.Drawing.Color.Green;
        }

        private string ObtenerConnectionString()
        {
            return $"Server={txtServer.Text.Trim()};Database={txtDatabase.Text.Trim()};Uid={txtUser.Text.Trim()};Pwd={txtPassword.Text.Trim()};";
        }
    }
}
