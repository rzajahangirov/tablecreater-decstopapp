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
/// Supports filtering, sorting, search, and statistics.
/// </summary>
public partial class CustomerDetailsViewModel : ObservableObject
{
    private readonly ITransactionService _transactionService;
    private readonly ICustomerService _customerService;
    private readonly IExcelService _excelService;

    public CustomerDetailsViewModel(
        ITransactionService transactionService,
        ICustomerService customerService,
        IExcelService excelService)
    {
        _transactionService = transactionService;
        _customerService = customerService;
        _excelService = excelService;
    }

    public IExcelService ExcelService => _excelService;

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
    // TRANSACTION LIST (raw + filtered)
    // =========================================================================

    /// <summary>
    /// All transactions loaded from DB (unfiltered).
    /// </summary>
    private List<TransactionReadResponse> _allTransactions = new();

    [ObservableProperty]
    private ObservableCollection<TransactionReadResponse> _transactions = new();

    [ObservableProperty]
    private TransactionReadResponse? _selectedTransaction;

    // =========================================================================
    // TRANSACTION FILTER FIELDS
    // =========================================================================

    [ObservableProperty]
    private DateTime? _txFilterDateFrom;

    [ObservableProperty]
    private DateTime? _txFilterDateTo;

    [ObservableProperty]
    private PaymentStatus? _txFilterPaymentStatus;

    [ObservableProperty]
    private ShipmentStatus? _txFilterShipmentStatus;

    [ObservableProperty]
    private string _txSearchText = string.Empty;

    /// <summary>
    /// Options for Payment Status filter ComboBox (null = Hamısı).
    /// </summary>
    public PaymentStatus?[] PaymentStatusFilterOptions => new PaymentStatus?[]
    {
        null,
        PaymentStatus.Paid,
        PaymentStatus.PaidFromBalance,
        PaymentStatus.Unpaid
    };

    /// <summary>
    /// Options for Shipment Status filter ComboBox (null = Hamısı).
    /// </summary>
    public ShipmentStatus?[] ShipmentStatusFilterOptions => new ShipmentStatus?[]
    {
        null,
        ShipmentStatus.Pending,
        ShipmentStatus.Loaded,
        ShipmentStatus.InTransit,
        ShipmentStatus.Delivered
    };

    // =========================================================================
    // TRANSACTION STATISTICS
    // =========================================================================

    [ObservableProperty]
    private int _paidTransactionCount;

    [ObservableProperty]
    private int _unpaidTransactionCount;

    [ObservableProperty]
    private int _filteredTransactionCount;

    // =========================================================================
    // BALANCE HISTORY (raw + filtered)
    // =========================================================================

    private List<CustomerBalanceHistoryResponse> _allBalanceHistories = new();

    [ObservableProperty]
    private ObservableCollection<CustomerBalanceHistoryResponse> _balanceHistories = new();

    // =========================================================================
    // BALANCE HISTORY FILTER FIELDS
    // =========================================================================

    [ObservableProperty]
    private DateTime? _bhFilterDateFrom;

    [ObservableProperty]
    private DateTime? _bhFilterDateTo;

    // =========================================================================
    // DASHBOARD CARDS
    // =========================================================================

    [ObservableProperty]
    private decimal _totalExpenseUsd;

    [ObservableProperty]
    private decimal _totalPaidUsd;

    [ObservableProperty]
    private decimal _totalBilledUsd;

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

    [ObservableProperty]
    private decimal _customerBalanceRub;

    [ObservableProperty]
    private string _balanceRubStatusColor = "#888888";

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

    [ObservableProperty]
    private string _transferPreview = string.Empty;

    public BalanceTransactionType[] ManualBalanceTypes => new[]
    {
        BalanceTransactionType.ManualDeposit,
        BalanceTransactionType.ManualWithdrawal,
        BalanceTransactionType.Adjustment,
        BalanceTransactionType.Transfer
    };

    public PaymentCurrency[] PaymentCurrencies => Enum.GetValues<PaymentCurrency>();

    partial void OnAdjustmentAmountChanged(decimal value) => UpdateTransferPreview();
    partial void OnAdjustmentExchangeRateChanged(decimal value) => UpdateTransferPreview();
    partial void OnAdjustmentCurrencyChanged(PaymentCurrency value) => UpdateTransferPreview();
    partial void OnAdjustmentTypeChanged(BalanceTransactionType value) => UpdateTransferPreview();

