using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using CyberBot.Models;
using MySqlConnector;

namespace CyberBot.Services
{
    /// <summary>
    /// All MySQL access for the task assistant (Create, Read, Update, Delete).
    /// Every method is wrapped in error handling: if the database is down the app keeps running
    /// and <see cref="LastError"/> explains what went wrong.
    /// </summary>
    public class TaskRepository
    {
        private const string DefaultConnectionString =
            "Server=localhost;Port=3306;User ID=root;Password=;Database=cyberbot_db;Connection Timeout=5;";

        private const string CreateTableSql = @"
            CREATE TABLE IF NOT EXISTS Tasks (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                Title VARCHAR(150) NOT NULL,
                Description VARCHAR(500) NOT NULL,
                ReminderDate DATETIME NULL,
                IsCompleted TINYINT(1) NOT NULL DEFAULT 0,
                ReminderNotified TINYINT(1) NOT NULL DEFAULT 0,
                CreatedAt DATETIME NOT NULL
            );";

        private const string SelectColumns =
            "Id, Title, Description, ReminderDate, IsCompleted, ReminderNotified, CreatedAt";

        private readonly string _connectionString;
        private readonly string _serverConnectionString;
        private readonly string _databaseName;

        public TaskRepository()
        {
            string connection = LoadConnectionString();

            try
            {
                var builder = new MySqlConnectionStringBuilder(connection);
                _databaseName = builder.Database;
                builder.Database = string.Empty;           // connect to the server first, create the DB if needed
                _serverConnectionString = builder.ConnectionString;
                _connectionString = connection;
            }
            catch (Exception ex)
            {
                _databaseName = "cyberbot_db";
                _connectionString = DefaultConnectionString;
                _serverConnectionString = DefaultConnectionString.Replace("Database=cyberbot_db;", string.Empty);
                LastError = "Invalid connection string in appsettings.json: " + ex.Message;
            }
        }

        /// <summary>True once the database and table are ready to use.</summary>
        public bool IsAvailable { get; private set; }

        /// <summary>The most recent database error message (null if the last call succeeded).</summary>
        public string? LastError { get; private set; }

        // =========================================================
        // SET-UP
        // =========================================================

        /// <summary>Creates the database and Tasks table if they don't exist. Returns false if MySQL can't be reached.</summary>
        public bool Initialise()
        {
            try
            {
                if (!Regex.IsMatch(_databaseName, "^[A-Za-z0-9_]+$"))
                {
                    throw new InvalidOperationException("Database name may only contain letters, numbers and underscores.");
                }

                using (var server = new MySqlConnection(_serverConnectionString))
                {
                    server.Open();
                    using MySqlCommand createDb = server.CreateCommand();
                    createDb.CommandText = $"CREATE DATABASE IF NOT EXISTS `{_databaseName}` CHARACTER SET utf8mb4;";
                    createDb.ExecuteNonQuery();
                }

                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                using MySqlCommand createTable = connection.CreateCommand();
                createTable.CommandText = CreateTableSql;
                createTable.ExecuteNonQuery();

                IsAvailable = true;
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                LastError = ex.Message;
                return false;
            }
        }

        // =========================================================
        // CRUD
        // =========================================================

