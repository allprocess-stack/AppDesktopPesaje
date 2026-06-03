using System.IO;
using System.Web.Script.Serialization;

namespace BlzSoftTerminal
{
    public class DbConfig
    {
        public string Server { get; set; } = "";
        public string Database { get; set; } = "";
        public string UserId { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public static class ConfigManager
    {
        private static readonly string filePath =
            Path.Combine(System.Windows.Forms.Application.StartupPath, "config.json");

        public static DbConfig Load()
        {
            try
            {
                if (!File.Exists(filePath))
                    return new DbConfig();

                string json = File.ReadAllText(filePath);
                return new JavaScriptSerializer().Deserialize<DbConfig>(json);
            }
            catch
            {
                return new DbConfig();
            }
        }

        public static void Save(DbConfig config)
        {
            string json = new JavaScriptSerializer().Serialize(config);
            File.WriteAllText(filePath, json);
        }
    }
}
