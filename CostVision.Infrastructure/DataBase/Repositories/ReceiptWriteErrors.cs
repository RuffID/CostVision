using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CostVision.Infrastructure.DataBase.Repositories;

/// <summary>
/// Распознаёт только известные SQL Server конфликты записи чеков.
/// </summary>
internal static class ReceiptWriteErrors
{
    internal const string AUTHOR_IDENTITY_INDEX = "IX_Receipts_CreatedByUserId_FiscalDriveNumber_FiscalDocumentNumber_FiscalSign_DateTime_TotalSum_OperationType";
    internal const string ACCOUNT_LINK_KEY = "PK_ReceiptAccounts";

    internal static bool IsKnownUniqueViolation(int number, string message)
    {
        if (number != 2601 && number != 2627)
        {
            return false;
        }

        string constraintName = Regex.Escape(AUTHOR_IDENTITY_INDEX) + "|" + Regex.Escape(ACCOUNT_LINK_KEY);
        return Regex.IsMatch(message,
            @"(?:unique index|UNIQUE KEY constraint|PRIMARY KEY constraint|уникальным индексом|ограничения UNIQUE KEY|ограничения PRIMARY KEY) '(?:" + constraintName + @")'",
            RegexOptions.CultureInvariant);
    }

    internal static bool IsDeadlock(Exception exception)
    {
        if (exception is SqlException sql)
        {
            return sql.Number == 1205;
        }

        if (exception is DbUpdateException update && update.InnerException is SqlException inner)
        {
            return inner.Number == 1205;
        }

        return false;
    }
}
