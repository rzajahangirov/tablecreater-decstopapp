using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TableCreater.WPF.Enums;
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

    [ObservableProperty]
    private decimal _customerBalanceUsd;

    [ObservableProperty]
    private string _balanceStatusColor = "#888888";

    // =========================================================================
    // BALANCE HISTORY
    // =========================================================================

    [ObservableProperty]
    private ObservableCollection<CustomerBalanceHistoryResponse> _balanceHistories = new();

    // =========================================================================
    // BALANCE ADJUSTMENT FIELDS
    // =========================================================================

    [ObservableProperty]
    private decimal _adjustmentAmount;

    [ObservableProperty]
    private PaymentCurrency _adjustmentCurrency = PaymentCurrency.Usd;

    [ObservableProperty]
    private decimal _adjustmentExchangeRate = 1.0m;

    [ObservableProperty]
    private string _adjustmentDescription = string.Empty;

    [ObservableProperty]
    private BalanceTransactionType _adjustmentType = BalanceTransactionType.ManualDeposit;

    public BalanceTransactionType[] ManualBalanceTypes => new[]
    {
        BalanceTransactionType.ManualDeposit,
        BalanceTransactionType.ManualWithdrawal,
        BalanceTransactionType.Adjustment
    };

    public PaymentCurrency[] PaymentCurrencies => Enum.GetValues<PaymentCurrency>();

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
            CustomerBalanceUsd = customer.BalanceUsd;
            UpdateBalanceColor();

            await RefreshTransactionsAndCards();
            await LoadBalanceHistoryAsync();
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
    private async Task AdjustBalanceAsync()
    {
        try
        {
            ErrorMessage = null;
            SuccessMessage = null;

            if (AdjustmentAmount <= 0 && AdjustmentType != BalanceTransactionType.Adjustment)
            {
                ErrorMessage = "Məbləğ 0-dan böyük olmalıdır.";
                return;
            }

            var request = new CustomerBalanceAdjustmentRequest
            {
                Type = AdjustmentType,
                Amount = AdjustmentAmount,
                Currency = AdjustmentCurrency,
                ExchangeRate = AdjustmentExchangeRate,
                Description = string.IsNullOrWhiteSpace(AdjustmentDescription)
                    ? null : AdjustmentDescription
            };

            var updated = await _customerService.AdjustBalance(CustomerId, request);
            CustomerBalanceUsd = updated.BalanceUsd;
            UpdateBalanceColor();

            // Reset fields
            AdjustmentAmount = 0;
            AdjustmentDescription = string.Empty;

            SuccessMessage = "Balans uğurla yeniləndi!";

            await LoadBalanceHistoryAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LoadBalanceHistoryAsync()
    {
        try
        {
            var histories = await _customerService.GetBalanceHistory(CustomerId);
            BalanceHistories = new ObservableCollection<CustomerBalanceHistoryResponse>(histories);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

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
        RemainingDebtUsd = report.TotalBenefitUsd;

        if (RemainingDebtUsd < 0)
            DebtStatusColor = "#C62828";
        else if (RemainingDebtUsd > 0)
            DebtStatusColor = "#2E7D32";
        else
            DebtStatusColor = "#888888";

        // Refresh balance after transactions change
        var customer = await _customerService.GetCustomerById(CustomerId);
        CustomerBalanceUsd = customer.BalanceUsd;
        UpdateBalanceColor();
    }

    private void UpdateBalanceColor()
    {
        if (CustomerBalanceUsd < 0)
            BalanceStatusColor = "#C62828"; // Red — borcu var
        else if (CustomerBalanceUsd > 0)
            BalanceStatusColor = "#2E7D32"; // Green — avansı var
        else
            BalanceStatusColor = "#888888"; // Gray — balans
    }
}
