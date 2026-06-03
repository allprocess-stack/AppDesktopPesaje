using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;


namespace BlzSoftTerminal
{
    public partial class Main : Form
    {
        byte[] rxdataModbus = new byte[10];
        public static string Trama;
        public static string PesoStr;
        public static int timeoutSerial;
        public static int timeoutModbus;
        public static char stateManualS1;
        public static char stateManualS2;
        public static bool ENmodbus = true;
        public static int UsuarioLogueadoID = 0;
        public static int IndType;
        public static string pesoLocal;
        public static string IndicadorActual = "";
        public static bool modoGuardarActivo = false;
        public static bool configuracionMode = false;
        public static int ID_detected=0;
        public static bool QRevent = false;

        string fechaPeso1, fechaPeso2, fechaPeso3, fechaPeso4, fechaPeso5, fechaPeso6;
        string peso1, peso2, peso3, peso4, peso5, peso6, pesoNeto1, pesoNeto2, pesoNeto3, pesoNeto4, pesoNeto5, prd1, prd2, prd3, prd4, prd5, cantidad;
        int totalProductos = 0;

        private char ultimoEstadoSemaforos = 'o';
        // BALANZA
        private SerialPort serialBalanza = new SerialPort();
        private int pesoUmbral = 30;

        // SEMÁFORO        
        private int timeoutSemaforo = 0;
        private bool errorSemaforo = false;

        private bool registroEnIngreso = true; // flag real para flujo ingreso/salida

        // CONTROL
        private bool bloqueado = false;


                
        //============CONFIGURACION INICIAL DE PARAMETROS Y VARIABLES DE PROGRAMA======================
        //===================================================        
        public Main()
        {
            InitializeComponent();
            CargarConfiguracion();
            foreach (string s in SerialPort.GetPortNames())
            {
                toolStripComboBoxSerial1.Items.Add(s);
                toolStripComboBoxSerial2.Items.Add(s);
                toolStripComboBoxSerial3.Items.Add(s);

            }

            // Comprobar controles (solo para debug, quitar en producción)
            if (txtPesoTara == null) MessageBox.Show("txtPesoSalida es NULL");
            if (txtPesoNeto == null) MessageBox.Show("txtNeto es NULL");
            if (lblPesoSalida == null) MessageBox.Show("lblSalida es NULL");
            if (lblPesoNeto == null) MessageBox.Show("lblNeto es NULL");

            /* Mostrar/Ocultar solo visualmente al iniciar (no determina la lógica)
            txtPesoSalida.Visible = false;
            txtPesoNeto.Visible = false;
            lblPesoSalida.Visible = false;
            lblPesoNeto.Visible = false;
            conexiónToolStripMenuItem.Visible = false;
            parametrosToolStripMenuItem.Visible = false;
            */
            
            // Inicializar flag por defecto (su valor real vendrá al buscar registro o guardar)
            registroEnIngreso = true;

        }
        private void Main_Load(object sender, EventArgs e)
        {
            //ConnFile.Load();
            CargarConfiguracion();
            AplicarConfiguracion();

            //configuracion de puerto modbus
            serialPort1.ReceivedBytesThreshold = 16;
            serialPort1.NewLine = "\x0D";

            // SEMÁFORO
            // CONFIGURAR UMBRAL Y PUERTO SEMÁFORO
            serialPort1.PortName = ConnFile.COM_SEMAFORO;
            try { OpenSerialPort1(); }
            catch
            {
                lblStatusCom1.Text = "Detenida";
                lblStatusCom1.ForeColor = Color.Red;
                toolStripComboBoxSerial1.Enabled = true;
                menuAbrirCOM1.Enabled = true;
                menuCerrarCOM1.Enabled = false;
                MessageBox.Show("No se pudo abrir puerto " + ConnFile.COM_SEMAFORO + "Para conexion de semaforo");
            }

            // BALANZA
            serialPort2.PortName = ConnFile.COM_BALANZA;
            try { OpenSerialPort2(); }
            catch
            {
                lblStatusCom2.Text = "Detenida";
                lblStatusCom2.ForeColor = Color.Red;
                toolStripComboBoxSerial2.Enabled = true;
                menuAbrirCOM2.Enabled = true;
                menuCerrarCOM2.Enabled = false;
                MessageBox.Show("No se pudo abrir puerto " + ConnFile.COM_BALANZA + "Para conexion de balanza");
            }

            // LECTOR
            serialPort3.PortName = ConnFile.COM_LECTOR;
            try { OpenSerialPort3(); }
            catch
            {
                lblStatusCOM3.Text = "Detenida";
                lblStatusCOM3.ForeColor = Color.Red;
                toolStripComboBoxSerial3.Enabled = true;
                menuAbrirCOM3.Enabled = true;
                menuCerrarCOM3.Enabled = false;
                MessageBox.Show("No se pudo abrir puerto " + ConnFile.COM_LECTOR + "Para conexion de lector");
            }

            string itemText;
            int i;

            // BUSCA Y SELECCIONA EL INDICADOR GUARDADO EN ConnFile

            //carga modelo de indicador anterior
            for (i = 0; i < indicadorToolStripComboBox1.Items.Count; i++)        //inspecciona items del comboBox
            {
                itemText = indicadorToolStripComboBox1.Items[i].ToString();
                if (itemText == ConnFile.INDICADOR)  //inspecciona si Indicador guardado en ConnFile esta registrado en comboBox
                {
                    indicadorToolStripComboBox1.SelectedIndex = i;       //selecciona item de combo box segun el guardado anteriormente para que se muestre
                    CfgSerialIndicador(i);      //va a configurar parametros de recepcion serial segun el indicador guardado antes
                    IndType = i;
                    break;  //encuentra item selecciona y sale
                }
            }
            if (i >= indicadorToolStripComboBox1.Items.Count)            //si encaso no encontro el item sale mensaje de error
            { MessageBox.Show("Software no compatible con Indicador " + ConnFile.INDICADOR); }


            // INICIAR TIMERS
            timeoutSerial = 0;
            timeoutModbus = 0;
                        
            timer1.Enabled = true;
            timer1.Start();
            
            timer2.Enabled = true;
            timer2.Start();
            
            timer3.Enabled = true;
            timer3.Start();

            panelRegistro.Enabled = false;
            panelPesos.Enabled = false;
        }
        private void CargarConfiguracion()
        {
            string query = @"
        SELECT COMBalanza, COMSemaforo, COMLector, Umbral, Indicador, Usuario
        FROM CONFIGURACION_UMBRAL
        ORDER BY Id DESC
        LIMIT 1;";

            using (MySqlConnection conn = Database.GetConnection())
            using (var cmd = new MySqlCommand(query, conn))
            {
                try { conn.Open(); }
                catch { MessageBox.Show("No se puede conectar a la base de datos del sistema"); }

                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    toolStripComboBoxSerial2.Text = reader["COMBalanza"].ToString();
                    toolStripComboBoxSerial1.Text = reader["COMSemaforo"].ToString();
                    toolStripComboBoxSerial3.Text = reader["COMLector"].ToString();
                    indicadorToolStripComboBox1.Text = reader["Indicador"].ToString();
                    txtPesoUmbral.Text = reader["Umbral"].ToString();

                    // Actualizar ConnFile con lo que viene de BD
                    ConnFile.COM_BALANZA = toolStripComboBoxSerial2.Text;
                    ConnFile.COM_SEMAFORO = toolStripComboBoxSerial1.Text;
                    ConnFile.COM_LECTOR = toolStripComboBoxSerial3.Text;
                    ConnFile.UMBRAL = txtPesoUmbral.Text;
                    ConnFile.INDICADOR = indicadorToolStripComboBox1.Text;

                    IndType = GetIndicadorType(indicadorToolStripComboBox1.Text);
                }
            }
        }
        private int GetIndicadorType(string indName)
        {
            switch (indName.Trim().ToUpper())
            {
                case "TRANSCELL": return 0;
                case "FT11": return 1;
                case "XK": return 2;
                case "PRECIA": return 3;
                default: return 0;
            }
        }
        public void CfgSerialIndicador(int type)
        {
            //por defecto trama serial port 3
            serialPort3.NewLine = "\r\n";

            switch (type)
            {
                case 0: // TransCELL TI1520
                    serialPort2.BaudRate = 9600;
                    serialPort2.DataBits = 8;
                    serialPort2.Parity = Parity.None;
                    serialPort2.StopBits = StopBits.One;

                    serialPort2.ReceivedBytesThreshold = 1;
                    serialPort2.NewLine = "\r\n";
                    break;

                case 1: // Flintec FT11
                    serialPort2.BaudRate = 9600;
                    serialPort2.DataBits = 8;
                    serialPort2.Parity = Parity.None;
                    serialPort2.StopBits = StopBits.One;

                    serialPort2.ReceivedBytesThreshold = 36;
                    serialPort2.NewLine = "\r"; // CR
                    break;

                case 2: // XK / genérico chino (wn...)
                    serialPort2.BaudRate = 9600;
                    serialPort2.DataBits = 8;
                    serialPort2.Parity = Parity.None;
                    serialPort2.StopBits = StopBits.One;

                    serialPort2.ReceivedBytesThreshold = 24;
                    serialPort2.NewLine = "\r\n";
                    break;

                case 3: // Precia Molen
                    serialPort2.BaudRate = 9600;
                    serialPort2.DataBits = 8;
                    serialPort2.Parity = Parity.None;
                    serialPort2.StopBits = StopBits.One;

                    serialPort2.ReceivedBytesThreshold = 49;
                    serialPort2.NewLine = "\n";
                    break;

                default:
                    return;
            }
        }


