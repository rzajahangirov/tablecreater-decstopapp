using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;
using TableCreater.WPF.Services;

namespace TableCreater.WPF.Views.Dialogs;

/// <summary>
/// Two-step wizard dialog for Excel export:
///   Step 1 — Select which transactions to export (with in-dialog filtering & checkboxes)
///   Step 2 — Select which columns to include in the export
/// </summary>
public partial class ExcelExportColumnsDialog : Window
{
    private readonly long _customerId;
    private readonly string _customerName;
    private readonly IExcelService _excelService;
    private readonly List<TransactionReadResponse>? _filteredTransactions;
    private readonly List<TransactionReadResponse> _allTransactions;
    private readonly List<ExportColumnOption> _columnOptions = new();

    // Wrapper for mutable IsSelected binding in DataGrid
    private readonly ObservableCollection<TransactionExportItem> _exportItems = new();
    private readonly ICollectionView _txView;

    private int _currentStep = 1;
    private bool _isLoaded;

    /// <summary>
    /// Constructor for the two-step Excel export wizard.
    /// </summary>
    /// <param name="customerId">Customer ID</param>
    /// <param name="customerName">Customer display name</param>
    /// <param name="excelService">Excel export service</param>
    /// <param name="allTransactions">All transactions for the customer (shown in grid)</param>
    /// <param name="filteredTransactions">Currently filtered transactions (optional, for "Select Filtered" button)</param>
    public ExcelExportColumnsDialog(
        long customerId,
        string customerName,
        IExcelService excelService,
        IEnumerable<TransactionReadResponse> allTransactions,
        IEnumerable<TransactionReadResponse>? filteredTransactions = null)
    {
        InitializeComponent();

        _customerId = customerId;
        _customerName = customerName;
        _excelService = excelService;
        _allTransactions = allTransactions.ToList();
        _filteredTransactions = filteredTransactions?.ToList();

        // Build mutable wrappers for every transaction — all selected by default
        foreach (var tx in _allTransactions)
        {
            _exportItems.Add(new TransactionExportItem(tx) { IsSelectedForExport = true });
        }

        _txView = CollectionViewSource.GetDefaultView(_exportItems);
        _txView.Filter = FilterTxPredicate;
        TxSelectionGrid.ItemsSource = _txView;

        InitializeColumnOptions();
        SetupStep1UI();
        _isLoaded = true;
        UpdateTxSelectionSummary();
    }

    // ═══════════════════════════════════════════════════════════════════
    // STEP 1: Transaction Selection
    // ═══════════════════════════════════════════════════════════════════

    private void SetupStep1UI()
    {
        int total = _exportItems.Count;
        if (TxtSubtitle != null)
            TxtSubtitle.Text = $"{_customerName} — {total} tranzaksiya mövcuddur";

        // If a subset was passed from outside, select those by default
        if (_filteredTransactions != null && _filteredTransactions.Count > 0 && _filteredTransactions.Count < _allTransactions.Count)
        {
            var filteredIds = new HashSet<long>(_filteredTransactions.Select(t => t.Id));
            foreach (var item in _exportItems)
            {
                item.IsSelectedForExport = filteredIds.Contains(item.Transaction.Id);
            }
        }
    }

