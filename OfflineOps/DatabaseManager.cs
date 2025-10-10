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

                    string tableLoginToken = "login_token";
                    var columnsLoginToken = new Dictionary<string, string>
                    {
                        { "access_token", "TEXT NOT NULL" },
                        { "refresh_token", "TEXT NOT NULL" },
                    };
                    CreateTable(conn, tableLoginToken, columnsLoginToken);


                    string tableUsers = "users";

                    var columnsUsers = new Dictionary<string, string>
                    {
                        { "id", "INTEGER PRIMARY KEY" },
                        { "username", "TEXT" },
                        { "contact", "TEXT" },
                        { "balance", "TEXT DEFAULT '0'" },
                        { "aakda_total", "TEXT DEFAULT '0'" },
                        { "aakda_exposure", "TEXT DEFAULT '0'" },
                        { "pana_total", "TEXT DEFAULT '0'" },
                        { "pana_exposure", "TEXT DEFAULT '0'" },
                        { "group_pana_total", "TEXT DEFAULT '0'" },
                        { "group_pana_exposure", "TEXT DEFAULT '0'" },
                        { "jodi_total", "TEXT DEFAULT '0'" },
                        { "jodi_exposure", "TEXT DEFAULT '0'" },
                        { "credit_amt", "TEXT DEFAULT '0'" },
                        { "apc_amount", "TEXT DEFAULT '0'" },
                        { "profit_loss", "TEXT DEFAULT '0'" },
                        { "sync_date", "TEXT" },
                    };
                    CreateTable(conn, tableUsers, columnsUsers);


                    string tableUserGames = "user_games";
                    var columnsUserGames = new Dictionary<string, string>
                    {
                        { "user_id", "INTEGER NOT NULL" },
                        { "game_id", "INTEGER NOT NULL" },
                    };
                    CreateTable(conn, tableUserGames, columnsUserGames);


                    string tableBazar = "bazar";

                    var columnsBazar = new Dictionary<string, string>
                    {
                        { "id", "INTEGER PRIMARY KEY" },
                        { "bazar_unique", "TEXT" },
                        { "bazar_name", "TEXT" },
                        { "open_time", "TEXT" },
                        { "close_time", "TEXT" },
                        { "open_start_time", "TEXT" },
                        { "close_start_time", "TEXT" },
                        { "total_days", "TEXT" },
                        { "open_block_time", "TEXT" },
                        { "close_block_time", "TEXT" },
                        { "status", "TEXT" },
                        { "is_madhur_exp", "TEXT" },
                        { "start_status", "TEXT" },
                        { "admin_opentime", "TEXT" },
                        { "admin_closetime", "TEXT" },
                        { "wa_start_status", "TEXT" },                        
                        { "bazar_section_id", "TEXT" },                        
                        { "new_open_time", "TEXT" },                        
                        { "new_close_time", "TEXT" },                        
                        { "bazar_code", "TEXT" },                        
                        { "monday", "TEXT DEFAULT '0'" },                        
                        { "tuesday", "TEXT DEFAULT '0'" },                        
                        { "wednesday", "TEXT DEFAULT '0'" },                        
                        { "Thursday", "TEXT DEFAULT '0'" },                        
                        { "friday", "TEXT DEFAULT '0'" },                        
                        { "saturday", "TEXT DEFAULT '0'" },                        
                        { "sunday", "TEXT DEFAULT '0'" },                        
                    };
                    CreateTable(conn, tableBazar, columnsBazar);


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