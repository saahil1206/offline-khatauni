using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OfflineOps
{
    public class DatabaseManager
    {
        private static string dbFileName = $"{StaticVar.dataText}.{StaticVar.dataExt}";
        private static string dbPath = Path.Combine(Application.StartupPath, dbFileName);
        private static string connectionString = $"{StaticVar.dataSource}={dbPath}";

        public static string ConnectionString => connectionString;


        public static bool Initialize()
        {
            try
            {
                if (!File.Exists(dbPath))
                {
                    File.Create(dbPath).Dispose();
                }
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();

                    string tableName = "Users";

                    var columns = new Dictionary<string, string>
                    {
                        { "UserId", "INTEGER PRIMARY KEY AUTOINCREMENT" },
                        { "Username", "TEXT NOT NULL UNIQUE" },
                        { "Password", "TEXT NOT NULL" },
                        { "FullName", "TEXT" },
                        { "Email", "TEXT" },
                        { "Role", "TEXT DEFAULT 'User'" },
                        { "IsActive", "INTEGER DEFAULT 1" },
                        { "CreatedDate", "TEXT DEFAULT CURRENT_TIMESTAMP" },
                        { "LastLoginDate", "TEXT" },
                        { "MachineId", "TEXT" }
                    };
                    CreateTable(conn, tableName, columns);



                    conn.Close();
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database initialization error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }


        private static void CreateTable(SQLiteConnection conn, string tableName, Dictionary<string, string> columns, List<string> foreignKeys = null)
        {
            // Build CREATE TABLE query
            var columnDefinitions = columns.Select(kvp => $"{kvp.Key} {kvp.Value}");
            var allDefinitions = columnDefinitions.ToList();

            if (foreignKeys != null && foreignKeys.Count > 0)
            {
                allDefinitions.AddRange(foreignKeys);
            }

            string createTableQuery = $@"
                CREATE TABLE IF NOT EXISTS {tableName} (
                    {string.Join(",\n                    ", allDefinitions)}
                );";

            ExecuteNonQuery(conn, createTableQuery);

            // Check and add columns if they don't exist (skip primary key)
            foreach (var column in columns.Where(c => !c.Value.Contains("PRIMARY KEY")))
            {
                AddColumnIfNotExists(conn, tableName, column.Key, column.Value);
            }
        }

        private static void AddColumnIfNotExists(SQLiteConnection conn, string tableName, string columnName, string columnDefinition)
        {
            try
            {
                // Check if column exists
                string checkColumnQuery = $"PRAGMA table_info({tableName});";
                bool columnExists = false;

                using (SQLiteCommand cmd = new SQLiteCommand(checkColumnQuery, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["name"].ToString().Equals(columnName, StringComparison.OrdinalIgnoreCase))
                            {
                                columnExists = true;
                                break;
                            }
                        }
                    }
                }

                // Add column if it doesn't exist
                if (!columnExists)
                {
                    string alterTableQuery = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
                    ExecuteNonQuery(conn, alterTableQuery);
                    Console.WriteLine($"Column {columnName} added to {tableName} table.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding column {columnName} to {tableName}: {ex.Message}");
            }
        }

        private static void ExecuteNonQuery(SQLiteConnection conn, string query)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }

    public class DatabaseHelper
    {
        public SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(DatabaseManager.ConnectionString);
        }

        public DataTable Read(SQLiteCommand command)
        {
            DataTable dt = new DataTable();
            try
            {
                using (SQLiteConnection conn = GetConnection())
                {
                    conn.Open();
                    command.Connection = conn;
                    using (var reader = command.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Query execution error: {ex.Message}");
            }
            return dt;
        }

        public int Update(SQLiteCommand command)
        {
            int result = 0;
            try
            {
                using (SQLiteConnection conn = GetConnection())
                {
                    conn.Open();
                    command.Connection = conn;
                    result = command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Non-query execution error: {ex.Message}");
            }
            return result;
        }

        public object GetScalar(SQLiteCommand command)
        {
            object result = null;
            try
            {
                using (SQLiteConnection conn = GetConnection())
                {
                    conn.Open();
                    command.Connection = conn;
                    result = command.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Scalar execution error: {ex.Message}");
            }
            return result;
        }
    }
}