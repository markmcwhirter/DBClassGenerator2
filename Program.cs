using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Text;
using System.Data.SqlClient;

namespace PocoGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting POCO generation...");

            string connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=Northwind;Trusted_Connection=True;MultipleActiveResultSets=true;"; // Set your DB connection string

            string currentDirectory = Directory.GetCurrentDirectory();


            string directory = Directory.GetParent(Directory.GetParent(Directory.GetParent(currentDirectory).FullName).FullName).FullName + "\\Models";

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }


            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Retrieve all table and column information using a single query
                var tableColumns = GetTableColumns(connection);

                // Group the result by table names and generate POCO classes
                var groupedByTables = tableColumns.AsEnumerable()
                                                  .GroupBy(row => row["TABLE_NAME"].ToString());

                foreach (var tableGroup in groupedByTables)
                {
                    var tableName = tableGroup.Key;
                    GeneratePocoClass(directory,tableName, tableGroup);
                }

                Console.WriteLine("POCO generation completed!");
            }
        }

        // Method to get all table names and column information in one query
        static DataTable GetTableColumns(SqlConnection connection)
        {
            string query = @"
                SELECT 
                    t.TABLE_NAME, 
                    c.COLUMN_NAME, 
                    c.DATA_TYPE, 
                    c.IS_NULLABLE,
                    c.CHARACTER_MAXIMUM_LENGTH
                FROM 
                    INFORMATION_SCHEMA.TABLES t
                INNER JOIN 
                    INFORMATION_SCHEMA.COLUMNS c 
                ON 
                    t.TABLE_NAME = c.TABLE_NAME
                WHERE 
                    t.TABLE_TYPE = 'BASE TABLE'
                ORDER BY 
                    t.TABLE_NAME, c.ORDINAL_POSITION";

            using (var command = new SqlCommand(query, connection))
            {
                var dataTable = new DataTable();
                using (var reader = command.ExecuteReader())
                {
                    dataTable.Load(reader);
                }
                return dataTable;
            }
        }

        // Method to generate POCO class for a given table
        static void GeneratePocoClass(string directory, string tableName, IEnumerable<DataRow> columns)
        {
            // var className = ToPascalCase(tableName);
            var className = tableName;
            var sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine($"namespace DBClassGenerator;");
            sb.AppendLine($"public class {className}");
            sb.AppendLine("{");

            foreach (var column in columns)
            {
                // var columnName = ToPascalCase(column["COLUMN_NAME"].ToString());
                var columnName = column["COLUMN_NAME"].ToString();
                var dataType = GetClrType(column["DATA_TYPE"].ToString());
                var isNullable = column["IS_NULLABLE"].ToString() == "YES";

                // Add the property with a nullable type if column allows null
                sb.AppendLine($"\tpublic {(isNullable && dataType != "string" ? dataType + "?" : dataType)} {columnName} {{ get; set; }}");
            }

            sb.AppendLine("}");


            var filePath = Path.Combine(directory, $"{className}.cs");
            File.WriteAllText(filePath, sb.ToString());

            Console.WriteLine($"Generated: {className}.cs");
        }

        // Method to map SQL data types to CLR types
        static string GetClrType(string sqlType)
        {
            switch (sqlType.ToLower())
            {
                case "int": return "int";
                case "bigint": return "long";
                case "smallint": return "short";
                case "tinyint": return "byte";
                case "bit": return "bool";
                case "decimal":
                case "numeric": return "decimal";
                case "float": return "double";
                case "real": return "float";
                case "datetime":
                case "smalldatetime":
                case "date":
                case "time": return "DateTime";
                case "char":
                case "nchar":
                case "nvarchar":
                case "varchar": return "string";
                case "uniqueidentifier": return "Guid";
                case "varbinary":
                case "binary": return "byte[]";
                case "ntext": return "string";
                case "image": return "byte[]";
                case "money": return "decimal";
                default: return "object";
            }
        }

        // Method to convert table/column names to PascalCase
        static string ToPascalCase(string input)
        {
            return string.Join("", input.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower()));
        }
    }
}
