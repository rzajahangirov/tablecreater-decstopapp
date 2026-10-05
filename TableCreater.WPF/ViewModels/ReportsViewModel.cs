using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;
using TableCreater.WPF.Services;

namespace TableCreater.WPF.ViewModels;

/// <summary>
/// Summary item for all customers financial overview report (Task 7.2).
/// </summary>
public record CustomerFinancialSummaryItem
{
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public decimal TotalExpenseUsd { get; init; }
    public decimal TotalPaidUsd { get; init; }
    public decimal TotalProfitUsd { get; init; }
    public decimal BalanceUsd { get; init; }
    public int TransactionCount { get; init; }
    public string TotalExpenseFormatted => $"${TotalExpenseUsd:N2}";
    public string TotalPaidFormatted => $"${TotalPaidUsd:N2}";
    public string TotalProfitFormatted => $"${TotalProfitUsd:N2}";
    public string BalanceFormatted => $"${BalanceUsd:N2}";
}

/// <summary>
/// ViewModel for the Financial Reports page.
/// Implements aggregate reporting matching the new accounting engine and Excel export.
/// </summary>
public partial class ReportsViewModel : ObservableObject
{
    private readonly ITransactionService _transactionService;
    private readonly ICustomerService _customerService;
    private readonly IExcelService _excelService;

    private List<TransactionReadResponse> _allReportTransactions = new();

    public ReportsViewModel(
        ITransactionService transactionService,
        ICustomerService customerService,
        IExcelService excelService)
    {
        _transactionService = transactionService;
        _customerService = customerService;
        _excelService = excelService;

        // Default date range: current month
        FromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        ToDate = DateTime.Today;
    }

    public IExcelService ExcelService => _excelService;
    public ITransactionService TransactionService => _transactionService;

    // =========================================================================
    // DATE RANGE FILTER & PRESETS
    // =========================================================================

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [RelayCommand]
    private void SetToday()
    {
        var now = DateTime.Today;
        FromDate = now;
        ToDate = now;
    }

    [RelayCommand]
    private void SetThisWeek()
    {
        var now = DateTime.Today;
        int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
        var startOfWeek = now.AddDays(-1 * diff);
        FromDate = startOfWeek;
        ToDate = now;
    }

    [RelayCommand]
    private void SetThisMonth()
    {
        var now = DateTime.Today;
        FromDate = new DateTime(now.Year, now.Month, 1);
        ToDate = now;
    }

    [RelayCommand]
    private void SetLastMonth()
    {
        var now = DateTime.Today;
        var firstDayLastMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
        var lastDayLastMonth = new DateTime(now.Year, now.Month, 1).AddDays(-1);
        FromDate = firstDayLastMonth;
        ToDate = lastDayLastMonth;
    }

    [RelayCommand]
    private void SetThisYear()
    {
        var now = DateTime.Today;
        FromDate = new DateTime(now.Year, 1, 1);
        ToDate = now;
    }

    [RelayCommand]
    private void SetLastYear()
    {
        var now = DateTime.Today;
        FromDate = new DateTime(now.Year - 1, 1, 1);
        ToDate = new DateTime(now.Year - 1, 12, 31);
    }

    [RelayCommand]
    private void SetAllTime()
    {
        FromDate = new DateTime(2020, 1, 1);
        ToDate = DateTime.Today;
    }

    // =========================================================================
    // CUSTOMER FILTER
    // =========================================================================

    [ObservableProperty]
    private ObservableCollection<CustomerReadResponse> _customers = new();

    [ObservableProperty]
    private CustomerReadResponse? _selectedCustomer;

    // =========================================================================
    // REPORT RESULTS DASHBOARD METRICS
    // =========================================================================

    [ObservableProperty]
    private ExpenseIncomeReport? _report;

    [ObservableProperty]
    private bool _hasReport;

    [ObservableProperty]
    private string _reportTypeTitle = string.Empty;

    [ObservableProperty]
    private decimal _totalExpenseUsd;

    [ObservableProperty]
    private decimal _totalBilledUsd;

    [ObservableProperty]
    private decimal _totalUserProfitUsd;

    [ObservableProperty]
    private decimal _totalPaidUsd;

    [ObservableProperty]
    private decimal _totalBenefitUsd;

    [ObservableProperty]
    private decimal _totalCashFlowUsd;

    [ObservableProperty]
    private decimal _remainingDebtUsd;

    [ObservableProperty]
    private long _transactionCount;

