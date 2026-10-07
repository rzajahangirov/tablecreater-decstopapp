using TableCreater.WPF.Models;

namespace TableCreater.WPF.Services;

/// <summary>
/// Excel export service interface using ClosedXML.
/// Generates professional, print-ready .xlsx workbooks.
/// </summary>
public interface IExcelService
{
    /// <summary>
    /// Exports transactions for a customer to an Excel file with selectable columns.
    /// If specificTransactions is provided, exports those (respecting active UI filters);
    /// otherwise loads all customer transactions from the database.
    /// </summary>
    Task ExportTransactionsToExcel(
        long customerId,
        string filePath,
        IEnumerable<string> selectedColumnIds,
        IEnumerable<TransactionReadResponse>? specificTransactions = null);

    /// <summary>
    /// Exports customer balance history (ledger) to an Excel file with selectable columns.
    /// If specificHistories is provided, exports those (respecting active UI filters);
    /// otherwise loads all balance histories from the database.
    /// </summary>
    Task ExportBalanceHistoryToExcel(
        long customerId,
        string filePath,
        IEnumerable<string>? selectedColumnIds = null,
        IEnumerable<CustomerBalanceHistoryResponse>? specificHistories = null);

    /// <summary>
    /// Backward-compatible full export method.
    /// </summary>
    Task ExportToExcel(long customerId, string filePath);
}
