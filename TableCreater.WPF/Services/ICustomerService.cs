using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;

namespace TableCreater.WPF.Services;

/// <summary>
/// Customer service interface — CRUD operations and search.
/// Mapped from Java CustomerService endpoints C1–C7.
/// </summary>
public interface ICustomerService
{
    Task<CustomerReadResponse> CreateCustomer(CustomerCreateRequest request);
    Task<List<CustomerReadResponse>> GetAllCustomers();
    Task<CustomerReadResponse> GetCustomerById(long id);
    Task ChangeStatus(long id, CustomerType type);
    Task<List<CustomerReadResponse>> SearchCustomers(string keyword);
    Task<CustomerReadResponse> UpdateCustomer(long id, CustomerUpdateRequest request);
    Task DeleteCustomer(long id);
    Task<CustomerReadResponse> AdjustBalance(long id, CustomerBalanceAdjustmentRequest request);
    Task<List<CustomerBalanceHistoryResponse>> GetBalanceHistory(long customerId);
}
