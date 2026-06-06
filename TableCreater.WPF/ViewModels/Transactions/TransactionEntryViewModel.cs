using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;
using TableCreater.WPF.Services;

namespace TableCreater.WPF.ViewModels.Transactions;

/// <summary>
/// ViewModel for the Transaction Entry form.
/// Implements the LIVE Calculation Engine from Section 5.1 — every input change
/// immediately recalculates TotalExpenseUsd and RemainingDebtUsd in real-time.
///
/// Uses CommunityToolkit.Mvvm partial methods (OnXxxChanged) for reactive updates.
/// </summary>
public partial class TransactionEntryViewModel : ObservableObject
{
    private readonly ITransactionService _transactionService;
    private readonly ICustomerService _customerService;

    public TransactionEntryViewModel(
        ITransactionService transactionService,
        ICustomerService customerService)
    {
        _transactionService = transactionService;
        _customerService = customerService;
    }

    // =========================================================================
    // CUSTOMER SELECTION
    // =========================================================================

    [ObservableProperty]
    private ObservableCollection<CustomerReadResponse> _customers = new();

    [ObservableProperty]
    private CustomerReadResponse? _selectedCustomer;

    [ObservableProperty]
    private string _customerSearchText = string.Empty;

    // =========================================================================
    // TRANSACTION INPUT FIELDS
    // All fields trigger Recalculate() on change via partial OnXxxChanged methods.
    // =========================================================================

    [ObservableProperty]
    private DateOnly _transactionDate = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private string _receivingCompany = string.Empty;

    [ObservableProperty]
    private decimal _weightTon;

    [ObservableProperty]
    private decimal _pricePerTonRub;

    [ObservableProperty]
    private TransportType _transportType = TransportType.Truck;

    [ObservableProperty]
    private int _vehicleCount = 1;

    [ObservableProperty]
    private decimal _pricePerVehicle;

    [ObservableProperty]
    private PaymentCurrency _paidCurrency = PaymentCurrency.Usd;

    [ObservableProperty]
    private decimal _paidAmount;

    [ObservableProperty]
    private decimal _historicalExchangeRate = 1.0m;

    // =========================================================================
    // FILE UPLOAD
    // =========================================================================

    [ObservableProperty]
    private string? _documentFilePath;

    [ObservableProperty]
    private string? _documentFileName;

    [ObservableProperty]
    private bool _hasDocument;

    // =========================================================================
    // LIVE CALCULATION OUTPUTS (Read-only, updated reactively)
    // =========================================================================

    [ObservableProperty]
    private decimal _liveGoodsCostRub;

    [ObservableProperty]
    private decimal _liveGoodsCostUsd;

    [ObservableProperty]
    private decimal _liveTransportCostRaw;

    [ObservableProperty]
    private decimal _liveTransportCostUsd;

    [ObservableProperty]
    private decimal _liveTotalExpenseUsd;

    [ObservableProperty]
    private decimal _livePaidInUsd;

    [ObservableProperty]
    private decimal _liveRemainingDebtUsd;

    /// <summary>
    /// Friendly text for the debt: "Debt" (negative) or "Overpayment" (positive).
    /// </summary>
    [ObservableProperty]
    private string _debtStatusText = "Balanced";

    /// <summary>
    /// Color indicator: Red for debt, Green for overpayment, Gray for balanced.
    /// </summary>
    [ObservableProperty]
    private string _debtStatusColor = "#888888";

    // =========================================================================
    // UI STATE
    // =========================================================================

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private long _editTransactionId;

    /// <summary>
    /// Pre-set customer ID when navigating from customer detail view.
    /// </summary>
    [ObservableProperty]
    private long? _presetCustomerId;

    // =========================================================================
    // ENUM SOURCES (for ComboBox binding)
    // =========================================================================

    public TransportType[] TransportTypes => Enum.GetValues<TransportType>();
    public PaymentCurrency[] PaymentCurrencies => Enum.GetValues<PaymentCurrency>();

    // =========================================================================
    // REACTIVE RECALCULATION — partial OnXxxChanged methods
    // CommunityToolkit.Mvvm generates these hooks automatically.
    // Every input change triggers instant recalculation.
    // =========================================================================