    private bool FilterTxPredicate(object obj)
    {
        if (obj is not TransactionExportItem item) return false;
        var tx = item.Transaction;

        // Date From
        if (DpFilterFrom?.SelectedDate.HasValue == true)
        {
            var filterFrom = DateOnly.FromDateTime(DpFilterFrom.SelectedDate.Value);
            if (tx.TransactionDate < filterFrom) return false;
        }

        // Date To
        if (DpFilterTo?.SelectedDate.HasValue == true)
        {
            var filterTo = DateOnly.FromDateTime(DpFilterTo.SelectedDate.Value);
            if (tx.TransactionDate > filterTo) return false;
        }

        // Payment status
        if (CmbFilterPayment?.SelectedItem is ComboBoxItem payItem && payItem.Tag is string payTag && payTag != "All")
        {
            if (payTag == "Paid" && tx.PaymentStatus != PaymentStatus.Paid) return false;
            if (payTag == "PaidFromBalance" && tx.PaymentStatus != PaymentStatus.PaidFromBalance) return false;
            if (payTag == "Unpaid" && tx.PaymentStatus != PaymentStatus.Unpaid) return false;
        }

        // Search text
        if (!string.IsNullOrWhiteSpace(TxtFilterSearch?.Text))
        {
            string q = TxtFilterSearch.Text.Trim();
            bool match = (tx.ProductName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                         || (tx.SendingCompany?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                         || (tx.ReceivingCompany?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                         || tx.Id.ToString().Contains(q);
            if (!match) return false;
        }

        return true;
    }

    private void FilterInput_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _txView == null) return;
        _txView.Refresh();
        UpdateTxSelectionSummary();
    }

    private void FilterSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isLoaded || _txView == null) return;
        _txView.Refresh();
        UpdateTxSelectionSummary();
    }

    private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
    {
        if (DpFilterFrom != null) DpFilterFrom.SelectedDate = null;
        if (DpFilterTo != null) DpFilterTo.SelectedDate = null;
        if (CmbFilterPayment != null) CmbFilterPayment.SelectedIndex = 0;
        if (TxtFilterSearch != null) TxtFilterSearch.Text = string.Empty;
        if (_isLoaded && _txView != null)
        {
            _txView.Refresh();
            UpdateTxSelectionSummary();
        }
    }

    private void BtnSelectFilteredTx_Click(object sender, RoutedEventArgs e)
    {
        if (_txView == null) return;
        foreach (var obj in _txView)
        {
            if (obj is TransactionExportItem item) item.IsSelectedForExport = true;
        }
        TxSelectionGrid.Items.Refresh();
        UpdateTxSelectionSummary();
    }

    private void BtnDeselectFilteredTx_Click(object sender, RoutedEventArgs e)
    {
        if (_txView == null) return;
        foreach (var obj in _txView)
        {
            if (obj is TransactionExportItem item) item.IsSelectedForExport = false;
        }
        TxSelectionGrid.Items.Refresh();
        UpdateTxSelectionSummary();
    }

    private void BtnSelectAllTx_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _exportItems) item.IsSelectedForExport = true;
        TxSelectionGrid.Items.Refresh();
        UpdateTxSelectionSummary();
    }

    private void BtnDeselectAllTx_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _exportItems) item.IsSelectedForExport = false;
        TxSelectionGrid.Items.Refresh();
        UpdateTxSelectionSummary();
    }

    private void BtnInvertSelectionTx_Click(object sender, RoutedEventArgs e)
    {
        if (_txView == null) return;
        foreach (var obj in _txView)
        {
            if (obj is TransactionExportItem item) item.IsSelectedForExport = !item.IsSelectedForExport;
        }
        TxSelectionGrid.Items.Refresh();
        UpdateTxSelectionSummary();
    }

    private void HeaderTxCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox chk && _txView != null)
        {
            bool targetState = chk.IsChecked == true;
            foreach (var obj in _txView)
            {
                if (obj is TransactionExportItem item) item.IsSelectedForExport = targetState;
            }
            TxSelectionGrid.Items.Refresh();
            UpdateTxSelectionSummary();
        }
    }

    private void TxCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateTxSelectionSummary();
    }

    private void UpdateTxSelectionSummary()
    {
        if (!_isLoaded) return;

        var selected = _exportItems.Where(x => x.IsSelectedForExport).ToList();
        int total = _exportItems.Count;
        int count = selected.Count;
        int visibleCount = _txView?.Cast<object>().Count() ?? total;
        int selectedVisible = _txView?.OfType<TransactionExportItem>().Count(x => x.IsSelectedForExport) ?? count;

        if (TxtSelectedTxCount != null)
            TxtSelectedTxCount.Text = count.ToString();

        if (TxtTotalTxCount != null)
        {
            TxtTotalTxCount.Text = visibleCount == total
                ? $" / {total}"
                : $" / {total} (Göstərilən: {visibleCount})";
        }

        decimal totalBilled = selected.Sum(x => x.Transaction.HistoricalCustomerBilledUsd);
        decimal totalPaid = selected.Sum(x => x.Transaction.PaidInUsd);
        decimal totalWeight = selected.Sum(x => x.Transaction.WeightTon);

        if (TxtSelectedBilled != null)
            TxtSelectedBilled.Text = $"${totalBilled:N2}";

        if (TxtSelectedPaid != null)
            TxtSelectedPaid.Text = $"${totalPaid:N2}";

        if (TxtSelectedWeight != null)
            TxtSelectedWeight.Text = $"{totalWeight:N2} T";

        if (ChkSelectAllTxHeader != null)
        {
            if (visibleCount == 0 || selectedVisible == 0)
            {
                ChkSelectAllTxHeader.IsChecked = false;
            }
            else if (selectedVisible == visibleCount)
            {
                ChkSelectAllTxHeader.IsChecked = true;
            }
            else
            {
                ChkSelectAllTxHeader.IsChecked = null;
            }
        }

        if (TxtError != null)
            TxtError.Text = string.Empty;
    }

    // ═══════════════════════════════════════════════════════════════════
    // STEP 2: Column Selection
    // ═══════════════════════════════════════════════════════════════════

    private void InitializeColumnOptions()
    {
        // 1. Əsas Məlumatlar
        AddOption("Id", "№ (Sıra nömrəsi)", "📌 Əsas Məlumatlar", isDefault: true);
        AddOption("TransactionDate", "Tarix", "📌 Əsas Məlumatlar", isDefault: true);
        AddOption("ProductName", "Məhsulun Adı", "📌 Əsas Məlumatlar", isDefault: true);
        AddOption("SendingCompany", "Göndərən Firma", "📌 Əsas Məlumatlar", isDefault: true);
        AddOption("ReceivingCompany", "Qəbul Edən Firma", "📌 Əsas Məlumatlar", isDefault: true);
        AddOption("WeightTon", "Çəki (Ton)", "📌 Əsas Məlumatlar", isDefault: true);
        AddOption("PricePerTonRub", "Qiymət / Ton (₽)", "📌 Əsas Məlumatlar", isDefault: true);

        // 2. Nəqliyyat
        AddOption("TransportType", "Nəqliyyat Növü (TIR / Vaqon)", "🚛 Nəqliyyat", isDefault: true);
        AddOption("VehicleCount", "Vasitə Sayı", "🚛 Nəqliyyat", isDefault: true);
        AddOption("PricePerVehicle", "Nəqliyyat Qiyməti", "🚛 Nəqliyyat", isDefault: true);
        AddOption("TransportCurrency", "Nəqliyyat Valyutası", "🚛 Nəqliyyat", isDefault: false);

        // 3. Əlavə Xərclər və Məzənnə
        AddOption("AdditionalExpenseAmount", "Əlavə Xərc Məbləği", "💰 Əlavə Xərclər və Məzənnə", isDefault: true);
        AddOption("AdditionalExpenseCurrency", "Əlavə Xərc Valyutası", "💰 Əlavə Xərclər və Məzənnə", isDefault: false);
        AddOption("AdditionalExpenseDescription", "Əlavə Xərc Təsviri / Qeyd", "💰 Əlavə Xərclər və Məzənnə", isDefault: true);
        AddOption("HistoricalExchangeRate", "Məzənnə (USD / RUB)", "💰 Əlavə Xərclər və Məzənnə", isDefault: true);

        // 4. Maliyyə və Hesablaşma
        AddOption("HistoricalTotalExpenseUsd", "Ümumi Xərc (USD)", "📊 Maliyyə və Hesablaşma", isDefault: true);
        AddOption("ProfitPerTon", "Ton Başına Qazanc", "📊 Maliyyə və Hesablaşma", isDefault: false);
        AddOption("ProfitPerTonCurrency", "Qazanc Valyutası", "📊 Maliyyə və Hesablaşma", isDefault: false);
        AddOption("HistoricalUserProfitUsd", "Şirkət Qazancı (USD)", "📊 Maliyyə və Hesablaşma", isDefault: true);
        AddOption("HistoricalCustomerBilledUsd", "Xərc + Qazanc (USD)", "📊 Maliyyə və Hesablaşma", isDefault: true);
        AddOption("PaidAmount", "Ödənilən Məbləğ", "📊 Maliyyə və Hesablaşma", isDefault: true);
        AddOption("PaidCurrency", "Ödəniş Valyutası", "📊 Maliyyə və Hesablaşma", isDefault: false);
        AddOption("PaidInUsd", "Ödəniş (USD ekvivalenti)", "📊 Maliyyə və Hesablaşma", isDefault: true);
        AddOption("PaidFromUsdAmount", "Kassadan Ödəniş (USD)", "📊 Maliyyə və Hesablaşma", isDefault: false);
        AddOption("PaidFromRubAmount", "Kassadan Ödəniş (RUB)", "📊 Maliyyə və Hesablaşma", isDefault: false);
        AddOption("PaymentStatus", "Ödəniş Statusu", "📊 Maliyyə və Hesablaşma", isDefault: true);
        AddOption("HistoricalBalanceDeltaUsd", "Balans Təsiri (USD)", "📊 Maliyyə və Hesablaşma", isDefault: true);

        // 5. Status və Tarixlər
        AddOption("ShipmentStatus", "Göndərmə Statusu", "📅 Status və Tarixlər", isDefault: true);
        AddOption("LoadedDate", "Yüklənmə Tarixi", "📅 Status və Tarixlər", isDefault: false);
        AddOption("InTransitStartDate", "Yola Çıxma Tarixi", "📅 Status və Tarixlər", isDefault: false);
        AddOption("DeliveredDate", "Çatdırılma Tarixi", "📅 Status və Tarixlər", isDefault: false);
    }

    private void AddOption(string id, string headerName, string category, bool isDefault)
    {
        _columnOptions.Add(new ExportColumnOption
        {
            Id = id,
            HeaderName = headerName,
            Category = category,
            IsDefault = isDefault,
            IsSelected = isDefault
        });
    }

    private void SetupStep2UI()
    {
        var selected = _exportItems.Where(x => x.IsSelectedForExport).ToList();
        TxtSubtitle.Text = $"{_customerName} — {selected.Count} tranzaksiya seçilib, sütunları seçin";

        // Render category cards
        var groups = _columnOptions.GroupBy(o => o.Category);
        PanelCategories.Children.Clear();

        foreach (var group in groups)
        {
            var card = CreateCategoryCard(group.Key, group.ToList());
            PanelCategories.Children.Add(card);
        }

        UpdateColumnSelectedCount();
    }

    private Border CreateCategoryCard(string title, List<ExportColumnOption> options)
    {
        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 14),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var stack = new StackPanel();

        // Category Header
        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            Margin = new Thickness(0, 0, 0, 10)
        };
        stack.Children.Add(titleBlock);

        // CheckBoxes layout (UniformGrid 2 columns)
        var grid = new UniformGrid
        {
            Columns = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        foreach (var opt in options)
        {
            var chk = new CheckBox
            {
                Content = opt.HeaderName,
                IsChecked = opt.IsSelected,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(4, 5, 8, 5),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = opt
            };

            chk.Checked += (s, e) =>
            {
                opt.IsSelected = true;
                UpdateColumnSelectedCount();
            };
            chk.Unchecked += (s, e) =>
            {
                opt.IsSelected = false;
                UpdateColumnSelectedCount();
            };

            grid.Children.Add(chk);
        }

        stack.Children.Add(grid);
        border.Child = stack;
        return border;
    }

    private void UpdateColumnSelectedCount()
    {
        int selected = _columnOptions.Count(o => o.IsSelected);
        int total = _columnOptions.Count;
        TxtSelectedColCount.Text = $"{selected} / {total}";
        TxtError.Text = string.Empty;
    }

    private void RefreshColumnCheckBoxes()
    {
        foreach (var child in PanelCategories.Children)
        {
            if (child is Border b && b.Child is StackPanel sp)
            {
                foreach (var spChild in sp.Children)
                {
                    if (spChild is UniformGrid ug)
                    {
                        foreach (var ugChild in ug.Children)
                        {
                            if (ugChild is CheckBox chk && chk.Tag is ExportColumnOption opt)
                            {
                                chk.IsChecked = opt.IsSelected;
                            }
                        }
                    }
                }
            }
        }
        UpdateColumnSelectedCount();
    }

    // ── Column Action Buttons ────────────────────────────────────────

    private void BtnSelectAllCols_Click(object sender, RoutedEventArgs e)
    {
        foreach (var opt in _columnOptions) opt.IsSelected = true;
        RefreshColumnCheckBoxes();
    }

    private void BtnDeselectAllCols_Click(object sender, RoutedEventArgs e)
    {
        foreach (var opt in _columnOptions) opt.IsSelected = false;
        RefreshColumnCheckBoxes();
    }

    private void BtnResetDefaultCols_Click(object sender, RoutedEventArgs e)
    {
        foreach (var opt in _columnOptions) opt.IsSelected = opt.IsDefault;
        RefreshColumnCheckBoxes();
    }

    // ═══════════════════════════════════════════════════════════════════
    // WIZARD NAVIGATION
    // ═══════════════════════════════════════════════════════════════════

    private void GoToStep(int step)
    {
        _currentStep = step;

        if (step == 1)
        {
            // Show Step 1, hide Step 2
            PanelStep1.Visibility = Visibility.Visible;
            PanelStep2.Visibility = Visibility.Collapsed;
            BtnBack.Visibility = Visibility.Collapsed;

            // Update stepper indicators
            Step1Circle.Background = new SolidColorBrush(Color.FromRgb(16, 124, 65));
            Step1Label.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));
            Step1Label.FontWeight = FontWeights.Bold;

            Step2Circle.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            Step2Number.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            Step2Label.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            Step2Label.FontWeight = FontWeights.SemiBold;

            StepConnector.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));

            // Button text
            TxtTitle.Text = "Excel İxracı — Tranzaksiya Seçimi";
            BtnNextText.Text = "Davam Et — Sütun Seçimi";
        }
        else
        {
            // Show Step 2, hide Step 1
            PanelStep1.Visibility = Visibility.Collapsed;
            PanelStep2.Visibility = Visibility.Visible;
            BtnBack.Visibility = Visibility.Visible;

            // Update stepper indicators
            Step1Circle.Background = new SolidColorBrush(Color.FromRgb(16, 124, 65));
            Step1Label.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));

            Step2Circle.Background = new SolidColorBrush(Color.FromRgb(16, 124, 65));
            Step2Number.Foreground = Brushes.White;
            Step2Label.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));
            Step2Label.FontWeight = FontWeights.Bold;

            StepConnector.Background = new SolidColorBrush(Color.FromRgb(16, 124, 65));

            // Button text
            TxtTitle.Text = "Excel İxracı — Sütun Seçimi";
            BtnNextText.Text = "Excel-ə İxrac Et";

            SetupStep2UI();
        }

        TxtError.Text = string.Empty;
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        GoToStep(1);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void BtnNextExport_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 1)
        {
            // Validate: at least 1 transaction selected
            int selectedCount = _exportItems.Count(x => x.IsSelectedForExport);
            if (selectedCount == 0)
            {
                TxtError.Text = "Zəhmət olmasa ən azı bir tranzaksiya seçin.";
                return;
            }

            GoToStep(2);
        }
        else
        {
            // Step 2 -> Export
            var selectedColumnIds = _columnOptions.Where(o => o.IsSelected).Select(o => o.Id).ToList();
            if (selectedColumnIds.Count == 0)
            {
                TxtError.Text = "Zəhmət olmasa ən azı bir sütun seçin.";
                return;
            }

            // Get selected transactions
            var selectedTransactions = _exportItems
                .Where(x => x.IsSelectedForExport)
                .Select(x => x.Transaction)
                .ToList();

            string safeName = SanitizeFileName(_customerName);
            var saveDialog = new SaveFileDialog
            {
                Title = "Excel Faylını Yadda Saxla",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"{safeName}_Tranzaksiyalar_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                DefaultExt = ".xlsx"
            };

            if (saveDialog.ShowDialog(this) != true) return;

            try
            {
                IsEnabled = false;
                TxtError.Text = "İxrac edilir, zəhmət olmasa gözləyin...";

                await _excelService.ExportTransactionsToExcel(
                    _customerId,
                    saveDialog.FileName,
                    selectedColumnIds,
                    selectedTransactions);

                if (ChkAutoOpen.IsChecked == true && File.Exists(saveDialog.FileName))
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
                        // Ignore shell opening error
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                IsEnabled = true;
                TxtError.Text = $"Xəta baş verdi: {ex.Message}";
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars).Trim();
    }
}

