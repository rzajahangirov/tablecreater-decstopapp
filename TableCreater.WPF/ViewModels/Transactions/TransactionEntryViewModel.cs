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
/// ViewModel for a single dynamic additional expense item in the transaction form.
/// </summary>
public partial class AdditionalExpenseItemViewModel : ObservableObject
{
    private readonly Action _recalculateCallback;

    public AdditionalExpenseItemViewModel(Action recalculateCallback)
    {
        _recalculateCallback = recalculateCallback;
    }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private AdditionalExpenseType _type = AdditionalExpenseType.PerTon;

    [ObservableProperty]
    private decimal _amountPerTon;

    [ObservableProperty]
    private PaymentCurrency _currency = PaymentCurrency.Usd;

    [ObservableProperty]
    private decimal _liveTotalAmount;

    public static readonly PaymentCurrency[] AvailableCurrencies = new[] { PaymentCurrency.Usd, PaymentCurrency.Rub };
    public PaymentCurrency[] Currencies => AvailableCurrencies;

    public bool IsPerTon => Type == AdditionalExpenseType.PerTon;
    public bool IsFixed => Type == AdditionalExpenseType.Fixed;

    public bool IsUsd => Currency == PaymentCurrency.Usd;
    public bool IsRub => Currency == PaymentCurrency.Rub;
    public string CurrencySymbol => Currency == PaymentCurrency.Rub ? "₽" : "$";
    public string Uid { get; } = Guid.NewGuid().ToString("N");

    [RelayCommand]
    public void SetPerTon()
    {
        Type = AdditionalExpenseType.PerTon;
    }

    [RelayCommand]
    public void SetFixed()
    {
        Type = AdditionalExpenseType.Fixed;
    }

    [RelayCommand]
    public void SetUsd()
    {
        Currency = PaymentCurrency.Usd;
    }

    [RelayCommand]
    public void SetRub()
    {
        Currency = PaymentCurrency.Rub;
    }

    partial void OnTypeChanged(AdditionalExpenseType value)
    {
        OnPropertyChanged(nameof(IsPerTon));
        OnPropertyChanged(nameof(IsFixed));
        _recalculateCallback();
    }

    partial void OnAmountPerTonChanged(decimal value) => _recalculateCallback();

    partial void OnCurrencyChanged(PaymentCurrency value)
    {
        OnPropertyChanged(nameof(IsUsd));
        OnPropertyChanged(nameof(IsRub));
        OnPropertyChanged(nameof(CurrencySymbol));
        _recalculateCallback();
    }
}