    partial void OnWeightTonChanged(decimal value) => Recalculate();
    partial void OnPricePerTonRubChanged(decimal value) => Recalculate();
    partial void OnTransportTypeChanged(TransportType value) => Recalculate();
    partial void OnVehicleCountChanged(int value) => Recalculate();
    partial void OnPricePerVehicleChanged(decimal value) => Recalculate();
    partial void OnPaidCurrencyChanged(PaymentCurrency value) => Recalculate();
    partial void OnPaidAmountChanged(decimal value) => Recalculate();
    partial void OnHistoricalExchangeRateChanged(decimal value) => Recalculate();

    // =========================================================================
    // CORE CALCULATION ENGINE — Section 5.1 (Live Preview)
    // Identical algorithm to TransactionService.CalculateHistoricalFields(),
    // but operates on ViewModel properties for instant UI feedback.
    // =========================================================================

    private void Recalculate()
    {
        decimal rate = HistoricalExchangeRate > 0 ? HistoricalExchangeRate : 1m;

        // Step 1 & 2: Goods Cost
        LiveGoodsCostRub = WeightTon * PricePerTonRub;
        LiveGoodsCostUsd = LiveGoodsCostRub * rate;

        // Step 3 & 4: Transport Cost
        LiveTransportCostRaw = PricePerVehicle * VehicleCount;
        LiveTransportCostUsd = TransportType == TransportType.Ship
            ? LiveTransportCostRaw * rate    // Ship: RUB → USD
            : LiveTransportCostRaw;          // Truck: already USD

        // Step 5: Total Expense
        LiveTotalExpenseUsd = LiveGoodsCostUsd + LiveTransportCostUsd;

        // Step 6: Paid in USD
        LivePaidInUsd = PaidCurrency == PaymentCurrency.Rub
            ? PaidAmount * rate              // RUB → USD
            : PaidAmount;                    // Already USD

        // Step 7: Remaining Debt
        LiveRemainingDebtUsd = LivePaidInUsd - LiveTotalExpenseUsd;

        // Update debt status indicator
        if (LiveRemainingDebtUsd < 0)
        {
            DebtStatusText = "Supplier-ə Borc";
            DebtStatusColor = "#C62828";  // Red
        }
        else if (LiveRemainingDebtUsd > 0)
        {
            DebtStatusText = "Artıq Ödəniş";
            DebtStatusColor = "#2E7D32";  // Green
        }
        else
        {
            DebtStatusText = "Balans";
            DebtStatusColor = "#888888";    // Gray
        }

        // Re-evaluate save command eligibility
        SaveCommand.NotifyCanExecuteChanged();
    }

    // =========================================================================
    // COMMANDS
    // =========================================================================

