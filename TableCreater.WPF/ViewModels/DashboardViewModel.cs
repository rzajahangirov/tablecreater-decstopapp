using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TableCreater.WPF.Data;
using TableCreater.WPF.Data.Entities;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;

namespace TableCreater.WPF.ViewModels;

/// <summary>
/// Model for customers with negative balance (debt) shown on the Dashboard.
/// </summary>
public record NegativeCustomerItem
{
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public decimal BalanceUsd { get; init; }
    public string BalanceFormatted => $"${BalanceUsd:N2}";
    public DateOnly? LastTransactionDate { get; init; }
    public string LastTransactionDateFormatted => LastTransactionDate?.ToString("yyyy-MM-dd") ?? "—";
}

/// <summary>
/// ViewModel for the executive Dashboard / Home Overview.
/// Implements Task 2 (🔴 Dashboard / Ana Səhifə) from Table Creater Task.md.
/// Displays key business metrics, recent transactions, and debt warnings.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly AppDbContext _db;

    public DashboardViewModel(AppDbContext db)
    {
        _db = db;
    }

    // =========================================================================
    // KPI METRICS
    // =========================================================================

    [ObservableProperty]
    private int _totalCustomers;

    [ObservableProperty]
    private int _activeCustomers;

    [ObservableProperty]
    private decimal _monthlyExpenseUsd;

    [ObservableProperty]
    private decimal _monthlyPaidUsd;

    [ObservableProperty]
    private decimal _monthlyProfitUsd;

    [ObservableProperty]
    private int _monthlyTransactionCount;

    [ObservableProperty]
    private decimal _totalBalanceUsd;

    [ObservableProperty]
    private string _currentMonthName = DateTime.Today.ToString("MMMM yyyy");

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    // =========================================================================
    // LISTS
    // =========================================================================

    [ObservableProperty]
    private ObservableCollection<TransactionReadResponse> _recentTransactions = new();

    [ObservableProperty]
    private ObservableCollection<NegativeCustomerItem> _negativeCustomers = new();

    // =========================================================================
    // COMMANDS
    // =========================================================================

    /// <summary>
    /// Loads all dashboard data asynchronously.
    /// </summary>
    [RelayCommand]
    public async Task LoadDashboardAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var today = DateTime.Today;
            var startOfMonth = new DateOnly(today.Year, today.Month, 1);
            var endOfMonth = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
            CurrentMonthName = today.ToString("MMMM yyyy");

            // 1. Customer counts
            TotalCustomers = await _db.Customers.CountAsync();
            ActiveCustomers = await _db.Customers.CountAsync(c => c.Type == CustomerType.Active);
            TotalBalanceUsd = await _db.Customers.SumAsync(c => (decimal?)c.BalanceUsd) ?? 0m;

            // 2. Transactions for current month
            var monthlyTx = await _db.Transactions
                .Where(t => t.TransactionDate >= startOfMonth && t.TransactionDate <= endOfMonth)
                .ToListAsync();

            MonthlyExpenseUsd = monthlyTx.Sum(t => t.HistoricalTotalExpenseUsd);
            MonthlyProfitUsd = monthlyTx.Sum(t => t.HistoricalUserProfitUsd);
            MonthlyTransactionCount = monthlyTx.Count;

            MonthlyPaidUsd = monthlyTx.Sum(t =>
            {
                if (t.PaymentStatus == PaymentStatus.Unpaid) return 0m;
                decimal paid = t.PaidAmount ?? 0m;
                return t.PaidCurrency == PaymentCurrency.Rub ? (t.HistoricalExchangeRate > 0 ? paid / t.HistoricalExchangeRate : 0m) : paid;
            });

            // 3. Top 10 recent transactions
            var recent = await _db.Transactions
                .Include(t => t.Customer)
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.Id)
                .Take(10)
                .ToListAsync();

            var recentResponses = recent.Select(t => MapToReadResponse(t, t.Customer?.Name ?? "—")).ToList();
            RecentTransactions = new ObservableCollection<TransactionReadResponse>(recentResponses);

            // 4. Customers with negative balance (debt)
            var debtors = await _db.Customers
                .Where(c => c.BalanceUsd < 0)
                .Include(c => c.Transactions)
                .ToListAsync();

            var orderedDebtors = debtors.OrderBy(c => c.BalanceUsd).ToList();

            var negItems = orderedDebtors.Select(c => new NegativeCustomerItem
            {
                CustomerId = c.Id,
                CustomerName = c.Name,
                Phone = c.Phone ?? string.Empty,
                BalanceUsd = c.BalanceUsd,
                LastTransactionDate = c.Transactions
                    .OrderByDescending(t => t.TransactionDate)
                    .Select(t => (DateOnly?)t.TransactionDate)
                    .FirstOrDefault()
            }).ToList();

            NegativeCustomers = new ObservableCollection<NegativeCustomerItem>(negItems);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Məlumatları yükləyərkən xəta: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static TransactionReadResponse MapToReadResponse(Transaction entity, string customerName)
    {
        decimal paidInUsd = 0m;
        if (entity.PaymentStatus != PaymentStatus.Unpaid && entity.PaidAmount.HasValue)
        {
            paidInUsd = entity.PaidCurrency == PaymentCurrency.Rub
                ? (entity.HistoricalExchangeRate > 0 ? entity.PaidAmount.Value / entity.HistoricalExchangeRate : 0m)
                : entity.PaidAmount.Value;
        }

        return new TransactionReadResponse
        {
            Id = entity.Id,
            CustomerId = entity.CustomerId,
            CustomerName = customerName,
            TransactionDate = entity.TransactionDate,
            CreatedAt = DateOnly.FromDateTime(entity.CreatedAt),
            ProductName = entity.ProductName ?? string.Empty,
            ReceivingCompany = entity.ReceivingCompany ?? string.Empty,
            SendingCompany = entity.SendingCompany,
            WeightTon = entity.WeightTon,
            PricePerTonRub = entity.PricePerTonRub,
            TransportType = entity.TransportType,
            TransportCurrency = entity.TransportCurrency,
            VehicleCount = entity.VehicleCount,
            PricePerVehicle = entity.PricePerVehicle,
            PaidAmount = entity.PaidAmount,
            PaidCurrency = entity.PaidCurrency,
            DocumentPath = entity.DocumentPath,
            HistoricalExchangeRate = entity.HistoricalExchangeRate,
            HistoricalTotalExpenseUsd = entity.HistoricalTotalExpenseUsd,
            HistoricalRemainingDebtUsd = entity.HistoricalRemainingDebtUsd,
            PaidInUsd = paidInUsd,
            IsCompleted = entity.IsCompleted,
            PaymentStatus = entity.PaymentStatus,
            ProfitPerTon = entity.ProfitPerTon,
            ProfitPerTonCurrency = entity.ProfitPerTonCurrency,
            HistoricalUserProfitUsd = entity.HistoricalUserProfitUsd,
            HistoricalCustomerBilledUsd = entity.HistoricalCustomerBilledUsd,
            HistoricalBalanceDeltaUsd = entity.HistoricalBalanceDeltaUsd,
            ShipmentStatus = entity.ShipmentStatus,
            LoadedDate = entity.LoadedDate,
            InTransitStartDate = entity.InTransitStartDate,
            InTransitEndDate = entity.InTransitEndDate,
            DeliveredDate = entity.DeliveredDate,
            IsInTransitAutoDates = entity.IsInTransitAutoDates,
            AdditionalExpenseAmount = entity.AdditionalExpenseAmount,
            AdditionalExpenseCurrency = entity.AdditionalExpenseCurrency,
            AdditionalExpenseDescription = entity.AdditionalExpenseDescription
        };
    }
}
