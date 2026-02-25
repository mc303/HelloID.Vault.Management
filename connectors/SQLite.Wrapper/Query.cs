using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SQLite.Wrapper
{
    public static class Query
    {
        private static bool _initialized = false;
        private static readonly object _initLock = new object();

        private static void EnsureInitialized()
        {
            if (_initialized) return;

            lock (_initLock)
            {
                if (_initialized) return;

                AssemblyLoader.Initialize();
                _initialized = true;
            }
        }

        #region Connection Helpers

        private static dynamic CreateConnection(string connectionString)
        {
            var sqliteAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name.Equals("Microsoft.Data.Sqlite", StringComparison.OrdinalIgnoreCase));

            if (sqliteAssembly == null)
                throw new InvalidOperationException("Microsoft.Data.Sqlite assembly not loaded");

            var connectionType = sqliteAssembly.GetType("Microsoft.Data.Sqlite.SqliteConnection");
            if (connectionType == null)
                throw new InvalidOperationException("SqliteConnection type not found");

            return Activator.CreateInstance(connectionType, connectionString);
        }

        private static dynamic CreateCommand(dynamic connection, string query, Dictionary<string, object> parameters)
        {
            var sqliteAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name.Equals("Microsoft.Data.Sqlite", StringComparison.OrdinalIgnoreCase));

            if (sqliteAssembly == null)
                throw new InvalidOperationException("Microsoft.Data.Sqlite assembly not loaded");

            var commandType = sqliteAssembly.GetType("Microsoft.Data.Sqlite.SqliteCommand");
            if (commandType == null)
                throw new InvalidOperationException("SqliteCommand type not found");

            dynamic command = Activator.CreateInstance(commandType);
            command.Connection = connection;
            command.CommandText = query;

            if (parameters != null && parameters.Count > 0)
            {
                foreach (var kvp in parameters)
                {
                    command.Parameters.AddWithValue(kvp.Key, kvp.Value ?? DBNull.Value);
                }
            }

            return command;
        }

        #endregion

        #region Read Operations

        public static DataTable Execute(string connectionString, string query)
        {
            return Execute(connectionString, query, null);
        }

        public static DataTable Execute(string connectionString, string query, Dictionary<string, object> parameters)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            dynamic connection = null;
            dynamic command = null;

            try
            {
                connection = CreateConnection(connectionString);
                connection.Open();

                command = CreateCommand(connection, query, parameters);

                var dataTable = new DataTable();

                using (var reader = command.ExecuteReader())
                {
                    dataTable.Load(reader);
                }

                return dataTable;
            }
            finally
            {
                if (command != null) { try { command.Dispose(); } catch { } }
                if (connection != null) { try { connection.Close(); } catch { } try { connection.Dispose(); } catch { } }
            }
        }

        public static object ExecuteScalar(string connectionString, string query)
        {
            return ExecuteScalar(connectionString, query, null);
        }

        public static object ExecuteScalar(string connectionString, string query, Dictionary<string, object> parameters)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            dynamic connection = null;
            dynamic command = null;

            try
            {
                connection = CreateConnection(connectionString);
                connection.Open();

                command = CreateCommand(connection, query, parameters);

                return command.ExecuteScalar();
            }
            finally
            {
                if (command != null) { try { command.Dispose(); } catch { } }
                if (connection != null) { try { connection.Close(); } catch { } try { connection.Dispose(); } catch { } }
            }
        }

        public static bool TestConnection(string connectionString)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                return false;

            try
            {
                dynamic connection = null;
                try
                {
                    connection = CreateConnection(connectionString);
                    connection.Open();
                    return true;
                }
                finally
                {
                    if (connection != null) { try { connection.Close(); } catch { } try { connection.Dispose(); } catch { } }
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Write Operations

        public static int ExecuteNonQuery(string connectionString, string query)
        {
            return ExecuteNonQuery(connectionString, query, null);
        }

        public static int ExecuteNonQuery(string connectionString, string query, Dictionary<string, object> parameters)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            dynamic connection = null;
            dynamic command = null;

            try
            {
                connection = CreateConnection(connectionString);
                connection.Open();

                command = CreateCommand(connection, query, parameters);

                return command.ExecuteNonQuery();
            }
            finally
            {
                if (command != null) { try { command.Dispose(); } catch { } }
                if (connection != null) { try { connection.Close(); } catch { } try { connection.Dispose(); } catch { } }
            }
        }

        public static int InsertBatch(string connectionString, string tableName, DataTable data, string[] keyColumns = null)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            if (data == null || data.Rows.Count == 0)
                return 0;

            int rowsInserted = 0;

            dynamic connection = null;
            dynamic command = null;

            try
            {
                connection = CreateConnection(connectionString);
                connection.Open();

                var columnNames = new List<string>();
                var paramNames = new List<string>();
                foreach (DataColumn col in data.Columns)
                {
                    columnNames.Add(col.ColumnName);
                    paramNames.Add("@" + col.ColumnName);
                }

                var insertSql = $"INSERT INTO {tableName} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)})";

                command = CreateCommand(connection, insertSql, null);

                foreach (DataRow row in data.Rows)
                {
                    command.Parameters.Clear();
                    foreach (DataColumn col in data.Columns)
                    {
                        var value = row[col];
                        command.Parameters.AddWithValue("@" + col.ColumnName, value ?? DBNull.Value);
                    }

                    rowsInserted += command.ExecuteNonQuery();
                }

                return rowsInserted;
            }
            finally
            {
                if (command != null) { try { command.Dispose(); } catch { } }
                if (connection != null) { try { connection.Close(); } catch { } try { connection.Dispose(); } catch { } }
            }
        }

        public static int UpdateBatch(string connectionString, string tableName, DataTable data, string[] keyColumns)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            if (data == null || data.Rows.Count == 0)
                return 0;

            if (keyColumns == null || keyColumns.Length == 0)
                throw new ArgumentException("Key columns must be specified for update", nameof(keyColumns));

            int rowsUpdated = 0;

            dynamic connection = null;
            dynamic command = null;

            try
            {
                connection = CreateConnection(connectionString);
                connection.Open();

                var keySet = new HashSet<string>(keyColumns, StringComparer.OrdinalIgnoreCase);
                var setClauses = new List<string>();
                var whereClauses = new List<string>();

                foreach (DataColumn col in data.Columns)
                {
                    if (keySet.Contains(col.ColumnName))
                    {
                        whereClauses.Add($"{col.ColumnName} = @{col.ColumnName}_key");
                    }
                    else
                    {
                        setClauses.Add($"{col.ColumnName} = @{col.ColumnName}");
                    }
                }

                var updateSql = $"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)}";

                command = CreateCommand(connection, updateSql, null);

                foreach (DataRow row in data.Rows)
                {
                    command.Parameters.Clear();

                    foreach (DataColumn col in data.Columns)
                    {
                        var value = row[col];
                        if (keySet.Contains(col.ColumnName))
                        {
                            command.Parameters.AddWithValue("@" + col.ColumnName + "_key", value ?? DBNull.Value);
                        }
                        command.Parameters.AddWithValue("@" + col.ColumnName, value ?? DBNull.Value);
                    }

                    rowsUpdated += command.ExecuteNonQuery();
                }

                return rowsUpdated;
            }
            finally
            {
                if (command != null) { try { command.Dispose(); } catch { } }
                if (connection != null) { try { connection.Close(); } catch { } try { connection.Dispose(); } catch { } }
            }
        }

        public static int DeleteBatch(string connectionString, string tableName, string keyColumn, object[] keys)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            if (string.IsNullOrWhiteSpace(keyColumn))
                throw new ArgumentNullException(nameof(keyColumn));

            if (keys == null || keys.Length == 0)
                return 0;

            dynamic connection = null;
            dynamic command = null;

            try
            {
                connection = CreateConnection(connectionString);
                connection.Open();

                var paramNames = new List<string>();
                for (int i = 0; i < keys.Length; i++)
                {
                    paramNames.Add($"@key{i}");
                }

                var deleteSql = $"DELETE FROM {tableName} WHERE {keyColumn} IN ({string.Join(", ", paramNames)})";

                command = CreateCommand(connection, deleteSql, null);

                for (int i = 0; i < keys.Length; i++)
                {
                    command.Parameters.AddWithValue($"@key{i}", keys[i] ?? DBNull.Value);
                }

                return command.ExecuteNonQuery();
            }
            finally
            {
                if (command != null) { try { command.Dispose(); } catch { } }
                if (connection != null) { try { connection.Close(); } catch { } try { connection.Dispose(); } catch { } }
            }
        }

        #endregion

        #region Transaction Support

        public static int ExecuteInTransaction(string connectionString, Action<dynamic> actions)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (actions == null)
                throw new ArgumentNullException(nameof(actions));

            dynamic connection = null;
            dynamic transaction = null;

            try
            {
                connection = CreateConnection(connectionString);
                connection.Open();

                transaction = connection.BeginTransaction();

                actions(transaction);

                transaction.Commit();
                return 1;
            }
            catch
            {
                if (transaction != null) { try { transaction.Rollback(); } catch { } }
                throw;
            }
            finally
            {
                if (transaction != null) { try { transaction.Dispose(); } catch { } }
                if (connection != null) { try { connection.Close(); } catch { } try { connection.Dispose(); } catch { } }
            }
        }

        public static int ExecuteNonQueryInTransaction(dynamic transaction, string query, Dictionary<string, object> parameters = null)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            dynamic command = null;

            try
            {
                var connection = transaction.Connection;
                command = CreateCommand(connection, query, parameters);
                command.Transaction = transaction;

                return command.ExecuteNonQuery();
            }
            finally
            {
                if (command != null) { try { command.Dispose(); } catch { } }
            }
        }

        public static object ExecuteScalarInTransaction(dynamic transaction, string query, Dictionary<string, object> parameters = null)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            dynamic command = null;

            try
            {
                var connection = transaction.Connection;
                command = CreateCommand(connection, query, parameters);
                command.Transaction = transaction;

                return command.ExecuteScalar();
            }
            finally
            {
                if (command != null) { try { command.Dispose(); } catch { } }
            }
        }

        #endregion

        #region Convenience Methods

        public static int UpdateField(string connectionString, string tableName, string fieldName, object value, string keyColumn, object keyValue)
        {
            return UpdateField(connectionString, tableName, fieldName, value, keyColumn, keyValue, null);
        }

        public static int UpdateField(string connectionString, string tableName, string fieldName, object value, string keyColumn, object keyValue, string additionalWhere)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentNullException(nameof(fieldName));

            if (string.IsNullOrWhiteSpace(keyColumn))
                throw new ArgumentNullException(nameof(keyColumn));

            var whereClause = $"{keyColumn} = @keyValue";
            if (!string.IsNullOrWhiteSpace(additionalWhere))
            {
                whereClause += " " + additionalWhere;
            }

            var sql = $"UPDATE {tableName} SET {fieldName} = @value WHERE {whereClause}";

            var parameters = new Dictionary<string, object>
            {
                { "value", value ?? DBNull.Value },
                { "keyValue", keyValue ?? DBNull.Value }
            };

            return ExecuteNonQuery(connectionString, sql, parameters);
        }

        public static int UpdateFields(string connectionString, string tableName, Dictionary<string, object> fields, string keyColumn, object keyValue)
        {
            return UpdateFields(connectionString, tableName, fields, keyColumn, keyValue, null);
        }

        public static int UpdateFields(string connectionString, string tableName, Dictionary<string, object> fields, string keyColumn, object keyValue, string additionalWhere)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            if (fields == null || fields.Count == 0)
                throw new ArgumentException("At least one field must be specified", nameof(fields));

            if (string.IsNullOrWhiteSpace(keyColumn))
                throw new ArgumentNullException(nameof(keyColumn));

            var setClauses = new List<string>();
            var parameters = new Dictionary<string, object>();

            foreach (var field in fields)
            {
                setClauses.Add($"{field.Key} = @{field.Key}");
                parameters[field.Key] = field.Value ?? DBNull.Value;
            }

            var whereClause = $"{keyColumn} = @keyValue";
            if (!string.IsNullOrWhiteSpace(additionalWhere))
            {
                whereClause += " " + additionalWhere;
            }

            parameters["keyValue"] = keyValue ?? DBNull.Value;

            var sql = $"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {whereClause}";

            return ExecuteNonQuery(connectionString, sql, parameters);
        }

        public static int UpsertField(string connectionString, string tableName, string fieldName, object value, string keyColumn, object keyValue)
        {
            return UpsertField(connectionString, tableName, fieldName, value, keyColumn, keyValue, null);
        }

        public static int UpsertField(string connectionString, string tableName, string fieldName, object value, string keyColumn, object keyValue, string additionalWhere)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentNullException(nameof(fieldName));

            if (string.IsNullOrWhiteSpace(keyColumn))
                throw new ArgumentNullException(nameof(keyColumn));

            var sql = $@"
