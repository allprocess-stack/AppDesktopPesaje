using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySql.Data.MySqlClient;

namespace BlzSoftTerminal
{
    public partial class Login : Form
    {
        string id, username, userlogin, userpass, usertype;
        public Login()
        {
            InitializeComponent();
        }
        
        private void btnIngresar_Click(object sender, EventArgs e)
        {
                using (MySqlConnection conn = Database.GetConnection())
                {
                    conn.Open();

                    string query = "SELECT * FROM USUARIOS WHERE Usuario=@usuario AND Contrasenha=@contra";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@usuario", txtUser.Text);
                        cmd.Parameters.AddWithValue("@contra", txtPass.Text);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                id = reader["Id"].ToString();
                                username = reader["Nombre"].ToString();
                                userlogin = reader["Usuario"].ToString();
                                userpass = reader["Contrasenha"].ToString();
                                usertype = reader["Tipo"].ToString();
                            }

                            if (string.IsNullOrWhiteSpace(id))
                            {
                                MessageBox.Show("Usuario No valido");
                            }
                            else
                            {
                                ConnFile.USER_ID = Convert.ToInt32(id);
                                if (usertype == "admin")
                                { Main.configuracionMode = true; this.Close(); }
                            }
                        }

                    }

                }
            
           

        }

        private void Login_Load(object sender, EventArgs e)
        {

        }
    }
}