        //=======================TIMERS===========================================================================
        //========================================================================================================
        private void timer1_Tick(object sender, EventArgs e)    //actualizacion de etiquetas de conexion de datos en barra estadp
        {
            if (timeoutSerial < 32000)      //tiempofuera balanza
                timeoutSerial++;

            if (timeoutModbus < 32000)     //tiempofuera modbus
                timeoutModbus++;

            if (timeoutModbus > 20)     //2000 SEGUNDOS
            {
                lblStatusDataSerial1.Text = "sin respuesta " + timeoutModbus / 5 + "s  EN:" + ENmodbus;
                lblStatusDataSerial1.ForeColor = System.Drawing.Color.Red;
            }
            else
            {
                lblStatusDataSerial1.Text = "Ok";
                lblStatusDataSerial1.ForeColor = System.Drawing.Color.Green;
            }

            if (timeoutSerial > 20)
            {
                lblStatusDataSerial2.Text = "no Data";
                lblStatusDataSerial2.ForeColor = Color.Red;
                txtBalanza.Text = "-----";return;
            }
            else
            {
                lblStatusDataSerial2.Text = "Ok";
                lblStatusDataSerial2.ForeColor = System.Drawing.Color.Green;
            }

            if (serialPort2.IsOpen == false)
            { txtBalanza.Text = ""; return; }

            //peso ok
            txtBalanza.Text = PesoStr + " Kg";

        }