    [ObservableProperty]
    private int _paidTransactionCount;

    [ObservableProperty]
    private int _unpaidTransactionCount;

    [ObservableProperty]
    private decimal _totalWeightTon;

    [ObservableProperty]
    private int _totalVehicleCount;

    [ObservableProperty]
    private decimal _profitMarginPercent;

    [ObservableProperty]
    private decimal _collectionRatePercent;

    [ObservableProperty]
    private string _benefitStatusText = string.Empty;

    [ObservableProperty]
    private string _benefitStatusColor = "#888888";

    [ObservableProperty]
    private string _debtStatusText = string.Empty;

    [ObservableProperty]
    private string _debtStatusColor = "#888888";

    // =========================================================================
    // REPORT TRANSACTIONS BREAKDOWN TABLE
    // =========================================================================

    [ObservableProperty]
    private ObservableCollection<TransactionReadResponse> _reportTransactions = new();

    [ObservableProperty]
    private int _reportFilteredCount;

    [ObservableProperty]
    private string _reportSearchText = string.Empty;

    [ObservableProperty]
    private PaymentStatus? _reportPaymentStatusFilter;

    public List<TransactionReadResponse> AllReportTransactions => _allReportTransactions;

    partial void OnReportSearchTextChanged(string value) => FilterReportTransactions();
    partial void OnReportPaymentStatusFilterChanged(PaymentStatus? value) => FilterReportTransactions();

