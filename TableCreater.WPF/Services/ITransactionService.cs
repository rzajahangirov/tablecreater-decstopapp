using TableCreater.WPF.Models;

namespace TableCreater.WPF.Services;

/// <summary>
/// Transaction service interface — CRUD, financial calculations, and export.
/// Mapped from Java TransactionService endpoints T1–T8.
/// Contains the core calculation engine (Section 5.1 of the spec).
/// </summary>
public interface ITransactionService
{
    Task<TransactionReadResponse> CreateTransaction(TransactionCreateRequest request, long customerId);
    Task<List<TransactionReadResponse>> GetTransactionsByCustomer(long customerId);
    Task<ExpenseIncomeReport> CalculateExpenseAndIncome(DateOnly from, DateOnly to);
    Task<ExpenseIncomeReport> CalculateCustomerExpenseAndIncome(long customerId);
    Task<TransactionReadResponse> UpdateTransaction(long id, TransactionUpdateRequest request);
    Task<TransactionReadResponse> UpdateShipmentStatus(long id, ShipmentStatusUpdateRequest request);
    Task<TransactionEditFormData> GetTransactionForUpdate(long id);
    Task DeleteTransaction(long id);
    Task ExportCustomerTransactions(long customerId, string filePath);
}