        private void timer2_Tick(object sender, EventArgs e)    //actualizacion de estado de semaforo        
        {
            // Si no hay comunicación con la balanza
            if (timeoutSerial > 20)
            {
                setSemaforo('o');                
                return;
            }


            if (!serialPort1.IsOpen) return;
            try
            {
                int val = (int)double.Parse(PesoStr);
                int umbral = int.Parse(txtPesoUmbral.Text);
                // PESO <= UMBRAL → AMBOS VERDE ('V')
                if (val <= umbral)
                {
                    setSemaforo('V');                    
                    if(modoGuardarActivo)
                    { LimpiarCampos(); }
                    modoGuardarActivo = false;   // liberar "S" y volver a automático                    
                    return;
                }

                // PESO > UMBRAL                

                // Semáforo 1 depende del botón GUARDAR
                if (modoGuardarActivo)
                {
                    // Mantener la condición 'S'
                    setSemaforo('S');
                }
                else
                {
                    // Comportamiento normal por peso
                    setSemaforo('R');
                }
            }
            catch
            {
                setSemaforo('o');
                
            }
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            if (QRevent)
            { QRevent = false; LimpiarCampos(); llenar_campos();  }
        }

        //===============================AJUSTE DE ESTADO DE SEMAFORO=============================================
        //========================================================================================================
        public void setSemaforo(char evento)
        {
            byte[] tramaMB_off = { 0x01, 0x0F, 0x00, 0x00, 0x00, 0x08, 0x01, 0x00, 0xFE, 0x95 };
            byte[] tramaMB_rojo = { 0x01, 0x0F, 0x00, 0x00, 0x00, 0x08, 0x01, 0x05, 0x3E, 0x96 };
            //byte[] tramaMB_rojo = { 0x01, 0x0F, 0x00, 0x00, 0x00, 0x08, 0x01, 0x01, 0x3F, 0x55 };
            byte[] tramaMB_verde = { 0x01, 0x0F, 0x00, 0x00, 0x00, 0x08, 0x01, 0x0A, 0x7E, 0x92 };
            //byte[] tramaMB_verde = { 0x01, 0x0F, 0x00, 0x00, 0x00, 0x08, 0x01, 0x02, 0x7F, 0x54 };
            byte[] tramaMB_v_r = { 0x01, 0x0F, 0x00, 0x00, 0x00, 0x08, 0x01, 0x06, 0x7E, 0x97 };

            if (!(serialPort1.IsOpen))  //puerto no abierto regresa
            { return; }

            if (ENmodbus == false) {
                return;
            }      //si se inhabilita trama modbus

            switch (evento)
            {
                case 'o':
                    Semaforo2.Image = BlzSoftTerminal.Properties.Resources.imgbase;
                    Semaforo1.Image = BlzSoftTerminal.Properties.Resources.imgbase;
                    serialPort1.Write(tramaMB_off, 0, 10);
                    break;
                case 'R':
                    //
                    Semaforo2.Image = BlzSoftTerminal.Properties.Resources.rojo;
                    Semaforo1.Image = BlzSoftTerminal.Properties.Resources.rojo;
                    serialPort1.Write(tramaMB_rojo, 0, 10);
                    break;
                case 'V':
                    Semaforo2.Image = BlzSoftTerminal.Properties.Resources.verde;
                    Semaforo1.Image = BlzSoftTerminal.Properties.Resources.verde;
                    serialPort1.Write(tramaMB_verde, 0, 10);
                    break;
                case 'S':
                    Semaforo1.Image = BlzSoftTerminal.Properties.Resources.verde;
                    Semaforo2.Image = BlzSoftTerminal.Properties.Resources.rojo;
                    serialPort1.Write(tramaMB_v_r, 0, 10);
                    break;
            }
        }