INSERT INTO {tableName} ({keyColumn}, {fieldName}) 
VALUES (@keyValue, @value)
ON CONFLICT({keyColumn}) DO UPDATE SET {fieldName} = excluded.{fieldName}";

            var parameters = new Dictionary<string, object>
            {
                { "value", value ?? DBNull.Value },
                { "keyValue", keyValue ?? DBNull.Value }
            };

            return ExecuteNonQuery(connectionString, sql, parameters);
        }

        public static int UpsertFields(string connectionString, string tableName, Dictionary<string, object> fields, string keyColumn, object keyValue)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            if (fields == null || fields.Count == 0)
                throw new ArgumentException("At least one field must be specified", nameof(fields));

            if (string.IsNullOrWhiteSpace(keyColumn))
                throw new ArgumentNullException(nameof(keyColumn));

            var allFields = new Dictionary<string, object>(fields)
            {
                [keyColumn] = keyValue ?? DBNull.Value
            };

            var columnNames = string.Join(", ", allFields.Keys);
            var paramNames = string.Join(", ", allFields.Keys.Select(k => "@" + k));
            var updateClauses = fields.Keys.Select(f => $"{f} = excluded.{f}");

            var sql = $@"
INSERT INTO {tableName} ({columnNames}) 
VALUES ({paramNames})
ON CONFLICT({keyColumn}) DO UPDATE SET {string.Join(", ", updateClauses)}";

            var parameters = new Dictionary<string, object>();
            foreach (var field in allFields)
            {
                parameters[field.Key] = field.Value ?? DBNull.Value;
            }

            return ExecuteNonQuery(connectionString, sql, parameters);
        }

        public static int UpsertFieldsWithWhere(string connectionString, string tableName, Dictionary<string, object> fields, string keyColumn, object keyValue, string whereColumn, object whereValue)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            if (fields == null || fields.Count == 0)
                throw new ArgumentException("At least one field must be specified", nameof(fields));

            if (string.IsNullOrWhiteSpace(keyColumn))
                throw new ArgumentNullException(nameof(keyColumn));

            if (string.IsNullOrWhiteSpace(whereColumn))
                throw new ArgumentNullException(nameof(whereColumn));

            var allFields = new Dictionary<string, object>(fields)
            {
                [keyColumn] = keyValue ?? DBNull.Value,
                [whereColumn] = whereValue ?? DBNull.Value
            };

            var columnNames = string.Join(", ", allFields.Keys);
            var paramNames = string.Join(", ", allFields.Keys.Select(k => "@" + k));
            var updateClauses = fields.Keys.Select(f => $"{f} = excluded.{f}");

            var sql = $@"
