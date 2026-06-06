using Microsoft.EntityFrameworkCore;
using TableCreater.WPF.Data;
using TableCreater.WPF.Data.Entities;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;

namespace TableCreater.WPF.Services;

/// <summary>
/// Customer service implementation — full CRUD + search + status toggle.
/// Ports Java CustomerService endpoints C1–C7 (Section 2.2).
/// All methods are async for UI responsiveness.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly AppDbContext _db;

    public CustomerService(AppDbContext db)
    {
        _db = db;
    }

    // =========================================================================
    // C1 — CREATE CUSTOMER
    // Mapped from: CustomerService.createCustomer(CustomerCreateDto)
    // Default type = ACTIVE.
    // =========================================================================

    /// <inheritdoc />
    public async Task<CustomerReadResponse> CreateCustomer(CustomerCreateRequest request)
    {
        var entity = new Customer
        {
            Name = request.Name,
            Phone = request.Phone,
            Type = CustomerType.Active  // Default per spec
        };

        _db.Customers.Add(entity);
        await _db.SaveChangesAsync();

        return MapToReadResponse(entity);
    }

    // =========================================================================
    // C2 — GET ALL CUSTOMERS
    // Mapped from: CustomerService.getAllCustomers()
    // Returns empty list if none exist.
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<CustomerReadResponse>> GetAllCustomers()
    {
        var customers = await _db.Customers
            .OrderBy(c => c.Name)
            .ToListAsync();

        return customers.Select(MapToReadResponse).ToList();
    }

    // =========================================================================
    // C3 — GET CUSTOMER BY ID
    // Mapped from: CustomerService.getCustomerBydId(Long id)
    // Throws if not found (matches Java RuntimeException behavior).
    // =========================================================================

    /// <inheritdoc />
    public async Task<CustomerReadResponse> GetCustomerById(long id)
    {
        var entity = await _db.Customers.FindAsync(id)
            ?? throw new InvalidOperationException("Customer not found");

        return MapToReadResponse(entity);
    }

    // =========================================================================
    // C4 — CHANGE STATUS
    // Mapped from: CustomerService.changeStatus(Long id, CustomerType type)
    // Toggles between ACTIVE and INACTIVE.
    // =========================================================================

    /// <inheritdoc />
    public async Task ChangeStatus(long id, CustomerType type)
    {
        var entity = await _db.Customers.FindAsync(id)
            ?? throw new InvalidOperationException("Customer not found");

        entity.Type = type;
        await _db.SaveChangesAsync();
    }

    // =========================================================================
    // C5 — SEARCH CUSTOMERS
    // Mapped from: CustomerService.searchCustomers(String keyword)
    // Case-insensitive search on Name OR Phone.
    // Matches Java: findByNameContainingIgnoreCaseOrPhoneContainingIgnoreCase
    // =========================================================================

    /// <inheritdoc />
    public async Task<List<CustomerReadResponse>> SearchCustomers(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return await GetAllCustomers();

        var lowerKeyword = keyword.ToLower();

        var results = await _db.Customers
            .Where(c => c.Name.ToLower().Contains(lowerKeyword)
                      || (c.Phone != null && c.Phone.ToLower().Contains(lowerKeyword)))
            .OrderBy(c => c.Name)
            .ToListAsync();

        return results.Select(MapToReadResponse).ToList();
    }

    // =========================================================================
    // C6 — UPDATE CUSTOMER
    // Mapped from: CustomerService.updateCustomer(Long id, CustomerUpdateDto)
    // Partial update: only updates non-null fields.
    // =========================================================================

    /// <inheritdoc />
    public async Task<CustomerReadResponse> UpdateCustomer(long id, CustomerUpdateRequest request)
    {
        var entity = await _db.Customers.FindAsync(id)
            ?? throw new InvalidOperationException("Customer not found");

        // Partial update pattern — only overwrite if value is provided
        if (request.Name != null)
            entity.Name = request.Name;

        if (request.Phone != null)
            entity.Phone = request.Phone;

        await _db.SaveChangesAsync();

        return MapToReadResponse(entity);
    }

    // =========================================================================
    // C7 — DELETE CUSTOMER
    // Mapped from: CustomerService.deleteCustomer(Long id)
    // CASCADE DELETE: all associated transactions are deleted by EF Core
    // (configured in AppDbContext.OnModelCreating with DeleteBehavior.Cascade).
    // =========================================================================

    /// <inheritdoc />
    public async Task DeleteCustomer(long id)
    {
        var entity = await _db.Customers.FindAsync(id)
            ?? throw new InvalidOperationException("Customer not found");

        _db.Customers.Remove(entity);
        await _db.SaveChangesAsync();
    }

    // =========================================================================
    // MAPPING
    // =========================================================================

    private static CustomerReadResponse MapToReadResponse(Customer entity)
    {
        return new CustomerReadResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            Phone = entity.Phone ?? string.Empty
        };
    }
}
