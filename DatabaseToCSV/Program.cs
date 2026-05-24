using FirebirdSql.Data.FirebirdClient;
using MySql.Data.MySqlClient;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseToCSV
{
    class Program
    {
        private const int TIMEOUT_MILESSECONDS = 90000;
        private static string queryFile = "";
        private static string logFile = "";

        private enum eDatabase
        {
            SQLServer = 1,
            Sqlite = 2,
            Firebird = 3,
            Oracle = 4,
            MySql = 5,
            Access = 6,
            DB2 = 7,
            PostgreSql = 8
        }

        static void Main(string[] args)
        {
            int rowNumber = 0;
            int columnNumber = 0;

            try
            {
                if (!Validate(args)) return;

                queryFile = args[0];
                string outputFile = args[1];
                string connectionString = args[2];
                eDatabase database = (eDatabase)Convert.ToInt32(args[3]);

                // Definir arquivo de log baseado no nome do arquivo SQL
                logFile = Path.GetFileNameWithoutExtension(queryFile) + ".error";

                // Mostrar apenas o nome do arquivo sendo processado
                Console.WriteLine(Path.GetFileName(queryFile));

                using (var connection = GetConnection(database, connectionString))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = File.ReadAllText(queryFile, Encoding.GetEncoding("ISO-8859-1"));
                        command.Connection = connection;
                        command.CommandTimeout = TIMEOUT_MILESSECONDS;

                        using (var dr = command.ExecuteReader())
                        {
                            // Verificar se é Parquet pela extensão
                            if (outputFile.ToLower().EndsWith(".parquet"))
                            {
                                ExportToParquet(dr, outputFile).GetAwaiter().GetResult();
                            }
                            else
                            {
                                // CSV padrão
                                using (var sw = new StreamWriter(outputFile, false, Encoding.UTF8))
                                {
                                    // Escrever cabeçalho
                                    for (int i = 0; i < dr.FieldCount; i++)
                                    {
                                        if (i != 0) sw.Write(";");
                                        sw.Write(CleanField(dr.GetName(i)));
                                    }
                                    sw.Write("\n");

                                    // Escrever dados
                                    while (dr.Read())
                                    {
                                        rowNumber++;

                                        for (columnNumber = 0; columnNumber < dr.FieldCount; columnNumber++)
                                        {
                                            if (columnNumber != 0) sw.Write(";");
                                            sw.Write(CleanField(dr[columnNumber].ToString()));
                                        }

                                        sw.Write("\n");
                                    }
                                }
                            }
                        }
                    }
                }

                // Se chegou aqui sem erros, deleta arquivo de erro se existir
                if (File.Exists(logFile))
                    File.Delete(logFile);
            }
            catch (Exception ex)
            {
                // Gravar erro no arquivo específico do SQL
                string errorMessage = string.Format("[{0}] Error on row {1}, column {2}.\nError message: {3}\nDetails: {4}\n\n",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    rowNumber.ToString(),
                    columnNumber.ToString(),
                    ex.Message,
                    ex.ToString());

                File.WriteAllText(logFile, errorMessage, Encoding.UTF8);
                Environment.ExitCode = 1;
            }
        }

        private static async Task ExportToParquet(DbDataReader dr, string parquetFile)
        {
            // Coletar dados por coluna
            var columnNames = new List<string>();
            var columnData = new List<List<object>>();
            var columnTypes = new List<Type>();

            // Inicializar listas
            for (int i = 0; i < dr.FieldCount; i++)
            {
                columnNames.Add(dr.GetName(i));
                columnData.Add(new List<object>());
                columnTypes.Add(typeof(string)); // Placeholder
            }

            // Ler todos os dados e detectar tipos
            while (dr.Read())
            {
                for (int i = 0; i < dr.FieldCount; i++)
                {
                    var value = dr[i];
                    columnData[i].Add(value);

                    // Detectar tipo não-nulo
                    if (value != null && value != DBNull.Value && columnTypes[i] == typeof(string))
                    {
                        columnTypes[i] = value.GetType();
                    }
                }
            }

            // Primeiro: criar o schema com todos os campos
            var fields = new List<Field>();

            for (int i = 0; i < columnNames.Count; i++)
            {
                Type columnType = columnTypes[i];

                if (columnType == typeof(int) || columnType == typeof(Int32))
                {
                    fields.Add(new DataField<int?>(columnNames[i]));
                }
                else if (columnType == typeof(long) || columnType == typeof(Int64))
                {
                    fields.Add(new DataField<long?>(columnNames[i]));
                }
                else if (columnType == typeof(float))
                {
                    fields.Add(new DataField<float?>(columnNames[i]));
                }
                else if (columnType == typeof(double))
                {
                    fields.Add(new DataField<double?>(columnNames[i]));
                }
                else if (columnType == typeof(decimal))
                {
                    fields.Add(new DataField<decimal?>(columnNames[i]));
                }
                else if (columnType == typeof(DateTime))
                {
                    fields.Add(new DataField<DateTime?>(columnNames[i]));
                }
                else if (columnType == typeof(bool))
                {
                    fields.Add(new DataField<bool?>(columnNames[i]));
                }
                else
                {
                    fields.Add(new DataField<string>(columnNames[i]));
                }
            }

            var schema = new Parquet.Schema.ParquetSchema(fields);

            // Segundo: preparar os dados para escrita usando métodos assíncronos
            using (Stream fileStream = File.Create(parquetFile))
            using (ParquetWriter writer = await ParquetWriter.CreateAsync(schema, fileStream))
            {
                // Usar compressão padrão (funciona com IronCompress 1.7.0)
                writer.CompressionMethod = CompressionMethod.Snappy;

                using (ParquetRowGroupWriter groupWriter = writer.CreateRowGroup())
                {
                    // Para cada coluna, criar o DataColumn e escrever
                    for (int i = 0; i < columnNames.Count; i++)
                    {
                        Type columnType = columnTypes[i];

                        if (columnType == typeof(int) || columnType == typeof(Int32))
                        {
                            var values = new int?[columnData[i].Count];
                            for (int j = 0; j < columnData[i].Count; j++)
                            {
                                var v = columnData[i][j];
                                values[j] = (v != null && v != DBNull.Value) ? Convert.ToInt32(v) : (int?)null;
                            }
                            var dataColumn = new DataColumn((DataField<int?>)fields[i], values);
                            await groupWriter.WriteColumnAsync(dataColumn);
                        }
                        else if (columnType == typeof(long) || columnType == typeof(Int64))
                        {
                            var values = new long?[columnData[i].Count];
                            for (int j = 0; j < columnData[i].Count; j++)
                            {
                                var v = columnData[i][j];
                                values[j] = (v != null && v != DBNull.Value) ? Convert.ToInt64(v) : (long?)null;
                            }
                            var dataColumn = new DataColumn((DataField<long?>)fields[i], values);
                            await groupWriter.WriteColumnAsync(dataColumn);
                        }
                        else if (columnType == typeof(float))
                        {
                            var values = new float?[columnData[i].Count];
                            for (int j = 0; j < columnData[i].Count; j++)
                            {
                                var v = columnData[i][j];
                                values[j] = (v != null && v != DBNull.Value) ? Convert.ToSingle(v) : (float?)null;
                            }
                            var dataColumn = new DataColumn((DataField<float?>)fields[i], values);
                            await groupWriter.WriteColumnAsync(dataColumn);
                        }
                        else if (columnType == typeof(double))
                        {
                            var values = new double?[columnData[i].Count];
                            for (int j = 0; j < columnData[i].Count; j++)
                            {
                                var v = columnData[i][j];
                                values[j] = (v != null && v != DBNull.Value) ? Convert.ToDouble(v) : (double?)null;
                            }
                            var dataColumn = new DataColumn((DataField<double?>)fields[i], values);
                            await groupWriter.WriteColumnAsync(dataColumn);
                        }
                        else if (columnType == typeof(decimal))
                        {
                            var values = new decimal?[columnData[i].Count];
                            for (int j = 0; j < columnData[i].Count; j++)
                            {
                                var v = columnData[i][j];
                                values[j] = (v != null && v != DBNull.Value) ? Convert.ToDecimal(v) : (decimal?)null;
                            }
                            var dataColumn = new DataColumn((DataField<decimal?>)fields[i], values);
                            await groupWriter.WriteColumnAsync(dataColumn);
                        }
                        else if (columnType == typeof(DateTime))
                        {
                            var values = new DateTime?[columnData[i].Count];
                            for (int j = 0; j < columnData[i].Count; j++)
                            {
                                var v = columnData[i][j];
                                values[j] = (v != null && v != DBNull.Value) ? Convert.ToDateTime(v) : (DateTime?)null;
                            }
                            var dataColumn = new DataColumn((DataField<DateTime?>)fields[i], values);
                            await groupWriter.WriteColumnAsync(dataColumn);
                        }
                        else if (columnType == typeof(bool))
                        {
                            var values = new bool?[columnData[i].Count];
                            for (int j = 0; j < columnData[i].Count; j++)
                            {
                                var v = columnData[i][j];
                                values[j] = (v != null && v != DBNull.Value) ? Convert.ToBoolean(v) : (bool?)null;
                            }
                            var dataColumn = new DataColumn((DataField<bool?>)fields[i], values);
                            await groupWriter.WriteColumnAsync(dataColumn);
                        }
                        else
                        {
                            var values = new string[columnData[i].Count];
                            for (int j = 0; j < columnData[i].Count; j++)
                            {
                                var v = columnData[i][j];
                                values[j] = (v != null && v != DBNull.Value) ? v.ToString() : null;
                            }
                            var dataColumn = new DataColumn((DataField<string>)fields[i], values);
                            await groupWriter.WriteColumnAsync(dataColumn);
                        }
                    }
                }
            }
        }

        private static string CleanField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            return field.Replace("\n", " ").Replace("\r", " ");
        }

        private static DbConnection GetConnection(eDatabase databaseType, string connectionString)
        {
            switch (databaseType)
            {
                case eDatabase.SQLServer:
                    return new SqlConnection(connectionString);
                case eDatabase.Sqlite:
                    return new SQLiteConnection(connectionString);
                case eDatabase.Firebird:
                    return new FbConnection(connectionString);
                case eDatabase.Oracle:
                    return new OracleConnection(connectionString);
                case eDatabase.MySql:
                    return new MySqlConnection(connectionString);
                case eDatabase.Access:
                    return new OleDbConnection(connectionString);
                case eDatabase.DB2:
                    throw new Exception("The IBM DB2 database is no longer supported.");
                case eDatabase.PostgreSql:
                    return new NpgsqlConnection(connectionString);
                default:
                    throw new Exception("Unspecified database type.");
            }
        }

        public static bool Validate(string[] args)
        {
            var message = "";

            if (args.Length != 4)
                message = "Run this tool again with four parameters:\n[1] - Full path of a .SQL file with a query\n" +
                    "[2] - Full path of the output file (.CSV or .PARQUET)\n" +
                    "[3] - Connectionstring (get support in connectionstrings.com)\n" +
                    "[4] - Database type (1 - SQL Server / 2 - SQLite / 3 - Firebird / 4 - Oracle / 5 - MySql / 6 - Access / 7 - IBM DB2 / 8 - PostgreSQL)";
            else
            {
                string file = args[0];

                if (!File.Exists(file))
                    message = string.Format("File not found in {0}!", file);
            }

            if (message != "")
            {
                string tempLogFile = args != null && args.Length > 0 ? Path.GetFileNameWithoutExtension(args[0]) + ".error" : "validation.error";
                File.WriteAllText(tempLogFile, string.Format("[{0}] Validation Error: {1}\n\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), message), Encoding.UTF8);
                return false;
            }

            return true;
        }
    }
}