        /// <summary>CREATE - inserts a task and returns its new Id (0 on failure).</summary>
        public int Add(TaskItem task) => Run(connection =>
        {
            using var cmd = new MySqlCommand(
                "INSERT INTO Tasks (Title, Description, ReminderDate, IsCompleted, ReminderNotified, CreatedAt) " +
                "VALUES (@title, @description, @reminder, 0, 0, @created);", connection);

            cmd.Parameters.AddWithValue("@title", task.Title);
            cmd.Parameters.AddWithValue("@description", task.Description);
            cmd.Parameters.AddWithValue("@reminder", (object?)task.ReminderDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@created", task.CreatedAt);
            cmd.ExecuteNonQuery();

            return (int)cmd.LastInsertedId;
        }, 0);

        /// <summary>READ - all tasks, open ones first, newest first.</summary>
        public List<TaskItem> GetAll() => Run(connection =>
        {
            using var cmd = new MySqlCommand(
                $"SELECT {SelectColumns} FROM Tasks ORDER BY IsCompleted ASC, CreatedAt DESC;", connection);

            return ReadTasks(cmd);
        }, new List<TaskItem>());

        /// <summary>READ - a single task by Id (null if not found).</summary>
        public TaskItem? Get(int id) => Run(connection =>
        {
            using var cmd = new MySqlCommand($"SELECT {SelectColumns} FROM Tasks WHERE Id = @id;", connection);
            cmd.Parameters.AddWithValue("@id", id);

            return ReadTasks(cmd).FirstOrDefault();
        }, (TaskItem?)null);

        /// <summary>READ - tasks whose reminder time has passed and that haven't been announced yet.</summary>
        public List<TaskItem> GetDueReminders(DateTime now) => Run(connection =>
        {
            using var cmd = new MySqlCommand(
                $"SELECT {SelectColumns} FROM Tasks WHERE ReminderDate IS NOT NULL AND ReminderDate <= @now " +
                "AND IsCompleted = 0 AND ReminderNotified = 0 ORDER BY ReminderDate;", connection);
            cmd.Parameters.AddWithValue("@now", now);

            return ReadTasks(cmd);
        }, new List<TaskItem>());

        /// <summary>UPDATE - sets (or clears) the reminder for a task.</summary>
        public bool SetReminder(int id, DateTime? when) => Run(connection =>
        {
            using var cmd = new MySqlCommand(
                "UPDATE Tasks SET ReminderDate = @reminder, ReminderNotified = 0 WHERE Id = @id;", connection);
            cmd.Parameters.AddWithValue("@reminder", (object?)when ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", id);

            return cmd.ExecuteNonQuery() > 0;
        }, false);

        /// <summary>UPDATE - marks a task as completed.</summary>
        public bool MarkCompleted(int id) => Run(connection =>
        {
            using var cmd = new MySqlCommand("UPDATE Tasks SET IsCompleted = 1 WHERE Id = @id;", connection);
            cmd.Parameters.AddWithValue("@id", id);

            return cmd.ExecuteNonQuery() > 0;
        }, false);

        /// <summary>UPDATE - remembers that the reminder message was shown.</summary>
        public bool MarkNotified(int id) => Run(connection =>
        {
            using var cmd = new MySqlCommand("UPDATE Tasks SET ReminderNotified = 1 WHERE Id = @id;", connection);
            cmd.Parameters.AddWithValue("@id", id);

            return cmd.ExecuteNonQuery() > 0;
        }, false);

        /// <summary>DELETE - removes a task.</summary>
        public bool Delete(int id) => Run(connection =>
        {
            using var cmd = new MySqlCommand("DELETE FROM Tasks WHERE Id = @id;", connection);
            cmd.Parameters.AddWithValue("@id", id);

            return cmd.ExecuteNonQuery() > 0;
        }, false);

        // =========================================================
        // HELPERS
        // =========================================================

        /// <summary>
        /// Opens a connection, runs the delegate and returns its result.
        /// Any exception is caught and stored in LastError, and the fallback value is returned.
        /// </summary>
        private T Run<T>(Func<MySqlConnection, T> action, T fallback)
        {
            if (!IsAvailable)
            {
                return fallback;
            }

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();
                T result = action(connection);
                LastError = null;
                return result;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return fallback;
            }
        }

        private static List<TaskItem> ReadTasks(MySqlCommand cmd)
        {
            var tasks = new List<TaskItem>();

            using MySqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                tasks.Add(new TaskItem
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Description = reader.GetString(2),
                    ReminderDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                    IsCompleted = reader.GetBoolean(4),
                    ReminderNotified = reader.GetBoolean(5),
                    CreatedAt = reader.GetDateTime(6)
                });
            }

            return tasks;
        }

        /// <summary>Reads the connection string from appsettings.json (next to the .exe), or falls back to the default.</summary>
        private static string LoadConnectionString()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

                if (File.Exists(path))
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

                    if (document.RootElement.TryGetProperty("ConnectionStrings", out JsonElement section) &&
                        section.TryGetProperty("CyberBotDb", out JsonElement value) &&
                        !string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        return value.GetString()!;
                    }
                }
            }
            catch (Exception)
            {
                // fall through to the default
            }

            return DefaultConnectionString;
        }
    }
}