INSERT INTO {tableName} ({columnNames}) 
VALUES ({paramNames})
ON CONFLICT({keyColumn}, {whereColumn}) DO UPDATE SET {string.Join(", ", updateClauses)}";

            var parameters = new Dictionary<string, object>();
            foreach (var field in allFields)
            {
                parameters[field.Key] = field.Value ?? DBNull.Value;
            }

            return ExecuteNonQuery(connectionString, sql, parameters);
        }

        public static int Insert(string connectionString, string tableName, Dictionary<string, object> fields)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            if (fields == null || fields.Count == 0)
                throw new ArgumentException("At least one field must be specified", nameof(fields));

            var columnNames = string.Join(", ", fields.Keys);
            var paramNames = string.Join(", ", fields.Keys.Select(k => "@" + k));

            var sql = $"INSERT INTO {tableName} ({columnNames}) VALUES ({paramNames})";

            var parameters = new Dictionary<string, object>();
            foreach (var field in fields)
            {
                parameters[field.Key] = field.Value ?? DBNull.Value;
            }

            return ExecuteNonQuery(connectionString, sql, parameters);
        }

        public static int Delete(string connectionString, string tableName, string keyColumn, object keyValue)
        {
            return Delete(connectionString, tableName, keyColumn, keyValue, null);
        }

        public static int Delete(string connectionString, string tableName, string keyColumn, object keyValue, string additionalWhere)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            if (string.IsNullOrWhiteSpace(keyColumn))
                throw new ArgumentNullException(nameof(keyColumn));

            var whereClause = $"{keyColumn} = @keyValue";
            if (!string.IsNullOrWhiteSpace(additionalWhere))
            {
                whereClause += " " + additionalWhere;
            }

            var sql = $"DELETE FROM {tableName} WHERE {whereClause}";

            var parameters = new Dictionary<string, object>
            {
                { "keyValue", keyValue ?? DBNull.Value }
            };

            return ExecuteNonQuery(connectionString, sql, parameters);
        }

        public static bool Exists(string connectionString, string tableName, string keyColumn, object keyValue)
        {
            return Exists(connectionString, tableName, keyColumn, keyValue, null);
        }

        public static bool Exists(string connectionString, string tableName, string keyColumn, object keyValue, string additionalWhere)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            if (string.IsNullOrWhiteSpace(keyColumn))
                throw new ArgumentNullException(nameof(keyColumn));

            var whereClause = $"{keyColumn} = @keyValue";
            if (!string.IsNullOrWhiteSpace(additionalWhere))
            {
                whereClause += " " + additionalWhere;
            }

            var sql = $"SELECT 1 FROM {tableName} WHERE {whereClause} LIMIT 1";

            var parameters = new Dictionary<string, object>
            {
                { "keyValue", keyValue ?? DBNull.Value }
            };

            var result = ExecuteScalar(connectionString, sql, parameters);
            return result != null && result != DBNull.Value;
        }

        public static T GetValue<T>(string connectionString, string tableName, string fieldName, string keyColumn, object keyValue)
        {
            return GetValue<T>(connectionString, tableName, fieldName, keyColumn, keyValue, null, default(T));
        }

        public static T GetValue<T>(string connectionString, string tableName, string fieldName, string keyColumn, object keyValue, string additionalWhere, T defaultValue)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentNullException(nameof(fieldName));

            if (string.IsNullOrWhiteSpace(keyColumn))
                throw new ArgumentNullException(nameof(keyColumn));

            var whereClause = $"{keyColumn} = @keyValue";
            if (!string.IsNullOrWhiteSpace(additionalWhere))
            {
                whereClause += " " + additionalWhere;
            }

            var sql = $"SELECT {fieldName} FROM {tableName} WHERE {whereClause} LIMIT 1";

            var parameters = new Dictionary<string, object>
            {
                { "keyValue", keyValue ?? DBNull.Value }
            };

            var result = ExecuteScalar(connectionString, sql, parameters);

            if (result == null || result == DBNull.Value)
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(result, typeof(T));
        }

        #endregion
    }
}
