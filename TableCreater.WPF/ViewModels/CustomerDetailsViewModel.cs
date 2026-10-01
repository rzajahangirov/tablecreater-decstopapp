using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TableCreater.WPF.Models;
using TableCreater.WPF.Services;

namespace TableCreater.WPF.ViewModels;

/// <summary>
/// ViewModel for the Customer Details page (Master-Detail pattern).
/// Displays customer-specific financial statistics and full transaction list.
/// Supports Delete operations with automatic dashboard refresh.
/// </summary>
public partial class CustomerDetailsViewModel : ObservableObject
{
    private readonly ITransactionService _transactionService;
    private readonly ICustomerService _customerService;

    public CustomerDetailsViewModel(
        ITransactionService transactionService,
        ICustomerService customerService)
    {
        _transactionService = transactionService;
        _customerService = customerService;
    }

    // =========================================================================
    // CUSTOMER INFO
    // =========================================================================

    [ObservableProperty]
    private long _customerId;

    [ObservableProperty]
    private string _customerName = string.Empty;

    [ObservableProperty]
    private string _customerPhone = string.Empty;

    // =========================================================================
    // TRANSACTION LIST
    // =========================================================================

    [ObservableProperty]
    private ObservableCollection<TransactionReadResponse> _transactions = new();

    [ObservableProperty]
    private TransactionReadResponse? _selectedTransaction;

    // =========================================================================
    // DASHBOARD CARDS
    // =========================================================================

    [ObservableProperty]
    private decimal _totalExpenseUsd;

    [ObservableProperty]
    private decimal _totalPaidUsd;

    [ObservableProperty]
    private decimal _remainingDebtUsd;

    [ObservableProperty]
    private string _debtStatusColor = "#888888";

    [ObservableProperty]
    private int _transactionCount;

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

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var customer = await _customerService.GetCustomerById(CustomerId);
            CustomerName = customer.Name;
            CustomerPhone = customer.Phone;

            await RefreshTransactionsAndCards();
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

    [RelayCommand]
    private async Task DeleteTransactionAsync(long transactionId)
    {
        try
        {
            ErrorMessage = null;
            SuccessMessage = null;

            await _transactionService.DeleteTransaction(transactionId);
            SuccessMessage = "Tranzaksiya uğurla silindi.";

            await RefreshTransactionsAndCards();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    public async Task RefreshDataAsync()
    {
        try
        {
            IsLoading = true;
            await RefreshTransactionsAndCards();
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

    public ITransactionService TransactionService => _transactionService;

    [RelayCommand]
    private void OpenDocument(string? documentPath)
    {
        if (!string.IsNullOrEmpty(documentPath) && System.IO.File.Exists(documentPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = documentPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Sənədi açmaq mümkün olmadı: {ex.Message}";
            }
        }
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private async Task RefreshTransactionsAndCards()
    {
        var transactions = await _transactionService.GetTransactionsByCustomer(CustomerId);
        Transactions = new ObservableCollection<TransactionReadResponse>(transactions);
        TransactionCount = transactions.Count;

        var report = await _transactionService.CalculateCustomerExpenseAndIncome(CustomerId);
        TotalExpenseUsd = report.TotalExpenseUsd;
        TotalPaidUsd = report.TotalPaidUsd;
        RemainingDebtUsd = report.TotalBenefitUsd; // TotalPaid - TotalExpense (Gəlir)

        if (RemainingDebtUsd < 0)
            DebtStatusColor = "#C62828"; // Red — Zərər
        else if (RemainingDebtUsd > 0)
            DebtStatusColor = "#2E7D32"; // Green — Gəlir
        else
            DebtStatusColor = "#888888"; // Gray — Balans
    }
}