    private void UpdateTransferPreview()
    {
        if (AdjustmentType != BalanceTransactionType.Transfer)
        {
            TransferPreview = string.Empty;
            return;
        }

        decimal rate = AdjustmentExchangeRate > 0 ? AdjustmentExchangeRate : 1.0m;
        if (AdjustmentCurrency == PaymentCurrency.Usd)
        {
            decimal rubIn = AdjustmentAmount * rate;
            TransferPreview = $"Köçürmə (USD → RUB): {AdjustmentAmount:N2} USD çıxılacaq ➔ {rubIn:N2} RUB mədaxil olunacaq (Məzənnə: {rate:N2})";
        }
        else
        {
            decimal usdIn = rate > 0 ? AdjustmentAmount / rate : 0m;
            TransferPreview = $"Köçürmə (RUB → USD): {AdjustmentAmount:N2} RUB çıxılacaq ➔ ${usdIn:N2} mədaxil olunacaq (Məzənnə: {rate:N2})";
        }
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
            CustomerBalanceRub = customer.BalanceRub;
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
    private async Task ToggleTransactionCompletedAsync(long transactionId)
    {
        try
        {
            ErrorMessage = null;
            await _transactionService.ToggleTransactionCompleted(transactionId);
            await RefreshTransactionsAndCards();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Tamamlanma statusunu dəyişmək mümkün olmadı: {ex.Message}";
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
    public List<CustomerBalanceHistoryResponse> AllBalanceHistories => _allBalanceHistories;

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
            CustomerBalanceRub = updated.BalanceRub;
            UpdateBalanceColor();

            // Reset fields
            AdjustmentAmount = 0;
            AdjustmentDescription = string.Empty;
            UpdateTransferPreview();

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
            _allBalanceHistories = histories;
            ExecuteBalanceHistoryFilter();
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

    [RelayCommand]
    private async Task ExportBalanceHistoryToExcelAsync()
    {
        try
        {
            ErrorMessage = null;
            SuccessMessage = null;

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Balans Tarixçəsini Excel-ə İxrac Et",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"{CustomerName}_Balans_Tarixcesi_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                DefaultExt = ".xlsx"
            };

            if (saveDialog.ShowDialog() != true) return;

            IsLoading = true;
            await _excelService.ExportBalanceHistoryToExcel(CustomerId, saveDialog.FileName, null, BalanceHistories);

            SuccessMessage = $"Balans tarixçəsi uğurla ixrac edildi: {saveDialog.FileName}";

            if (System.IO.File.Exists(saveDialog.FileName))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = saveDialog.FileName,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // Ignore if no default viewer
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Excel ixracı zamanı xəta: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public int TotalTransactionCount => _allTransactions.Count;
    public List<TransactionReadResponse> AllTransactions => _allTransactions;

    // =========================================================================
    // FILTER COMMANDS
    // =========================================================================

    [RelayCommand]
    private void ApplyTransactionFilters()
    {
        IEnumerable<TransactionReadResponse> filtered = _allTransactions;

        // Date range filter
        if (TxFilterDateFrom.HasValue)
        {
            var fromDate = DateOnly.FromDateTime(TxFilterDateFrom.Value);
            filtered = filtered.Where(t => t.TransactionDate >= fromDate);
        }
        if (TxFilterDateTo.HasValue)
        {
            var toDate = DateOnly.FromDateTime(TxFilterDateTo.Value);
            filtered = filtered.Where(t => t.TransactionDate <= toDate);
        }

        // Payment status filter
        if (TxFilterPaymentStatus.HasValue)
        {
            filtered = filtered.Where(t => t.PaymentStatus == TxFilterPaymentStatus.Value);
        }

        // Shipment status filter
        if (TxFilterShipmentStatus.HasValue)
        {
            filtered = filtered.Where(t => t.ShipmentStatus == TxFilterShipmentStatus.Value);
        }

        // Text search (product name, receiving company, sending company)
        if (!string.IsNullOrWhiteSpace(TxSearchText))
        {
            var searchLower = TxSearchText.ToLower();
            filtered = filtered.Where(t =>
                (t.ProductName?.ToLower().Contains(searchLower) == true) ||
                (t.ReceivingCompany?.ToLower().Contains(searchLower) == true) ||
                (t.SendingCompany?.ToLower().Contains(searchLower) == true));
        }

        // Default sort: newest first (already sorted from DB, but ensure after filtering)
        var result = filtered.OrderByDescending(t => t.TransactionDate).ToList();

        Transactions = new ObservableCollection<TransactionReadResponse>(result);
        FilteredTransactionCount = result.Count;

        // Update statistics
        UpdateTransactionStatistics();
    }

    [RelayCommand]
    private void ClearTransactionFilters()
    {
        TxFilterDateFrom = null;
        TxFilterDateTo = null;
        TxFilterPaymentStatus = null;
        TxFilterShipmentStatus = null;
        TxSearchText = string.Empty;
        ApplyTransactionFilters();
    }

    [ObservableProperty]
    private PaymentCurrency? _bhFilterKassa = null;

    public PaymentCurrency?[] BhKassaFilterOptions => new PaymentCurrency?[]
    {
        null,
        PaymentCurrency.Usd,
        PaymentCurrency.Rub
    };

    [RelayCommand]
    private void ApplyBalanceHistoryFilters()
    {
        ExecuteBalanceHistoryFilter();
    }

    [RelayCommand]
    private void ClearBalanceHistoryFilters()
    {
        BhFilterDateFrom = null;
        BhFilterDateTo = null;
        BhFilterKassa = null;
        ExecuteBalanceHistoryFilter();
    }

    [RelayCommand]
    private async Task DeleteBalanceHistoryAsync(long historyId)
    {
        try
        {
            var res = System.Windows.MessageBox.Show(
                "Bu balans əməliyyatını silmək istədiyinizə əminsiniz?\nƏməliyyatın məbləği müştərinin balansına geri qaytarılacaq.",
                "Əməliyyatı Sil",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (res != System.Windows.MessageBoxResult.Yes) return;

            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;

            var updated = await _customerService.DeleteBalanceHistory(historyId);
            CustomerBalanceUsd = updated.BalanceUsd;
            CustomerBalanceRub = updated.BalanceRub;
            UpdateBalanceColor();

            SuccessMessage = "Balans əməliyyatı uğurla silindi və məbləğ bərpa edildi.";
            await LoadBalanceHistoryAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Əməliyyatı silmək mümkün olmadı: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private async Task RefreshTransactionsAndCards()
    {
        var transactions = await _transactionService.GetTransactionsByCustomer(CustomerId);
        _allTransactions = transactions;
        TransactionCount = transactions.Count;

        // Apply current filters
        ApplyTransactionFilters();

        var report = await _transactionService.CalculateCustomerExpenseAndIncome(CustomerId);
        TotalExpenseUsd = report.TotalExpenseUsd;
        TotalPaidUsd = report.TotalPaidUsd;
        TotalBilledUsd = report.TotalBilledUsd;
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
        CustomerBalanceRub = customer.BalanceRub;
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

        if (CustomerBalanceRub < 0)
            BalanceRubStatusColor = "#C62828"; // Red — borcu var
        else if (CustomerBalanceRub > 0)
            BalanceRubStatusColor = "#2E7D32"; // Green — avansı var
        else
            BalanceRubStatusColor = "#888888"; // Gray — balans
    }

    private void UpdateTransactionStatistics()
    {
        // Statistics are based on ALL transactions (not filtered), to give full picture
        PaidTransactionCount = _allTransactions.Count(t => t.PaymentStatus == PaymentStatus.Paid || t.PaymentStatus == PaymentStatus.PaidFromBalance);
        UnpaidTransactionCount = _allTransactions.Count(t => t.PaymentStatus == PaymentStatus.Unpaid);
    }

    private void ExecuteBalanceHistoryFilter()
    {
        IEnumerable<CustomerBalanceHistoryResponse> filtered = _allBalanceHistories;

        if (BhFilterDateFrom.HasValue)
        {
            filtered = filtered.Where(h => h.CreatedAt >= BhFilterDateFrom.Value.Date);
        }
        if (BhFilterDateTo.HasValue)
        {
            // Include the entire end date
            filtered = filtered.Where(h => h.CreatedAt < BhFilterDateTo.Value.Date.AddDays(1));
        }
        if (BhFilterKassa.HasValue)
        {
            filtered = filtered.Where(h => h.Currency == BhFilterKassa.Value);
        }

        // Default sort: newest first
        var result = filtered.OrderByDescending(h => h.CreatedAt).ToList();

        BalanceHistories = new ObservableCollection<CustomerBalanceHistoryResponse>(result);
    }
}
