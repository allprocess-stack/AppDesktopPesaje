using MySql.Data.MySqlClient;

namespace BlzSoftTerminal
{
    public static class Database
    {
        public static MySqlConnection GetConnection()
        {
            DbConfig cfg = ConfigManager.Load();
            string connectionString = $"Server={cfg.Server};Database={cfg.Database};User={cfg.UserId};Pwd={cfg.Password};";
            return new MySqlConnection(connectionString);
        }
    }
}
