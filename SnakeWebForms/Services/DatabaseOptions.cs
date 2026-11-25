using System;
using Microsoft.Extensions.Configuration;

namespace SnakeWebForms.Services
{
    /// <summary>
    /// Central configuration holder for future database connectivity (e.g., PostgreSQL on Railway/Render).
    /// Reads DATABASE_URL first, then ConnectionStrings:DefaultConnection.
    /// </summary>
    public class DatabaseOptions
    {
        public string ConnectionString { get; }
        public string DataDir { get; }
        public bool IsDatabaseConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

        public DatabaseOptions(IConfiguration configuration)
        {
            ConnectionString =
                Environment.GetEnvironmentVariable("DATABASE_URL")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? string.Empty;

            DataDir =
                Environment.GetEnvironmentVariable("DATA_DIR")
                ?? configuration["Storage:DataDir"]
                ?? "App_Data";
        }
    }
}
