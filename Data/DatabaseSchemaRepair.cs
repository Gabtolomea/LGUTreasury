using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace LGUTreasury.Data;

/// <summary>
/// Repairs schema drift left by historical migrations that omitted columns
/// already present in the EF Core model snapshot.
/// </summary>
public static class DatabaseSchemaRepair
{
    // Compile-time constant column specs; never derived from runtime input.
    private static readonly (string TableName, string ColumnName, string ColumnDefinition)[] RequiredColumns =
    [
        ("PaymentRecords", "PaymentMethod", "LONGTEXT NULL"),
        ("Editrequests", "OfficialReceipt", "LONGTEXT NULL"),
        ("DeletedRecords", "OfficialReceipt", "LONGTEXT NULL"),
    ];

    /// <summary>
    /// Ensures required columns omitted by earlier migrations exist on relational
    /// databases without failing when the columns are already present.
    /// </summary>
    public static void EnsureMissingColumns(ApplicationDbContext context)
    {
        if (!context.Database.IsRelational())
            return;

        var connection = context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            connection.Open();

        try
        {
            foreach (var (tableName, columnName, columnDefinition) in RequiredColumns)
            {
                if (ColumnExists(connection, tableName, columnName))
                    continue;

                AddColumn(connection, tableName, columnName, columnDefinition);
            }
        }
        finally
        {
            if (openedHere && connection.State == ConnectionState.Open)
                connection.Close();
        }
    }

    private static bool ColumnExists(DbConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND LOWER(TABLE_NAME) = LOWER(@tableName)
              AND LOWER(COLUMN_NAME) = LOWER(@columnName)
            """;

        AddParameter(command, "@tableName", tableName);
        AddParameter(command, "@columnName", columnName);

        var result = command.ExecuteScalar();
        return Convert.ToInt64(result) > 0;
    }

    private static void AddColumn(
        DbConnection connection,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {columnDefinition};";
        command.ExecuteNonQuery();
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
