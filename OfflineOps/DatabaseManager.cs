using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
                        { "opening_credit", "TEXT DEFAULT '0'" },
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

                    string tableCommands = "li_com";

                    var columnsCommands = new Dictionary<string, string>
                    {
                        { "com_id", "BIGINT PRIMARY KEY" },
                        { "com_display_name", "TEXT" },
                        { "com_name", "TEXT" },
                        { "com_function", "TEXT" },
                        { "game_name", "TEXT" },
                        { "com_min_no", "TEXT" },
                        { "com_max_no", "TEXT" },
                        { "com_status", "TEXT DEFAULT '0'" },

                    };
                    CreateTable(conn, tableCommands, columnsCommands);
                    
                    string tableLiPana = "li_pana";

                    var columnsLiPana = new Dictionary<string, string>
                    {
                        { "id", "BIGINT PRIMARY KEY" },
                        { "pana", "TEXT" },
                        { "group_id", "TEXT" },
                        { "number_id", "TEXT" },
                        { "number_main", "TEXT" },
                        { "status", "TEXT DEFAULT '0'" },
                        { "pana_type", "TEXT" },
                        { "check_motor", "TEXT" },
                        { "check_family", "TEXT" },
                        { "check_chipke_bikhre", "TEXT" },
                        { "check_run", "TEXT" },
                        { "check_center", "TEXT" },
                        { "check_forgot", "TEXT" },

                    };
                    CreateTable(conn, tableLiPana, columnsLiPana);


                    string tableBazar = "bazar";

                    var columnsBazar = new Dictionary<string, string>
                    {
                        { "id", "BIGINT PRIMARY KEY" },
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

                    string tableGroupTrans = "group_trans";

                    var columnsGroupTrans = new Dictionary<string, string>
                    {
                        { "id", "TEXT PRIMARY KEY" },
                        { "bet_id", "TEXT" },
                        { "user_id", "BIGINT" },
                        { "bazar_id", "BIGINT" },
                        { "bazar_cat", "TEXT" },
                        { "game_name", "TEXT" },
                        { "game_test_name", "TEXT" },
                        { "aakda_no", "TEXT" },
                        { "pana_no", "TEXT" },
                        { "amount", "NUMERIC" },
                        { "total_amount", "NUMERIC" },
                        { "server_flag", "BIT DEFAULT 0" },
                        { "cancel_status", "BIT DEFAULT 0" },
                        { "game_date", "TEXT" },
                        { "upload_date", "TEXT" },
                        { "created_date", "TEXT" },
                    };
                    CreateTable(conn, tableGroupTrans, columnsGroupTrans);

                    string tableConsoleStr = "bet_request";

                    var columnsConsoleStr = new Dictionary<string, string>
                    {
                        { "id", "TEXT PRIMARY KEY" },
                        { "user_id", "BIGINT" },
                        { "bazar_id", "BIGINT" },
                        { "bazar_cat", "TEXT" },
                        { "bet_str", "TEXT" },
                        { "total_amount", "NUMERIC" },
                        { "game_date", "TEXT" },
                        { "server_flag", "BIT DEFAULT 0" },
                        { "upload_date", "TEXT" },
                        { "created_date", "TEXT" },
                    };
                    CreateTable(conn, tableConsoleStr, columnsConsoleStr);

                    string tableSingleDigit = "single_digit";

                    var columnsSingleDigit = new Dictionary<string, string>
                    {
                        { "id", "TEXT PRIMARY KEY" },
                        { "bet_id", "TEXT" },
                        { "user_id", "BIGINT" },
                        { "bazar_id", "BIGINT" },
                        { "bazar_cat", "TEXT" },
                        { "single0", "NUMERIC" },
                        { "single1", "NUMERIC" },
                        { "single2", "NUMERIC" },
                        { "single3", "NUMERIC" },
                        { "single4", "NUMERIC" },
                        { "single5", "NUMERIC" },
                        { "single6", "NUMERIC" },
                        { "single7", "NUMERIC" },
                        { "single8", "NUMERIC" },
                        { "single9", "NUMERIC" },
                        { "amount", "NUMERIC" },
                        { "server_flag", "BIT DEFAULT 0" },
                        { "cancel_status", "BIT DEFAULT 0" },
                        { "game_date", "TEXT" },
                        { "upload_date", "TEXT" },
                        { "created_date", "TEXT" },
                    };
                    CreateTable(conn, tableSingleDigit, columnsSingleDigit);

                    string tablePana = "pana";

                    var columnsPana = new Dictionary<string, string>
                    {
                        { "id", "TEXT PRIMARY KEY" },
                        { "group_id", "BIGINT" },
                        { "user_id", "BIGINT" },
                        { "bazar_id", "BIGINT" },
                        { "bazar_cat", "TEXT" },
                        { "pana", "NUMERIC" },
                        { "amount", "NUMERIC" },
                        { "server_flag", "BIT DEFAULT 0" },
                        { "cancel_status", "BIT DEFAULT 0" },
                        { "game_date", "TEXT" },
                        { "upload_date", "TEXT" },
                        { "created_date", "TEXT" },
                    };
                    CreateTable(conn, tablePana, columnsPana);

                    string tableJodi = "jodi";

                    var columnsJodi = new Dictionary<string, string>
                    {
                        { "id", "TEXT PRIMARY KEY" },
                        { "group_id", "BIGINT" },
                        { "user_id", "BIGINT" },
                        { "bazar_id", "BIGINT" },
                        { "bazar_cat", "TEXT" },
                        { "jodi", "NUMERIC" },
                        { "amount", "NUMERIC" },
                        { "server_flag", "BIT DEFAULT 0" },
                        { "cancel_status", "BIT DEFAULT 0" },
                        { "game_date", "TEXT" },
                        { "upload_date", "TEXT" },
                        { "created_date", "TEXT" },
                    };
                    CreateTable(conn, tableJodi, columnsJodi);


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

    public class DatabaseHelper : IDisposable
    {
        private readonly SQLiteConnection _connection;
        private SQLiteTransaction _transaction;

        public DatabaseHelper()
        {
            _connection = new SQLiteConnection(DatabaseManager.ConnectionString);
            _connection.Open();

            using (var cmd = new SQLiteCommand("PRAGMA journal_mode = WAL;", _connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public void BeginTransaction()
        {
            _transaction = _connection.BeginTransaction();
        }

        public void Commit()
        {
            if (_transaction != null)
            {
                _transaction.Commit();
                _transaction.Dispose();
                _transaction = null;
            }
        }

        public void Rollback()
        {
            if (_transaction != null)
            {
                _transaction.Rollback();
                _transaction.Dispose();
                _transaction = null;
            }
        }

        public DataTable Read(SQLiteCommand command)
        {
            command.Connection = _connection;
            if (_transaction != null) command.Transaction = _transaction;

            using (var reader = command.ExecuteReader())
            {
                DataTable dt = new DataTable();
                dt.Load(reader);
                return dt;
            }
        }

        public int Update(SQLiteCommand command)
        {
            command.Connection = _connection;
            if (_transaction != null) command.Transaction = _transaction;

            return command.ExecuteNonQuery();
        }

        public object GetScalar(SQLiteCommand command)
        {
            command.Connection = _connection;
            if (_transaction != null) command.Transaction = _transaction;

            return command.ExecuteScalar();
        }

        public void Dispose()
        {
            try { Commit(); } catch { Rollback(); }

            if (_connection.State == ConnectionState.Open)
            {
                _connection.Close();
            }

            _connection.Dispose();
        }
    }

}