    /// <summary>
    /// Loads the customer list for the selection ComboBox.
    /// </summary>
    [RelayCommand]
    private async Task LoadCustomersAsync()
    {
        try
        {
            var customers = await _customerService.GetAllCustomers();
            Customers = new ObservableCollection<CustomerReadResponse>(customers);

            // If a preset customer ID was provided, auto-select it
            if (PresetCustomerId.HasValue)
            {
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == PresetCustomerId.Value);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Loads existing transaction data for editing.
    /// </summary>
    [RelayCommand]
    private async Task LoadForEditAsync(long transactionId)
    {
        try
        {
            IsLoading = true;
            IsEditMode = true;
            EditTransactionId = transactionId;

            var data = await _transactionService.GetTransactionForUpdate(transactionId);

            TransactionDate = data.TransactionDate;
            ProductName = data.ProductName;
            ReceivingCompany = data.ReceivingCompany;
            WeightTon = data.WeightTon;
            PricePerTonRub = data.PricePerTonRub;
            TransportType = data.TransportType;
            VehicleCount = data.VehicleCount ?? 1;
            PricePerVehicle = data.PricePerVehicle ?? 0m;
            PaidCurrency = data.PaidCurrency;
            PaidAmount = data.PaidAmount ?? 0m;
            HistoricalExchangeRate = data.HistoricalExchangeRate;

            if (!string.IsNullOrEmpty(data.DocumentImageUrl))
            {
                DocumentFilePath = data.DocumentImageUrl;
                DocumentFileName = Path.GetFileName(data.DocumentImageUrl);
                HasDocument = true;
            }

            Recalculate();
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

    /// <summary>
    /// Opens a file dialog to select a document (PDF/JPG/PNG).
    /// Replaces Java's MultipartFile upload.
    /// </summary>
    [RelayCommand]
    private void UploadFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Sənəd Seçin",
            Filter = "Sənədlər|*.pdf;*.jpg;*.jpeg;*.png;*.bmp|Bütün Fayllar|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            DocumentFilePath = dialog.FileName;
            DocumentFileName = Path.GetFileName(dialog.FileName);
            HasDocument = true;
        }
    }

    /// <summary>
    /// Clears the selected document.
    /// </summary>
    [RelayCommand]
    private void ClearFile()
    {
        DocumentFilePath = null;
        DocumentFileName = null;
        HasDocument = false;
    }

    /// <summary>
    /// Saves the transaction (create or update).
    /// Can only execute when required fields are valid.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;

            if (IsEditMode)
            {
                var request = new TransactionUpdateRequest
                {
                    TransactionDate = TransactionDate,
                    ProductName = ProductName,
                    ReceivingCompany = ReceivingCompany,
                    WeightTon = WeightTon,
                    PricePerTonRub = PricePerTonRub,
                    TransportType = TransportType,
                    VehicleCount = VehicleCount,
                    PricePerVehicle = PricePerVehicle,
                    PaidCurrency = PaidCurrency,
                    PaidAmount = PaidAmount,
                    HistoricalExchangeRate = HistoricalExchangeRate,
                    DocumentFilePath = DocumentFilePath,
                    IsCompleted = false
                };

                await _transactionService.UpdateTransaction(EditTransactionId, request);
                SuccessMessage = "Tranzaksiya uğurla yeniləndi!";

                // Redirect to Customer Details
                var customerId = SelectedCustomer!.Id;
                var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                mainWindow?.NavigateToCustomerDetails(customerId);
            }
            else
            {
                var request = new TransactionCreateRequest
                {
                    TransactionDate = TransactionDate,
                    ProductName = ProductName,
                    ReceivingCompany = ReceivingCompany,
                    WeightTon = WeightTon,
                    PricePerTonRub = PricePerTonRub,
                    TransportType = TransportType,
                    VehicleCount = VehicleCount,
                    PricePerVehicle = PricePerVehicle,
                    PaidCurrency = PaidCurrency,
                    PaidAmount = PaidAmount,
                    HistoricalExchangeRate = HistoricalExchangeRate,
                    DocumentFilePath = DocumentFilePath
                };

                await _transactionService.CreateTransaction(request, SelectedCustomer!.Id);
                SuccessMessage = "Tranzaksiya uğurla yaradıldı!";

                // Redirect to Customer Details
                var customerId = SelectedCustomer.Id;
                var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                mainWindow?.NavigateToCustomerDetails(customerId);
            }
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

    /// <summary>
    /// Validation: Save is only enabled when all required fields are filled.
    /// </summary>
    private bool CanSave()
    {
        return SelectedCustomer != null
            && !string.IsNullOrWhiteSpace(ProductName)
            && !string.IsNullOrWhiteSpace(ReceivingCompany)
            && WeightTon > 0
            && PricePerTonRub >= 0
            && HistoricalExchangeRate > 0;
    }

    // Notify save eligibility on these changes too
    partial void OnSelectedCustomerChanged(CustomerReadResponse? value)
    {
        if (value != null)
        {
            CustomerSearchText = value.Name;
        }
        SaveCommand.NotifyCanExecuteChanged();
    }
    partial void OnProductNameChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
    partial void OnReceivingCompanyChanged(string value) => SaveCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// Resets the form to default values for a new entry.
    /// </summary>
    [RelayCommand]
    private void ResetForm()
    {
        TransactionDate = DateOnly.FromDateTime(DateTime.Today);
        ProductName = string.Empty;
        ReceivingCompany = string.Empty;
        WeightTon = 0;
        PricePerTonRub = 0;
        TransportType = TransportType.Truck;
        VehicleCount = 1;
        PricePerVehicle = 0;
        PaidCurrency = PaymentCurrency.Usd;
        PaidAmount = 0;
        // Keep exchange rate — user likely needs the same rate for multiple entries
        ClearFile();
        ErrorMessage = null;
        SuccessMessage = null;
        Recalculate();
    }
}
