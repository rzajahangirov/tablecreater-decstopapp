using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TableCreater.WPF.Models;
using TableCreater.WPF.Services;

namespace TableCreater.WPF.ViewModels;

/// <summary>
/// ViewModel for the Financial Reports page.
/// Implements aggregate reporting from Section 5.2 and Excel export via SaveFileDialog.
/// </summary>
public partial class ReportsViewModel : ObservableObject
{
    private readonly ITransactionService _transactionService;
    private readonly ICustomerService _customerService;
    private readonly IExcelService _excelService;

    public ReportsViewModel(
        ITransactionService transactionService,
        ICustomerService customerService,
        IExcelService excelService)
    {
        _transactionService = transactionService;
        _customerService = customerService;
        _excelService = excelService;

        // Default date range: current month
        FromDate = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        ToDate = DateOnly.FromDateTime(DateTime.Today);
    }

    // =========================================================================
    // DATE RANGE FILTER
    // =========================================================================

    [ObservableProperty]
    private DateOnly _fromDate;

    [ObservableProperty]
    private DateOnly _toDate;

    // =========================================================================
    // CUSTOMER FILTER (for per-customer reports & export)
    // =========================================================================

    [ObservableProperty]
    private ObservableCollection<CustomerReadResponse> _customers = new();

    [ObservableProperty]
    private CustomerReadResponse? _selectedCustomer;

    // =========================================================================
    // REPORT RESULTS
    // =========================================================================

    [ObservableProperty]
    private ExpenseIncomeReport? _report;

    [ObservableProperty]
    private bool _hasReport;

    [ObservableProperty]
    private decimal _totalExpenseUsd;

    [ObservableProperty]
    private decimal _totalPaidUsd;

    [ObservableProperty]
    private decimal _totalBenefitUsd;

    [ObservableProperty]
    private long _transactionCount;

    [ObservableProperty]
    private string _benefitStatusText = "";

    [ObservableProperty]
    private string _benefitStatusColor = "#888888";

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
    // COMMANDS
    // =========================================================================

    /// <summary>
    /// Loads customer list for the filter ComboBox.
    /// </summary>
    [RelayCommand]
    private async Task LoadCustomersAsync()
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
    /// Implements Section 5.2 aggregate reporting.
    /// </summary>
    [RelayCommand]
    private async Task GenerateDateRangeReportAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;

            var report = await _transactionService.CalculateExpenseAndIncome(FromDate, ToDate);
            ApplyReport(report);

            SuccessMessage = $"Report generated for {FromDate:yyyy-MM-dd} to {ToDate:yyyy-MM-dd}";
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
    private async Task GenerateCustomerReportAsync()
    {
        if (SelectedCustomer == null)
        {
            ErrorMessage = "Please select a customer first.";
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;

            var report = await _transactionService.CalculateCustomerExpenseAndIncome(SelectedCustomer.Id);
            ApplyReport(report);

            SuccessMessage = $"Report generated for customer: {SelectedCustomer.Name}";
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
    /// Exports transactions to Excel using SaveFileDialog.
    /// </summary>
    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        if (SelectedCustomer == null)
        {
            ErrorMessage = "Please select a customer to export.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export Transactions to Excel",
            Filter = "Excel Workbook|*.xlsx",
            FileName = $"{SelectedCustomer.Name}_Transactions_{DateTime.Now:yyyyMMdd}.xlsx",
            DefaultExt = ".xlsx"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            await _excelService.ExportToExcel(SelectedCustomer.Id, dialog.FileName);

            SuccessMessage = $"Exported to: {dialog.FileName}";
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
    // HELPERS
    // =========================================================================

    private void ApplyReport(ExpenseIncomeReport report)
    {
        Report = report;
        TotalExpenseUsd = report.TotalExpenseUsd;
        TotalPaidUsd = report.TotalPaidUsd;
        TotalBenefitUsd = report.TotalBenefitUsd;
        TransactionCount = report.TransactionCount;
        HasReport = true;

        if (TotalBenefitUsd > 0)
        {
            BenefitStatusText = "Xalis Mənfəət";
            BenefitStatusColor = "#2E7D32";
        }
        else if (TotalBenefitUsd < 0)
        {
            BenefitStatusText = "Xalis Zərər";
            BenefitStatusColor = "#C62828";
        }
        else
        {
            BenefitStatusText = "Balans";
            BenefitStatusColor = "#888888";
        }
    }
}