        //===============================MANIPULACION DE PUERTOS SERIALES=============================================
        //========================================================================================================        
        /*
         *       -----------------------------
         *       |   PUERTOS SERIALES MENU   |
         *       -----------------------------
         */
        public void OpenSerialPort1()
        {
            try
            {
                if (!serialPort1.IsOpen)
                {
                    serialPort1.Open();
                    toolStripComboBoxSerial1.Enabled = false;
                    menuAbrirCOM1.Enabled = false;
                    menuCerrarCOM1.Enabled = true;
                    lblStatusCom1.Text = "Iniciada";
                    lblStatusCom1.ForeColor = System.Drawing.Color.Green;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se puede tener acceso a Puerto: [" + serialPort1.PortName + "]\n" + ex.Message);
            }
        }
        public void CloseSerialPort1()      // CERRAR ´PUERTO SEMAFORO
        {
            if (serialPort1.IsOpen)
            {
                Thread CloseDown1 = new Thread(new ThreadStart(ThreadCloseSerial1));
                CloseDown1.Start();

                lblStatusCom1.Text = "Detenida";
                lblStatusCom1.ForeColor = System.Drawing.Color.Red;
            }
        }
        private void ThreadCloseSerial1()
        {
            System.Threading.Thread.Sleep(2000);
            try
            {
                if (serialPort1.IsOpen)
                    serialPort1.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        public void OpenSerialPort2()       // ABRIR PUERTO BALANZA
        {
            try
            {
                if (!serialPort2.IsOpen)
                {
                    serialPort2.Open();
                    toolStripComboBoxSerial2.Enabled = false;
                    menuAbrirCOM2.Enabled = false;
                    menuCerrarCOM2.Enabled = true;
                    lblStatusCom2.Text = "Iniciada";
                    lblStatusCom2.ForeColor = System.Drawing.Color.Green;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se puede tener acceso a Puerto: [" + serialPort2.PortName + "]\n" + ex.Message);
            }
        }      
        public void CloseSerialPort2()  // CERRAR PUERTO BALANZA
        {
            if (serialPort2.IsOpen)
            {
                Thread CloseDown2 = new Thread(new ThreadStart(ThreadCloseSerial2));
                CloseDown2.Start();

                lblStatusCom2.Text = "Detenida";
                lblStatusCom2.ForeColor = System.Drawing.Color.Red;
            }
        }
        private void ThreadCloseSerial2()
        {
            System.Threading.Thread.Sleep(2000);
            try
            {
                if (serialPort2.IsOpen)
                    serialPort2.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }     
        public void OpenSerialPort3()   // ABRIR PUERTO LECTOR qr
        {
            try
            {
                if (!serialPort3.IsOpen)
                {
                    serialPort3.Open();
                    toolStripComboBoxSerial3.Enabled = false;
                    menuAbrirCOM3.Enabled = false;
                    menuCerrarCOM3.Enabled = true;
                    lblStatusCOM3.Text = "Iniciada";
                    lblStatusCOM3.ForeColor = System.Drawing.Color.Green;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se puede tener acceso a Puerto: [" + serialPort3.PortName + "]\n" + ex.Message);
            }
        }
        public void CloseSerialPort3()       // CERRAR PUERTO LECTOR
        {
            if (serialPort3.IsOpen)
            {
                Thread CloseDown3 = new Thread(new ThreadStart(ThreadCloseSerial3));
                CloseDown3.Start();

                lblStatusCOM3.Text = "Detenida";
                lblStatusCOM3.ForeColor = System.Drawing.Color.Red;
            }
        }
        private void ThreadCloseSerial3()
        {
            System.Threading.Thread.Sleep(2000);
            try
            {
                if (serialPort3.IsOpen)
                    serialPort3.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void menuAbrirCOM1_Click(object sender, EventArgs e) => OpenSerialPort1();  // SELECCIONAR COM SEMAFORO
        private void menuCerrarCOM1_Click(object sender, EventArgs e)           // DESELECCIONAR COM SEMAFORO
        {
            if (serialPort1.IsOpen)
            {
                CloseSerialPort1();
                toolStripComboBoxSerial1.Enabled = true;
                menuAbrirCOM1.Enabled = true;
                menuCerrarCOM1.Enabled = false;
            }
        }
        private void menuAbrirCOM2_Click(object sender, EventArgs e) => OpenSerialPort2();    // SELECCIONAR COM BALANZA
        private void menuCerrarCOM2_Click(object sender, EventArgs e)       // DESELECCIONAR COM BALANZA
        {
            if (serialPort2.IsOpen)
            {
                CloseSerialPort2();
                toolStripComboBoxSerial2.Enabled = true;
                menuAbrirCOM2.Enabled = true;
                menuCerrarCOM2.Enabled = false;
            }
        }
        private void menuAbrirCOM3_Click(object sender, EventArgs e) => OpenSerialPort3();      // SELECCIONAR COM LECTOR
        private void menuCerrarCOM3_Click(object sender, EventArgs e)           // DESELECCIONAR COM LECTOR
        {
            if (serialPort3.IsOpen)
            {
                CloseSerialPort3();
                toolStripComboBoxSerial3.Enabled = true;
                menuAbrirCOM3.Enabled = true;
                menuCerrarCOM3.Enabled = false;
            }
        }
        private void toolStripComboBoxSerial1_SelectedIndexChanged(object sender, EventArgs e)      // MENU BOTTOM PUERTO SEMAFORO
        {
            serialPort1.PortName = toolStripComboBoxSerial1.Text;
            ConnFile.COM_SEMAFORO = toolStripComboBoxSerial1.Text;
            //ConnFile.Save();
        }

        private void toolStripTextBox1_Click(object sender, EventArgs e)
        {
            new ConfigDb().ShowDialog();
        }

        private void conexiónToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void toolStripComboBoxSerial2_SelectedIndexChanged(object sender, EventArgs e)      // MENU BOTTOM PUERTO BALANZA
        {
            serialPort2.PortName = toolStripComboBoxSerial2.Text;
            ConnFile.COM_BALANZA = toolStripComboBoxSerial2.Text;
            //ConnFile.Save();
        }
        private void toolStripComboBoxSerial3_SelectedIndexChanged(object sender, EventArgs e)      // MENU BOTTOM PUERTO LECTOR
        {
            serialPort3.PortName = toolStripComboBoxSerial3.Text;
            ConnFile.COM_LECTOR = toolStripComboBoxSerial3.Text;
            //ConnFile.Save();
        }               
               
        // OBSERVACION
        /*private void AplicarEstadoSemaforos(char estado)
        {
            switch (estado)
            {
                case 'R': // rojo
                    Semaforo2.Image = BlzSoftTerminal.Properties.Resources.rojo;
                    Semaforo1.Image = BlzSoftTerminal.Properties.Resources.rojo;
                    Semaforo2.SizeMode = PictureBoxSizeMode.StretchImage;
                    Semaforo1.SizeMode = PictureBoxSizeMode.StretchImage;

                    break;
                case 'v': // verde
                    Semaforo2.Image = BlzSoftTerminal.Properties.Resources.verde;
                    Semaforo1.Image = BlzSoftTerminal.Properties.Resources.verde;
                    Semaforo2.SizeMode = PictureBoxSizeMode.StretchImage;
                    Semaforo1.SizeMode = PictureBoxSizeMode.StretchImage;

                    break;
                default: // apagado/base
                    Semaforo2.Image = BlzSoftTerminal.Properties.Resources.imgbase;
                    Semaforo1.Image = BlzSoftTerminal.Properties.Resources.imgbase;
                    Semaforo2.SizeMode = PictureBoxSizeMode.StretchImage;
                    Semaforo1.SizeMode = PictureBoxSizeMode.StretchImage;

                    break;
            }
        }*/

        //==============================AJUSTES DE MENU Y PARAMETROS=============================================
        //========================================================================================================        
        // GUARDA UMBRAL E INDICADOR 
        private void guardarPesoUmbralToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ConnFile.UMBRAL = txtPesoUmbral.Text;
            ConnFile.INDICADOR = indicadorToolStripComboBox1.Text;

            //int key = ConnFile.Save();
            //ConnFile.Load();
            AplicarConfiguracion();

            /*
            if (key == 2)
            {
                MessageBox.Show("error al guardar archivo de configuracion");
            }
            else
            {
                MessageBox.Show("Configuracion guardada con exito ");
            }
            */
        }
        // APLICA LO REGISTRADO EN UMBRAL E INDICADOR
        private void AplicarConfiguracion()
        {
            txtPesoUmbral.Text = ConnFile.UMBRAL;
            indicadorToolStripComboBox1.Text = ConnFile.INDICADOR;

            // Aquí asignas la configuración al lector real del indicador
            IndicadorActual = ConnFile.INDICADOR;
        }
        private void coneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenLogin();
        }
        private void btnLogin_Click(object sender, EventArgs e)
        {
            OpenLogin();
        }
        private void txtLoginKey_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                OpenLogin();
        }
        private void OpenLogin()
        {
            new Login().ShowDialog();
            if (configuracionMode)
            {
                conexiónToolStripMenuItem.Visible = true;
                parametrosToolStripMenuItem.Visible = true;
                configuraciónToolStripMenuItem.Visible = true;
                configDbToolStripMenuItem.Visible = true;
                guardarConfiguracionToolStripMenuItem.Enabled = true;
            }
            else
            {
                conexiónToolStripMenuItem.Visible = false;
                parametrosToolStripMenuItem.Visible = false;
                configuraciónToolStripMenuItem.Visible = false;
                configDbToolStripMenuItem.Visible = false;
                guardarConfiguracionToolStripMenuItem.Enabled = false;
            }
        }
        private void indicadorToolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = indicadorToolStripComboBox1.SelectedIndex;

            IndType = index;   // 0, 1, 2, 3 según el orden de tus items
            CfgSerialIndicador(IndType);
        }
        private void btnConfigDb_Click(object sender, EventArgs e)
        {
            new ConfigDb().ShowDialog();
        }
        private void configDbToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new ConfigDb().ShowDialog();
        }
        private void guardarConfiguracionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                using (MySqlConnection conn = Database.GetConnection())
                {
                    conn.Open();

                    string query = @"
                                     INSERT INTO CONFIGURACION_UMBRAL
                                    (COMBalanza, COMSemaforo, COMLector, Umbral, Indicador, Fecha, Usuario)
                                    VALUES(@combalanza, @comsemaforo, @comlector, @umbral, @indicador, NOW(), @usuarioid)";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@combalanza", toolStripComboBoxSerial2.Text);
                        cmd.Parameters.AddWithValue("@comsemaforo", toolStripComboBoxSerial1.Text);
                        cmd.Parameters.AddWithValue("@comlector", toolStripComboBoxSerial3.Text);
                        cmd.Parameters.AddWithValue("@umbral", txtPesoUmbral.Text);
                        cmd.Parameters.AddWithValue("@indicador", indicadorToolStripComboBox1.Text);
                        cmd.Parameters.AddWithValue("@usuarioid", ConnFile.USER_ID);

                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Configuración guardada en la base de datos.");
                conexiónToolStripMenuItem.Visible = false;
                parametrosToolStripMenuItem.Visible = false;
                configuraciónToolStripMenuItem.Visible = false;
                configDbToolStripMenuItem.Visible = false;
                guardarConfiguracionToolStripMenuItem.Enabled = false;
                configuracionMode = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar configuración: " + ex.Message);
            }
        }

        //======================INTERPRETACION DE DATA RECIBIDA POR PUERTOS SERIALES=============================================
        //========================================================================================================        
        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int lon, acc;
            int i, k;

            lon = serialPort1.BytesToRead;
            if (lon < 8)
                return;

            //lee bytes todos en total
            serialPort1.Read(rxdataModbus, 0, 8);

            //verifica que se encuentre dirreccion valida
            if (rxdataModbus[0] != 0x01)
            { return; }

            //hace chekeo de datos basico
            acc = 0;
            for (i = 0; i < 8; i++)
            {
                acc = rxdataModbus[i] + acc;
            }

            //verifica checksum, si no existe error reinicia timeout
            if (acc == 0x79)            //4bits out     79 8bits
            { timeoutModbus = 0; }


        }
        private void serialPort2_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {            
                if (IndType == 0) ReadWeight_TransCell();
                if (IndType == 1) ReadWeight_FT11();
                if (IndType == 2) ReadWeight_Sanfeng();
                if (IndType == 3) ReadWeight_PreciaMolen();

                serialPort2.DiscardInBuffer();           
        }
        private void serialPort3_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //deteccion de trama de lector QR
            try
            {
                string text = serialPort3.ReadLine();
                if (string.IsNullOrWhiteSpace(text))
                    return;

                text = text.Split('/').Last();

                try
                {
                    ID_detected = Convert.ToInt32(text);
                }
                catch { MessageBox.Show("codigo QR no valido","ERROR",MessageBoxButtons.OK, MessageBoxIcon.Error); }

                MessageBox.Show("Codigo QR detectado -> ID: " + text);
                QRevent = true;
                
            }
            catch (Exception ex)
            {
                return;
            }


        }
        //=================================INTERPRETACION DE TRAMAS DE INDICADORES=============================================
        //========================================================================================================        
        public void ReadWeight_TransCell()
        {
            try
            {
                string text = serialPort2.ReadLine();
                if (string.IsNullOrWhiteSpace(text))
                    return;

                int idx = text.IndexOf('\x02');
                if (idx < 0)
                    return;

                if (text.Length < idx + 10)
                    return;

                Trama = text.Substring(idx);
                if (Trama.Length < 10)
                    return;

                char signo = Trama[1];
                if (Trama.Length <= 9)
                    return;

                char codec = Trama[9];
                if (codec != 'k')
                    throw new Exception("Trama no contiene el código 'k' esperado.");

                if (Trama.Length < 2 + 7)
                    return;

                string rawPeso = Trama.Substring(2, 7).Trim();
                if (!int.TryParse(rawPeso, out int valor))
                    throw new Exception("No se pudo convertir el peso a número.");

                double y = valor;
                if (signo != ' ')
                    y = -y;

                timeoutSerial = 0;
                PesoStr = y.ToString("0");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error leyendo TransCell: " + ex.Message);
            }
        }
        public void ReadWeight_FT11()
        {
            try
            {
                string text = serialPort2.ReadExisting();
                if (string.IsNullOrWhiteSpace(text))
                    return;

                int idx = text.IndexOf('\x02');
                if (idx < 0)
                    return;

                if (text.Length < idx + 12)
                    return;

                Trama = text.Substring(idx);
                if (Trama.Length < 12)
                    return;

                string rawPeso = Trama.Substring(4, 6).Trim();
                if (!double.TryParse(rawPeso, out double pesoBase))
                    throw new Exception("No se pudo convertir el peso a número.");

                char[] codec = Trama.Substring(1, 2).ToCharArray();

                int regSigno = codec[1] & 0x02;
                int signo = (regSigno == 2) ? -1 : 1;

                int regDecimal = codec[0] & 0x07;
                int decimales = (regDecimal < 2) ? 0 : (regDecimal - 2);

                double divisor = Math.Pow(10, decimales);
                double y = (pesoBase * signo) / divisor;

                timeoutSerial = 0;
                PesoStr = y.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error leyendo FT11: " + ex.Message);
            }
        }
        public void ReadWeight_Sanfeng()
        {
            string text, rawPeso, unidad;
            int lon;
            double y;

            text = serialPort2.ReadExisting(); 
            lon = text.Length;

            if (lon < 12)
                return;

            //0123456789
            //wn039970kg{0D}{0A}            

            try
            {
                Trama = text.Substring(text.IndexOf('w'));

                rawPeso = Trama.Substring(2, 6);    //desde desde el tercer byte , 8 en total
                unidad = Trama.Substring(8, 2);
                if (unidad != "kg")
                    return;
                y = Double.Parse(rawPeso);      //convierte incluyendo signo
                timeoutSerial = 0;

                PesoStr = y.ToString();
            }

            catch
            {
                return;
            }

        }
        public void ReadWeight_PreciaMolen()
        {
            try
            {
                string text = serialPort2.ReadLine();
                if (string.IsNullOrWhiteSpace(text))
                    return;

                int idx = text.IndexOf('\x01');
                if (idx < 0)
                    throw new Exception("No se encontró SOH en la trama.");

                Trama = text.Substring(idx);
                if (Trama.Length < 43)
                    throw new Exception("Trama demasiado corta para Precia Molen.");

                char signo = Trama[4];
                string rawPeso = Trama.Substring(37, 6).Trim();
                if (!int.TryParse(rawPeso, out int valor))
                    throw new Exception("No se pudo convertir el peso a número.");

                double y = valor;
                if (signo != '0')
                    y = -y;

                timeoutSerial = 0;
                PesoStr = y.ToString("0");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error leyendo Precia Molen: " + ex.Message);
            }
        }
        //================================CIERRA POR COMPLETO LA APP=============================================
        //========================================================================================================        
        private void Main_FormClosed(object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }
        // CIERRA LOS PUERTOS COM
        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            CloseSerialPort1();
            CloseSerialPort2();
            CloseSerialPort3();
        }
            

        //======================== ALGORITMOS DE REGISTRO DE PESAJE ==============================================================
        //========================================================================================================================

        private void LimpiarCampos()
        {            
            txtId.Clear();
            txtTicket.Clear();
            txtPlaca.Clear();
            txtCliente.Clear();
            txtEstado.Clear();
            txtOperacion.Clear();
            txtProducto.Clear();
            txtPesoBruto.Clear();
            txtPesoTara.Clear();
            txtPesoNeto.Clear();
            txtPesoBruto.Clear();
            txtPesoTara.Clear();
            txtPesoNeto.Clear();
            panelRegistro.Enabled = false;
            panelPesos.Enabled = false;
        }

        private void llenar_campos()
        {            
            using (MySqlConnection conn = Database.GetConnection())
            {
                conn.Open();
                //string query = "SELECT Id, Ticket, Estado, Placa, Operacion, Cliente, Producto1, Producto2, Producto3, Producto4, Producto5, Cantidad, PesoIn1, PesoIn2, PesoIn3, PesoIn4, PesoIn5, PesoIn6, PesoNeto1, PesoNeto2, PesoNeto3, PesoNeto4, PesoNeto5 FROM registros WHERE Id=@id";
                string query = " SELECT  r.Id,  r.Ticket, r.Estado, c.Placa, r.Operacion, c.Cliente, r.Producto1,r.Producto2,r.Producto3,r.Producto4,r.Producto5,r.Cantidad,r.PesoIn1,r.PesoIn2,r.PesoIn3,r.PesoIn4,r.PesoIn5,r.PesoIn6,r.PesoNeto1,r.PesoNeto2, r.PesoNeto3, r.PesoNeto4,r.PesoNeto5 FROM REGISTROS r INNER JOIN CITAS c ON r.CitaId = c.Id " +
                                "WHERE r.Id = @id; ";

                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@id", ID_detected);

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            txtId.Text = reader["Id"].ToString();
                            txtTicket.Text = reader["Ticket"].ToString();
                            txtEstado.Text = reader["Estado"].ToString();
                            txtPlaca.Text = reader["Placa"].ToString();
                            txtOperacion.Text = reader["Operacion"].ToString();
                            txtCliente.Text = reader["Cliente"].ToString();
                            prd1 = reader["Producto1"].ToString();
                            prd2 = reader["Producto2"].ToString();
                            prd3 = reader["Producto3"].ToString();
                            prd4 = reader["Producto4"].ToString();
                            prd5 = reader["Producto5"].ToString();
                            cantidad = reader["Cantidad"].ToString();
                            peso1 = reader["PesoIn1"]?.ToString();
                            peso2 = reader["PesoIn2"]?.ToString();
                            peso3 = reader["PesoIn3"]?.ToString();
                            peso4 = reader["PesoIn4"]?.ToString();
                            peso5 = reader["PesoIn5"]?.ToString();
                            peso6 = reader["PesoIn6"]?.ToString();
                            pesoNeto1 = reader["PesoNeto1"]?.ToString();
                            pesoNeto2 = reader["PesoNeto2"]?.ToString();
                            pesoNeto3 = reader["PesoNeto3"]?.ToString();
                            pesoNeto4 = reader["PesoNeto4"]?.ToString();
                            pesoNeto5 = reader["PesoNeto5"]?.ToString();

                            switch(txtEstado.Text)
                            {
                                case "PRIMERA PESADA":
                                    if(txtOperacion.Text == "CARGA")
                                    { txtPesoTara.Text = peso1; }
                                    else
                                    { txtPesoBruto.Text = peso1; }
                                    break;
                                case "SEGUNDA PESADA":
                                    if (txtOperacion.Text == "CARGA")
                                    { txtPesoTara.Text = peso2; }
                                    else
                                    { txtPesoBruto.Text = peso2; }
                                    break;
                                case "TERCERA PESADA":
                                    if (txtOperacion.Text == "CARGA")
                                    { txtPesoTara.Text = peso3; }
                                    else
                                    { txtPesoBruto.Text = peso3; }
                                    break;
                                case "CUARTA PESADA":
                                    if (txtOperacion.Text == "CARGA")
                                    { txtPesoTara.Text = peso4; }
                                    else
                                    { txtPesoBruto.Text = peso4; }
                                    break;
                                case "QUINTA PESADA":
                                    if (txtOperacion.Text == "CARGA")
                                    { txtPesoTara.Text = peso5; }
                                    else
                                    { txtPesoBruto.Text = peso5; }
                                    break;
                                default: break;
                                        
                            }

                            //evalua la cantidad de productos
                            try { totalProductos = Convert.ToInt32(cantidad); }
                            catch { MessageBox.Show("la cantidad de productos en el registro no ha sido definida", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

                            if(totalProductos==0)
                            { MessageBox.Show("la cantidad de productos no puede ser 0", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                        
                            if(totalProductos==1)
                            {
                                if(txtOperacion.Text=="CARGA")
                                {
                                    txtPesoBruto.Text = peso2;
                                    txtPesoTara.Text = peso1;
                                }
                            }
                           
                        }
                        else
                        {
                            MessageBox.Show("No se encontró registros con el ID detectado");
                        }
                    }
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)      //acceso a ingreso manual de id
        {
            new vManualId().ShowDialog();
        }

        private void btnGuardarPeso_Click(object sender, EventArgs e)
        {
            int totalprd=0;
            int tara, bruto, neto;

            if (modoGuardarActivo)  //si se guardo sale, hasta que este habilitado por salida de camion
                return;

            if (string.IsNullOrWhiteSpace(txtId.Text))
            { MessageBox.Show("Ingrese ID primero."); return; }            

            if (timeoutSerial > 20)
            { MessageBox.Show("No se detecta trama de datos de peso"); return; }

            switch (txtEstado.Text)
            {
                case "INGRESO":
                    if(txtOperacion.Text == "CARGA")
                    { txtPesoTara.Text = PesoStr; }
                    else
                    { txtPesoBruto.Text = PesoStr; }
                    txtEstado.Text = "PRIMERA PESADA";
                    fechaPeso1 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    peso1 = PesoStr;
                    break;
                case "PRIMERA PESADA":
                    if (txtOperacion.Text == "CARGA")
                    { txtPesoTara.Text = peso1; txtPesoBruto.Text = PesoStr; }
                    else
                    { txtPesoBruto.Text = peso1; txtPesoTara.Text = PesoStr; }
                    try { bruto = Convert.ToInt32(txtPesoBruto.Text); tara = Convert.ToInt32(txtPesoTara.Text); neto = Math.Abs(bruto - tara); pesoNeto1 = neto.ToString(); }
                    catch { MessageBox.Show("No se puede calcular peso neto"); return; }
                    try 
                    { 
                        totalprd = Convert.ToInt32(cantidad);
                        txtEstado.Text = totalprd == 1 ? "PESAJE COMPLETADO" : "SEGUNDA PESADA";  
                    }                    
                    catch
                    { txtEstado.Text = "SEGUNDA PESADA"; }
                    fechaPeso2 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    peso2 = PesoStr;
                    txtPesoNeto.Text = neto.ToString();
                    txtProducto.Text = prd1;
                    break;

                case "SEGUNDA PESADA":
                    if (txtOperacion.Text == "CARGA")
                    { txtPesoTara.Text = peso2; txtPesoBruto.Text = PesoStr; }
                    else
                    { txtPesoBruto.Text = peso2; txtPesoTara.Text = PesoStr; }
                    try { bruto = Convert.ToInt32(txtPesoBruto.Text); tara = Convert.ToInt32(txtPesoTara.Text); neto = Math.Abs(bruto - tara); pesoNeto2 = neto.ToString(); }
                    catch { MessageBox.Show("No se puede calcular peso neto"); return; }
                    try
                    {
                        totalprd = Convert.ToInt32(cantidad);
                        txtEstado.Text = totalprd == 2 ? "PESAJE COMPLETADO" : "TERCERA PESADA";                        
                    }
                    catch { txtEstado.Text = "TERCERA PESADA"; }
                    fechaPeso3 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    peso3 = PesoStr;
                    txtPesoNeto.Text = neto.ToString();
                    txtProducto.Text = prd2;
                    break;

                case "TERCERA PESADA":
                    if (txtOperacion.Text == "CARGA")
                    { txtPesoTara.Text = peso3; txtPesoBruto.Text = PesoStr; }
                    else
                    { txtPesoBruto.Text = peso3; txtPesoTara.Text = PesoStr; }
                    try { bruto = Convert.ToInt32(txtPesoBruto.Text); tara = Convert.ToInt32(txtPesoTara.Text); neto = Math.Abs(bruto - tara); pesoNeto3 = neto.ToString(); }
                    catch { MessageBox.Show("No se puede calcular peso neto"); return; }
                    try
                    {
                        totalprd = Convert.ToInt32(cantidad);
                        txtEstado.Text = totalprd == 3 ? "PESAJE COMPLETADO" : "CUARTA PESADA";
                    }
                    catch { txtEstado.Text = "CUARTA PESADA"; }
                    fechaPeso4 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    peso4 = PesoStr;
                    txtPesoNeto.Text = neto.ToString();
                    txtProducto.Text = prd3;
                    break;

                case "CUARTA PESADA":
                    if (txtOperacion.Text == "CARGA")
                    { txtPesoTara.Text = peso4; txtPesoBruto.Text = PesoStr; }
                    else
                    { txtPesoBruto.Text = peso4; txtPesoTara.Text = PesoStr; }
                    try { bruto = Convert.ToInt32(txtPesoBruto.Text); tara = Convert.ToInt32(txtPesoTara.Text); neto = Math.Abs(bruto - tara); pesoNeto4 = neto.ToString(); }
                    catch { MessageBox.Show("No se puede calcular peso neto"); return; }
                    try
                    {
                        totalprd = Convert.ToInt32(cantidad);
                        txtEstado.Text = totalprd == 4 ? "PESAJE COMPLETADO" : "QUINTA PESADA";
                    }
                    catch { txtEstado.Text = "QUINTA PESADA"; }
                    fechaPeso5 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    peso5 = PesoStr;
                    txtPesoNeto.Text = neto.ToString();
                    txtProducto.Text = prd4;
                    break;

                case "QUINTA PESADA":
                    if (txtOperacion.Text == "CARGA")
                    { txtPesoTara.Text = peso5; txtPesoBruto.Text = PesoStr; }
                    else
                    { txtPesoBruto.Text = peso5; txtPesoTara.Text = PesoStr; }
                    try { bruto = Convert.ToInt32(txtPesoBruto.Text); tara = Convert.ToInt32(txtPesoTara.Text); neto = Math.Abs(bruto - tara); pesoNeto5 = neto.ToString(); }
                    catch { MessageBox.Show("No se puede calcular peso neto"); return; }
                    txtEstado.Text = "PESAJE COMPLETADO"; fechaPeso6 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); peso6 = PesoStr; txtPesoNeto.Text = neto.ToString(); txtProducto.Text = prd5;
                    break;

                default:
                    MessageBox.Show("Estado no valido para registrar peso"); return; ;                   
            }

            //procede a guardar en base de datos
            string query = "UPDATE registros SET PesoIn1=@p1,PesoIn2=@p2,PesoIn3=@p3,PesoIn4=@p4,PesoIn5=@p5,PesoIn6=@p6,Hora1=@hp1,Hora2=@hp2,Hora3=@hp3,Hora4=@hp4,Hora5=@hp5,Hora6=@hp6,PesoNeto1=@n1,PesoNeto2=@n2,PesoNeto3=@n3,PesoNeto4=@n4,PesoNeto5=@n5, Estado=@state WHERE Id=@id";

            using (MySqlConnection con = Database.GetConnection())
            using (var cmd = new MySqlCommand(query, con))
            {
                cmd.Parameters.AddWithValue("@p1", peso1);
                cmd.Parameters.AddWithValue("@p2", peso2);
                cmd.Parameters.AddWithValue("@p3", peso3);
                cmd.Parameters.AddWithValue("@p4", peso4);
                cmd.Parameters.AddWithValue("@p5", peso5);
                cmd.Parameters.AddWithValue("@p6", peso6);
                cmd.Parameters.AddWithValue("@hp1", fechaPeso1);
                cmd.Parameters.AddWithValue("@hp2", fechaPeso2);
                cmd.Parameters.AddWithValue("@hp3", fechaPeso3);
                cmd.Parameters.AddWithValue("@hp4", fechaPeso4);
                cmd.Parameters.AddWithValue("@hp5", fechaPeso5);
                cmd.Parameters.AddWithValue("@hp6", fechaPeso6);
                cmd.Parameters.AddWithValue("@n1", pesoNeto1);
                cmd.Parameters.AddWithValue("@n2", pesoNeto2);
                cmd.Parameters.AddWithValue("@n3", pesoNeto3);
                cmd.Parameters.AddWithValue("@n4", pesoNeto4);
                cmd.Parameters.AddWithValue("@n5", pesoNeto5);
                cmd.Parameters.AddWithValue("@state", txtEstado.Text);
                cmd.Parameters.AddWithValue("@id", txtId.Text);

                con.Open();
                cmd.ExecuteNonQuery();
            }

            //se guarda sin problemas
            modoGuardarActivo = true;
            panelRegistro.Enabled = true;
            panelPesos.Enabled = true;
            //MessageBox.Show("Peso de entrada guardado.");

        }
    }
    
}
