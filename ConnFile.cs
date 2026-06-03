using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BlzSoftTerminal
{
    class ConnFile
    {
        public static string COM_SEMAFORO = "";
        public static string COM_BALANZA = "";
        public static string UMBRAL = "0";
        public static string COM_LECTOR = "";
        public static string INDICADOR = "0";

        public static int USER_ID = 0;

        private static readonly string fileName =
            Application.StartupPath + "\\" + "CfgPesaje";

        // ================================================================
        // GUARDAR
        // ================================================================
        public static int Save()
        {
            try
            {
                using (StreamWriter writer = File.CreateText(fileName))
                {
                    writer.WriteLine(COM_SEMAFORO);
                    writer.WriteLine(COM_BALANZA);
                    writer.WriteLine(UMBRAL);
                    writer.WriteLine(COM_LECTOR);
                    writer.WriteLine(INDICADOR);
                }

                return 0;
            }
            catch
            {
                MessageBox.Show("Error al guardar archivo de configuración.");
                return 2;
            }
        }

        // ================================================================
        // LEER
        // ================================================================
        public static int Load()
        {
            try
            {
                if (!File.Exists(fileName))
                {
                    COM_SEMAFORO = "COM9";
                    COM_BALANZA = "COM4";
                    UMBRAL = "30";
                    COM_LECTOR = "COM1";
                    INDICADOR = "0";
                    Save();
                    return 0;
                }

                using (StreamReader reader = File.OpenText(fileName))
                {
                    COM_SEMAFORO = reader.ReadLine();
                    COM_BALANZA = reader.ReadLine();
                    UMBRAL = reader.ReadLine();
                    COM_LECTOR = reader.ReadLine();
                    INDICADOR = reader.ReadLine(); 
                }

                return 0;
            }
            catch (Exception e)
            {
                MessageBox.Show("Error al leer archivo de configuración.\n" + e.Message);
                return 2;
            }
        }
    }
}