/// <summary>
/// ViewModel for the Transaction Entry form.
/// Updated for dual-stream billing (RUB + USD independently) with
/// dynamic per-ton additional expenses and no PaymentStatus selection.
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

    [ObservableProperty]
    private decimal _customerUsdBalance;

    [ObservableProperty]
    private decimal _customerRubBalance;

    // =========================================================================
    // TRANSACTION INPUT FIELDS
    // =========================================================================

    [ObservableProperty]
    private DateOnly _transactionDate = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private string _receivingCompany = string.Empty;

    [ObservableProperty]
    private string _sendingCompany = string.Empty;

    [ObservableProperty]
    private decimal _weightTon;

    [ObservableProperty]
    private decimal _pricePerTonRub;

    [ObservableProperty]
    private decimal _exchangeRate = 90.0m;

    [ObservableProperty]
    private TransportType _transportType = TransportType.Truck;

    [ObservableProperty]
    private PaymentCurrency _transportCurrency = PaymentCurrency.Usd;

    [ObservableProperty]
    private int _vehicleCount = 1;

    [ObservableProperty]
    private decimal _pricePerVehicle;

    // =========================================================================
    // TON BAŞINA QAZANC
    // =========================================================================

    [ObservableProperty]
    private decimal _profitPerTon;

    [ObservableProperty]
    private PaymentCurrency _profitPerTonCurrency = PaymentCurrency.Usd;

    // =========================================================================
    // DİNAMİK ƏLAVƏ XƏRCLƏR (ton başına, çox saylı)
    // =========================================================================

    public ObservableCollection<AdditionalExpenseItemViewModel> AdditionalExpenses { get; } = new();

    // =========================================================================
    // GÖNDƏRMƏ STATUSU VƏ TARİXLƏR (Shipment Tracking)
    // =========================================================================

    [ObservableProperty]
    private ShipmentStatus _shipmentStatus = ShipmentStatus.Pending;

    [ObservableProperty]
    private DateTime? _loadedDate;

    [ObservableProperty]
    private DateTime? _inTransitStartDate;

    [ObservableProperty]
    private DateTime? _inTransitEndDate;

    [ObservableProperty]
    private DateTime? _deliveredDate;

    [ObservableProperty]
    private bool _isInTransitAutoDates = true;

    public ShipmentStatus[] ShipmentStatuses => Enum.GetValues<ShipmentStatus>();

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
    // LIVE CALCULATION OUTPUTS — İKİLİ VALYUTA (RUB + USD müstəqil)
    // =========================================================================

    // --- RUB Axını ---
    [ObservableProperty]
    private decimal _liveGoodsCostRub;

    [ObservableProperty]
    private decimal _liveTransportCostRub;

    [ObservableProperty]
    private decimal _liveAdditionalExpensesRub;

    [ObservableProperty]
    private decimal _liveProfitRub;

    [ObservableProperty]
    private decimal _liveTotalBilledRub;

    // --- USD Axını ---
    [ObservableProperty]
    private decimal _liveTransportCostUsd;

    [ObservableProperty]
    private decimal _liveAdditionalExpensesUsd;

    [ObservableProperty]
    private decimal _liveProfitUsd;

    [ObservableProperty]
    private decimal _liveTotalBilledUsd;

    public decimal LiveNewRubBalance => CustomerRubBalance - LiveTotalBilledRub;
    public decimal LiveNewUsdBalance => CustomerUsdBalance - LiveTotalBilledUsd;

    public decimal LiveAdditionalExpensesUnifiedUsd => LiveAdditionalExpensesUsd + (ExchangeRate > 0 ? LiveAdditionalExpensesRub / ExchangeRate : 0m);

    // --- Vahid Hesabat (Məzənnə ilə USD Ekvivalenti) ---
    [ObservableProperty]
    private decimal _liveBaseCostRub;

    [ObservableProperty]
    private decimal _liveBaseCostUsd;

    [ObservableProperty]
    private decimal _liveBaseCostUnifiedUsd;

    [ObservableProperty]
    private decimal _liveProfitUnifiedUsd;

    [ObservableProperty]
    private decimal _liveTotalExpenseUnifiedUsd;

    // --- Legacy (backward compat for old calculation panel references) ---
    [ObservableProperty]
    private decimal _liveGoodsCostUsd;

    [ObservableProperty]
    private decimal _liveTransportCostRaw;

    [ObservableProperty]
    private decimal _liveTotalExpenseUsd;

    [ObservableProperty]
    private decimal _liveUserProfitUsd;

    [ObservableProperty]
    private decimal _liveCustomerBilledUsd;

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

    [ObservableProperty]
    private long? _presetCustomerId;

    // =========================================================================
    // ENUM SOURCES
    // =========================================================================

    public static TransportType[] TransportTypes { get; } = Enum.GetValues<TransportType>();
    public static PaymentCurrency[] PaymentCurrencies { get; } = Enum.GetValues<PaymentCurrency>();

    // =========================================================================
    // REACTIVE RECALCULATION — partial OnXxxChanged
    // =========================================================================

    partial void OnWeightTonChanged(decimal value) { UpdateExpenseTotals(); Recalculate(); }
    partial void OnPricePerTonRubChanged(decimal value) => Recalculate();
    partial void OnExchangeRateChanged(decimal value) => Recalculate();
    partial void OnTransportTypeChanged(TransportType value) => Recalculate();
    partial void OnTransportCurrencyChanged(PaymentCurrency value) => Recalculate();
    partial void OnVehicleCountChanged(int value) => Recalculate();
    partial void OnPricePerVehicleChanged(decimal value) => Recalculate();
    partial void OnProfitPerTonChanged(decimal value) => Recalculate();
    partial void OnProfitPerTonCurrencyChanged(PaymentCurrency value) => Recalculate();

    /// <summary>
    /// Updates LiveTotalAmount on each expense item based on current WeightTon.
    /// </summary>
    private void UpdateExpenseTotals()
    {
        foreach (var item in AdditionalExpenses)
        {
            item.LiveTotalAmount = item.Type == AdditionalExpenseType.PerTon 
                ? (WeightTon * item.AmountPerTon) 
                : item.AmountPerTon;
        }
    }

    // =========================================================================
    // CORE CALCULATION ENGINE — İKİLİ VALYUTA (Dual-Stream)
    //
    // RUB Stream: GoodsCost(RUB) + Transport(RUB) + AddlExpenses(RUB) + Profit(RUB)
    // USD Stream: Transport(USD) + AddlExpenses(USD) + Profit(USD)
    //
    // Heç bir valyuta konvertasiyası yoxdur! Hər axın müstəqildir.
    // =========================================================================

    private void Recalculate()
    {
        // === RUB AXINI ===

        // Mal dəyəri — həmişə RUB-dur
        LiveGoodsCostRub = WeightTon * PricePerTonRub;

        // Nəqliyyat RUB
        LiveTransportCostRaw = PricePerVehicle * VehicleCount;
        LiveTransportCostRub = TransportCurrency == PaymentCurrency.Rub ? LiveTransportCostRaw : 0m;

        // Əlavə xərclər RUB
        LiveAdditionalExpensesRub = 0m;
        foreach (var item in AdditionalExpenses)
        {
            if (item.Currency == PaymentCurrency.Rub)
            {
                LiveAdditionalExpensesRub += item.Type == AdditionalExpenseType.PerTon 
                    ? (WeightTon * item.AmountPerTon) 
                    : item.AmountPerTon;
            }
        }

        // Qazanc RUB
        LiveProfitRub = ProfitPerTonCurrency == PaymentCurrency.Rub
            ? WeightTon * ProfitPerTon : 0m;

        // Maya Dəyəri və Cəmi RUB
        LiveBaseCostRub = LiveGoodsCostRub + LiveTransportCostRub + LiveAdditionalExpensesRub;
        LiveTotalBilledRub = LiveBaseCostRub + LiveProfitRub;

        // === USD AXINI ===

        // Nəqliyyat USD
        LiveTransportCostUsd = TransportCurrency == PaymentCurrency.Usd ? LiveTransportCostRaw : 0m;

        // Əlavə xərclər USD
        LiveAdditionalExpensesUsd = 0m;
        foreach (var item in AdditionalExpenses)
        {
            if (item.Currency == PaymentCurrency.Usd)
            {
                LiveAdditionalExpensesUsd += item.Type == AdditionalExpenseType.PerTon 
                    ? (WeightTon * item.AmountPerTon) 
                    : item.AmountPerTon;
            }
        }

        // Qazanc USD
        LiveProfitUsd = ProfitPerTonCurrency == PaymentCurrency.Usd
            ? WeightTon * ProfitPerTon : 0m;

        // Maya Dəyəri və Cəmi USD
        LiveBaseCostUsd = LiveTransportCostUsd + LiveAdditionalExpensesUsd;
        LiveTotalBilledUsd = LiveBaseCostUsd + LiveProfitUsd;

        // === VAHİD HESABAT (RUB -> USD MƏZƏNNƏ İLƏ KONVERTASİYA) ===
        decimal rate = ExchangeRate > 0 ? ExchangeRate : 1.0m;
        LiveBaseCostUnifiedUsd = LiveBaseCostUsd + (LiveBaseCostRub / rate);
        LiveProfitUnifiedUsd = LiveProfitUsd + (LiveProfitRub / rate);
        LiveTotalExpenseUnifiedUsd = LiveTotalBilledUsd + (LiveTotalBilledRub / rate);

        OnPropertyChanged(nameof(LiveNewRubBalance));
        OnPropertyChanged(nameof(LiveNewUsdBalance));
        OnPropertyChanged(nameof(LiveAdditionalExpensesUnifiedUsd));

        // Update each item's live total
        UpdateExpenseTotals();

        // Re-evaluate save command eligibility
        SaveCommand.NotifyCanExecuteChanged();
    }

    // =========================================================================
    // COMMANDS
    // =========================================================================

    [RelayCommand]
    private async Task LoadCustomersAsync()
    {
        try
        {
            var customers = await _customerService.GetAllCustomers();
            Customers = new ObservableCollection<CustomerReadResponse>(customers);

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
            SendingCompany = data.SendingCompany ?? string.Empty;
            WeightTon = data.WeightTon;
            PricePerTonRub = data.PricePerTonRub;
            ExchangeRate = data.HistoricalExchangeRate > 0 ? data.HistoricalExchangeRate : 90.0m;
            TransportType = data.TransportType;
            TransportCurrency = data.TransportCurrency;
            VehicleCount = data.VehicleCount ?? 1;
            PricePerVehicle = data.PricePerVehicle ?? 0m;

            // Profit
            ProfitPerTon = data.ProfitPerTon;
            ProfitPerTonCurrency = data.ProfitPerTonCurrency;

            // Shipment Tracking
            ShipmentStatus = data.ShipmentStatus;
            LoadedDate = data.LoadedDate?.ToDateTime(TimeOnly.MinValue);
            InTransitStartDate = data.InTransitStartDate?.ToDateTime(TimeOnly.MinValue);
            InTransitEndDate = data.InTransitEndDate?.ToDateTime(TimeOnly.MinValue);
            DeliveredDate = data.DeliveredDate?.ToDateTime(TimeOnly.MinValue);
            IsInTransitAutoDates = data.IsInTransitAutoDates;

            // Dynamic Additional Expenses
            AdditionalExpenses.Clear();
            foreach (var exp in data.AdditionalExpenses)
            {
                var item = new AdditionalExpenseItemViewModel(Recalculate)
                {
                    Title = exp.Title,
                    Type = exp.Type,
                    AmountPerTon = exp.AmountPerTon,
                    Currency = exp.Currency,
                    LiveTotalAmount = exp.Type == AdditionalExpenseType.PerTon 
                        ? (WeightTon * exp.AmountPerTon) 
                        : exp.AmountPerTon
                };
                AdditionalExpenses.Add(item);
            }

            // Legacy single expense — convert to dynamic if present and no dynamic expenses loaded
            if (AdditionalExpenses.Count == 0 && data.AdditionalExpenseAmount.HasValue && data.AdditionalExpenseAmount.Value > 0)
            {
                var legacyItem = new AdditionalExpenseItemViewModel(Recalculate)
                {
                    Title = data.AdditionalExpenseDescription ?? "Əlavə xərc",
                    AmountPerTon = data.AdditionalExpenseAmount.Value,
                    Currency = data.AdditionalExpenseCurrency ?? PaymentCurrency.Usd,
                    LiveTotalAmount = WeightTon * data.AdditionalExpenseAmount.Value
                };
                AdditionalExpenses.Add(legacyItem);
            }

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

    [RelayCommand]
    private void ClearFile()
    {
        DocumentFilePath = null;
        DocumentFileName = null;
        HasDocument = false;
    }

    /// <summary>
    /// Adds a new blank additional expense item to the list.
    /// </summary>
    [RelayCommand]
    private void AddAdditionalExpense()
    {
        var item = new AdditionalExpenseItemViewModel(Recalculate)
        {
            LiveTotalAmount = 0m
        };
        AdditionalExpenses.Add(item);
    }

    /// <summary>
    /// Removes a specific additional expense item from the list.
    /// </summary>
    [RelayCommand]
    private void RemoveAdditionalExpense(AdditionalExpenseItemViewModel item)
    {
        AdditionalExpenses.Remove(item);
        Recalculate();
    }

    /// <summary>
    /// Builds the list of additional expense DTOs from the ViewModel collection.
    /// </summary>
    private List<TransactionAdditionalExpenseDto> BuildAdditionalExpenseDtos()
    {
        var list = new List<TransactionAdditionalExpenseDto>();
        foreach (var item in AdditionalExpenses)
        {
            if (item.AmountPerTon > 0)
            {
                list.Add(new TransactionAdditionalExpenseDto
                {
                    Title = string.IsNullOrWhiteSpace(item.Title) ? "Əlavə xərc" : item.Title,
                    Type = item.Type,
                    AmountPerTon = item.AmountPerTon,
                    Currency = item.Currency,
                    TotalAmount = item.LiveTotalAmount
                });
            }
        }
        return list;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;

            var additionalExpenses = BuildAdditionalExpenseDtos();

            if (IsEditMode)
            {
                var request = new TransactionUpdateRequest
                {
                    TransactionDate = TransactionDate,
                    ProductName = ProductName,
                    ReceivingCompany = ReceivingCompany,
                    SendingCompany = string.IsNullOrWhiteSpace(SendingCompany) ? null : SendingCompany,
                    WeightTon = WeightTon,
                    PricePerTonRub = PricePerTonRub,
                    TransportType = TransportType,
                    TransportCurrency = TransportCurrency,
                    VehicleCount = VehicleCount,
                    PricePerVehicle = PricePerVehicle,
                    HistoricalExchangeRate = ExchangeRate > 0 ? ExchangeRate : 1.0m,
                    DocumentFilePath = DocumentFilePath,
                    AdditionalExpenses = additionalExpenses,
                    ProfitPerTon = ProfitPerTon,
                    ProfitPerTonCurrency = ProfitPerTonCurrency,
                    ShipmentStatus = ShipmentStatus,
                    LoadedDate = LoadedDate.HasValue ? DateOnly.FromDateTime(LoadedDate.Value) : null,
                    InTransitStartDate = InTransitStartDate.HasValue ? DateOnly.FromDateTime(InTransitStartDate.Value) : null,
                    InTransitEndDate = (ShipmentStatus == ShipmentStatus.Delivered && DeliveredDate.HasValue) ? DateOnly.FromDateTime(DeliveredDate.Value) : null,
                    DeliveredDate = DeliveredDate.HasValue ? DateOnly.FromDateTime(DeliveredDate.Value) : null,
                    IsInTransitAutoDates = true,
                    IsCompleted = ShipmentStatus == ShipmentStatus.Delivered,
                    PaymentStatus = PaymentStatus.Unpaid,
                    PaidCurrency = PaymentCurrency.Usd
                };

                await _transactionService.UpdateTransaction(EditTransactionId, request);
                SuccessMessage = "Tranzaksiya uğurla yeniləndi!";

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
                    SendingCompany = string.IsNullOrWhiteSpace(SendingCompany) ? null : SendingCompany,
                    WeightTon = WeightTon,
                    PricePerTonRub = PricePerTonRub,
                    TransportType = TransportType,
                    TransportCurrency = TransportCurrency,
                    VehicleCount = VehicleCount,
                    PricePerVehicle = PricePerVehicle,
                    HistoricalExchangeRate = ExchangeRate > 0 ? ExchangeRate : 1.0m,
                    DocumentFilePath = DocumentFilePath,
                    AdditionalExpenses = additionalExpenses,
                    ProfitPerTon = ProfitPerTon,
                    ProfitPerTonCurrency = ProfitPerTonCurrency,
                    ShipmentStatus = ShipmentStatus,
                    LoadedDate = LoadedDate.HasValue ? DateOnly.FromDateTime(LoadedDate.Value) : null,
                    InTransitStartDate = InTransitStartDate.HasValue ? DateOnly.FromDateTime(InTransitStartDate.Value) : null,
                    InTransitEndDate = (ShipmentStatus == ShipmentStatus.Delivered && DeliveredDate.HasValue) ? DateOnly.FromDateTime(DeliveredDate.Value) : null,
                    DeliveredDate = DeliveredDate.HasValue ? DateOnly.FromDateTime(DeliveredDate.Value) : null,
                    IsInTransitAutoDates = true,
                    PaymentStatus = PaymentStatus.Unpaid,
                    PaidCurrency = PaymentCurrency.Usd
                };

                await _transactionService.CreateTransaction(request, SelectedCustomer!.Id);
                SuccessMessage = "Tranzaksiya uğurla yaradıldı!";

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

    private bool CanSave()
    {
        return SelectedCustomer != null
            && !string.IsNullOrWhiteSpace(ProductName)
            && !string.IsNullOrWhiteSpace(ReceivingCompany)
            && WeightTon > 0
            && PricePerTonRub >= 0;
    }

    partial void OnSelectedCustomerChanged(CustomerReadResponse? value)
    {
        if (value != null)
        {
            CustomerSearchText = value.Name;
            CustomerUsdBalance = value.BalanceUsd;
            CustomerRubBalance = value.BalanceRub;
        }
        else
        {
            CustomerUsdBalance = 0m;
            CustomerRubBalance = 0m;
        }
        OnPropertyChanged(nameof(LiveNewRubBalance));
        OnPropertyChanged(nameof(LiveNewUsdBalance));
        SaveCommand.NotifyCanExecuteChanged();
    }
    partial void OnProductNameChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
    partial void OnReceivingCompanyChanged(string value) => SaveCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void ResetForm()
    {
        TransactionDate = DateOnly.FromDateTime(DateTime.Today);
        ProductName = string.Empty;
        ReceivingCompany = string.Empty;
        SendingCompany = string.Empty;
        WeightTon = 0;
        PricePerTonRub = 0;
        TransportType = TransportType.Truck;
        TransportCurrency = PaymentCurrency.Usd;
        VehicleCount = 1;
        PricePerVehicle = 0;
        ProfitPerTon = 0;
        ProfitPerTonCurrency = PaymentCurrency.Usd;
        ShipmentStatus = ShipmentStatus.Pending;
        LoadedDate = null;
        InTransitStartDate = null;
        InTransitEndDate = null;
        DeliveredDate = null;
        IsInTransitAutoDates = true;
        AdditionalExpenses.Clear();
        ClearFile();
        ErrorMessage = null;
        SuccessMessage = null;
        Recalculate();
    }
}