/// <summary>
/// Mutable wrapper around immutable TransactionReadResponse for DataGrid checkbox binding.
/// Implements INotifyPropertyChanged for proper two-way binding.
/// </summary>
public class TransactionExportItem : INotifyPropertyChanged
{
    private bool _isSelectedForExport;

    public TransactionExportItem(TransactionReadResponse transaction)
    {
        Transaction = transaction;
    }

    public TransactionReadResponse Transaction { get; }

    public bool IsSelectedForExport
    {
        get => _isSelectedForExport;
        set
        {
            if (_isSelectedForExport != value)
            {
                _isSelectedForExport = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelectedForExport)));
            }
        }
    }

    // Proxy properties for DataGrid binding
    public DateOnly TransactionDate => Transaction.TransactionDate;
    public string ProductName => Transaction.ProductName;
    public string? SendingCompany => Transaction.SendingCompany;
    public string WeightTonFormatted => Transaction.WeightTonFormatted;
    public string BilledUsdFormatted => Transaction.BilledUsdFormatted;
    public string PaidUsdFormatted => Transaction.PaidUsdFormatted;
    public string PaymentStatusDisplay => Transaction.PaymentStatusDisplay;
    public string ShipmentStatusDisplay => Transaction.ShipmentStatusDisplay;

    public event PropertyChangedEventHandler? PropertyChanged;
}