    [RelayCommand]
    private void FilterReportTransactions()
    {
        IEnumerable<TransactionReadResponse> filtered = _allReportTransactions;

        if (ReportPaymentStatusFilter.HasValue)
        {
            filtered = filtered.Where(t => t.PaymentStatus == ReportPaymentStatusFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(ReportSearchText))
        {
            var q = ReportSearchText.Trim().ToLowerInvariant();
            filtered = filtered.Where(t =>
                (t.CustomerName?.ToLowerInvariant().Contains(q) == true) ||
                (t.ProductName?.ToLowerInvariant().Contains(q) == true) ||
                (t.ReceivingCompany?.ToLowerInvariant().Contains(q) == true) ||
                (t.SendingCompany?.ToLowerInvariant().Contains(q) == true));
        }

        var list = filtered.ToList();
        ReportTransactions = new ObservableCollection<TransactionReadResponse>(list);
        ReportFilteredCount = list.Count;
    }

    [RelayCommand]
    private void ClearReportFilters()
    {
        ReportSearchText = string.Empty;
        ReportPaymentStatusFilter = null;
        FilterReportTransactions();
    }

    // =========================================================================
    // UI STATE
    // =========================================================================

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    // =========================================================================
    // ALL CUSTOMERS SUMMARY (Task 7.2)
    // =========================================================================

    [ObservableProperty]
    private ObservableCollection<CustomerFinancialSummaryItem> _allCustomersSummary = new();

    [ObservableProperty]
    private bool _isAllCustomersSummaryView;

    [RelayCommand]
    public async Task GenerateAllCustomersSummaryAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;

            var customers = await _customerService.GetAllCustomers();
            var summaryList = new List<CustomerFinancialSummaryItem>();

            foreach (var c in customers)
            {
                var report = await _transactionService.CalculateCustomerExpenseAndIncome(c.Id);
                summaryList.Add(new CustomerFinancialSummaryItem
                {
                    CustomerId = c.Id,
                    CustomerName = c.Name,
                    Phone = c.Phone,
                    TotalExpenseUsd = report.TotalExpenseUsd,
                    TotalPaidUsd = report.TotalPaidUsd,
                    TotalProfitUsd = report.TotalUserProfitUsd,
                    BalanceUsd = c.BalanceUsd,
                    TransactionCount = (int)report.TransactionCount
                });
            }

            AllCustomersSummary = new ObservableCollection<CustomerFinancialSummaryItem>(summaryList);
            IsAllCustomersSummaryView = true;
            HasReport = false;
            ReportTypeTitle = "Bütün Müştərilər Üzrə Ümumi Maliyyə Xülasəsi";
            SuccessMessage = $"Bütün müştərilərin xülasəsi hazırlandı ({customers.Count} müştəri).";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // =========================================================================
    // COMMANDS
    // =========================================================================

    /// <summary>
    /// Loads customer list for the filter ComboBox.
    /// </summary>
    [RelayCommand]
    public async Task LoadCustomersAsync()
    {
        try
        {
            var customers = await _customerService.GetAllCustomers();
            Customers = new ObservableCollection<CustomerReadResponse>(customers);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Generates the financial report for the selected date range.
    /// Loads both summary metrics and detailed transactions.
    /// </summary>
    [RelayCommand]
    public async Task GenerateDateRangeReportAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;

            if (!FromDate.HasValue || !ToDate.HasValue)
            {
                ErrorMessage = "Zəhmət olmasa başlanğıc və son tarixləri seçin.";
                return;
            }

            if (ToDate.Value < FromDate.Value)
            {
                ErrorMessage = "Son tarix başlanğıc tarixindən əvvəl ola bilməz.";
                return;
            }

            var fromDateOnly = DateOnly.FromDateTime(FromDate.Value);
            var toDateOnly = DateOnly.FromDateTime(ToDate.Value);

            var report = await _transactionService.CalculateExpenseAndIncome(fromDateOnly, toDateOnly);
            var transactions = await _transactionService.GetTransactionsByDateRange(fromDateOnly, toDateOnly);

            _allReportTransactions = transactions;
            FilterReportTransactions();

            IsAllCustomersSummaryView = false;
            ReportTypeTitle = $"Tarix Aralığı: {fromDateOnly:dd.MM.yyyy} — {toDateOnly:dd.MM.yyyy}";
            ApplyReport(report);

            SuccessMessage = $"Hesabat yaradıldı: {fromDateOnly:dd.MM.yyyy} – {toDateOnly:dd.MM.yyyy} ({report.TransactionCount} tranzaksiya)";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasReport = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Generates the financial report for the selected customer (all transactions).
    /// </summary>
    [RelayCommand]
    public async Task GenerateCustomerReportAsync()
    {
        if (SelectedCustomer == null)
        {
            ErrorMessage = "Zəhmət olmasa əvvəlcə müştəri seçin.";
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;

            var report = await _transactionService.CalculateCustomerExpenseAndIncome(SelectedCustomer.Id);
            var transactions = await _transactionService.GetTransactionsByCustomer(SelectedCustomer.Id);

            _allReportTransactions = transactions;
            FilterReportTransactions();

            IsAllCustomersSummaryView = false;
            ReportTypeTitle = $"Müştəri Hesabatı: {SelectedCustomer.Name}";
            ApplyReport(report);

            SuccessMessage = $"Müştəri hesabatı yaradıldı: {SelectedCustomer.Name} ({report.TransactionCount} tranzaksiya)";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasReport = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private void ApplyReport(ExpenseIncomeReport report)
    {
        Report = report;
        TotalExpenseUsd = report.TotalExpenseUsd;
        TotalBilledUsd = report.TotalBilledUsd;
        TotalUserProfitUsd = report.TotalUserProfitUsd;
        TotalPaidUsd = report.TotalPaidUsd;
        TotalBenefitUsd = report.TotalBenefitUsd;
        TotalCashFlowUsd = report.TotalCashFlowUsd;
        RemainingDebtUsd = report.RemainingDebtUsd;
        TransactionCount = report.TransactionCount;
        PaidTransactionCount = report.PaidTransactionCount;
        UnpaidTransactionCount = report.UnpaidTransactionCount;
        TotalWeightTon = report.TotalWeightTon;
        TotalVehicleCount = report.TotalVehicleCount;
        ProfitMarginPercent = report.ProfitMarginPercent;
        CollectionRatePercent = report.CollectionRatePercent;
        HasReport = true;

        // Debt status (Müştəri Borcu / Fərq)
        if (TotalBenefitUsd < 0)
        {
            DebtStatusText = "Qalıq Borc";
            DebtStatusColor = "#C62828"; // Red
        }
        else if (TotalBenefitUsd > 0)
        {
            DebtStatusText = "Artıq Ödəniş (Avans)";
            DebtStatusColor = "#2E7D32"; // Green
        }
        else
        {
            DebtStatusText = "Tam Ödənilib (Sıfır Qalıq)";
            DebtStatusColor = "#555555";
        }

        // Cash flow status
        if (TotalCashFlowUsd > 0)
        {
            BenefitStatusText = "Müsbət Nağd Axını";
            BenefitStatusColor = "#2E7D32";
        }
        else if (TotalCashFlowUsd < 0)
        {
            BenefitStatusText = "Mənfi Nağd Axını";
            BenefitStatusColor = "#C62828";
        }
        else
        {
            BenefitStatusText = "Kassa Balansda";
            BenefitStatusColor = "#555555";
        }
    }
}
