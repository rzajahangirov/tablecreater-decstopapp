using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;
using TableCreater.WPF.Services;

namespace TableCreater.WPF.Views.Dialogs;

/// <summary>
/// Two-step wizard dialog for Balance History Excel export:
///   Step 1 — Select which balance operations to export (with in-dialog filtering & checkboxes)
///   Step 2 — Select which columns to include in the export
/// </summary>
public partial class BalanceHistoryExportDialog : Window
{
    private readonly long _customerId;
    private readonly string _customerName;
    private readonly IExcelService _excelService;
    private readonly List<CustomerBalanceHistoryResponse> _allHistories;
    private readonly List<ExportColumnOption> _columnOptions = new();

    private readonly ObservableCollection<BalanceHistoryExportItem> _exportItems = new();
    private readonly ICollectionView _historyView;
    private int _currentStep = 1;
    private bool _isLoaded;

    public BalanceHistoryExportDialog(
        long customerId,
        string customerName,
        IExcelService excelService,
        IEnumerable<CustomerBalanceHistoryResponse> histories)
    {
        InitializeComponent();

        _customerId = customerId;
        _customerName = customerName;
        _excelService = excelService;
        _allHistories = histories.ToList();

        foreach (var h in _allHistories)
        {
            _exportItems.Add(new BalanceHistoryExportItem(h) { IsSelectedForExport = true });
        }

        _historyView = CollectionViewSource.GetDefaultView(_exportItems);
        _historyView.Filter = FilterHistoryPredicate;
        HistorySelectionGrid.ItemsSource = _historyView;

        InitializeColumnOptions();
        SetupStep1UI();
        _isLoaded = true;
        UpdateSelectionSummary();
    }

    private void SetupStep1UI()
    {
        int total = _exportItems.Count;
        if (TxtSubtitle != null)
            TxtSubtitle.Text = $"{_customerName} — {total} balans əməliyyatı mövcuddur";
    }

    private bool FilterHistoryPredicate(object obj)
    {
        if (obj is not BalanceHistoryExportItem item) return false;
        var h = item.History;

        // Date From
        if (DpFilterFrom?.SelectedDate.HasValue == true && h.CreatedAt.Date < DpFilterFrom.SelectedDate.Value.Date)
            return false;

        // Date To
        if (DpFilterTo?.SelectedDate.HasValue == true && h.CreatedAt.Date > DpFilterTo.SelectedDate.Value.Date)
            return false;

        // Kassa
        if (CmbFilterKassa?.SelectedItem is ComboBoxItem kassaItem && kassaItem.Tag is string kassaTag && kassaTag != "All")
        {
            if (kassaTag == "Usd" && h.Currency != PaymentCurrency.Usd) return false;
            if (kassaTag == "Rub" && h.Currency != PaymentCurrency.Rub) return false;
        }

        // Type
        if (CmbFilterType?.SelectedItem is ComboBoxItem typeItem && typeItem.Tag is string typeTag && typeTag != "All")
        {
            if (typeTag != h.Type.ToString()) return false;
        }

        return true;
    }

    private void FilterInput_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded || _historyView == null) return;
        _historyView.Refresh();
        UpdateSelectionSummary();
    }

    private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
    {
        if (DpFilterFrom != null) DpFilterFrom.SelectedDate = null;
        if (DpFilterTo != null) DpFilterTo.SelectedDate = null;
        if (CmbFilterKassa != null) CmbFilterKassa.SelectedIndex = 0;
        if (CmbFilterType != null) CmbFilterType.SelectedIndex = 0;
        if (_isLoaded && _historyView != null)
        {
            _historyView.Refresh();
            UpdateSelectionSummary();
        }
    }

    private void BtnSelectFiltered_Click(object sender, RoutedEventArgs e)
    {
        if (_historyView == null) return;
        foreach (var obj in _historyView)
        {
            if (obj is BalanceHistoryExportItem item) item.IsSelectedForExport = true;
        }
        HistorySelectionGrid.Items.Refresh();
        UpdateSelectionSummary();
    }

    private void BtnDeselectFiltered_Click(object sender, RoutedEventArgs e)
    {
        if (_historyView == null) return;
        foreach (var obj in _historyView)
        {
            if (obj is BalanceHistoryExportItem item) item.IsSelectedForExport = false;
        }
        HistorySelectionGrid.Items.Refresh();
        UpdateSelectionSummary();
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _exportItems) item.IsSelectedForExport = true;
        HistorySelectionGrid.Items.Refresh();
        UpdateSelectionSummary();
    }

    private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _exportItems) item.IsSelectedForExport = false;
        HistorySelectionGrid.Items.Refresh();
        UpdateSelectionSummary();
    }

    private void BtnInvertSelection_Click(object sender, RoutedEventArgs e)
    {
        if (_historyView == null) return;
        foreach (var obj in _historyView)
        {
            if (obj is BalanceHistoryExportItem item) item.IsSelectedForExport = !item.IsSelectedForExport;
        }
        HistorySelectionGrid.Items.Refresh();
        UpdateSelectionSummary();
    }

    private void HeaderCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox chk && _historyView != null)
        {
            bool targetState = chk.IsChecked == true;
            foreach (var obj in _historyView)
            {
                if (obj is BalanceHistoryExportItem item) item.IsSelectedForExport = targetState;
            }
            HistorySelectionGrid.Items.Refresh();
            UpdateSelectionSummary();
        }
    }

    private void HistoryCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        if (!_isLoaded) return;

        int total = _exportItems.Count;
        int selectedTotal = _exportItems.Count(x => x.IsSelectedForExport);
        int visibleCount = _historyView?.Cast<object>().Count() ?? total;
        int selectedVisible = _historyView?.OfType<BalanceHistoryExportItem>().Count(x => x.IsSelectedForExport) ?? selectedTotal;

        if (TxtSelectedCount != null)
            TxtSelectedCount.Text = selectedTotal.ToString();

        if (TxtTotalCount != null)
        {
            TxtTotalCount.Text = visibleCount == total
                ? $" / {total}"
                : $" / {total} (Göstərilən: {visibleCount})";
        }

        if (ChkSelectAllHeader != null)
        {
            if (visibleCount == 0 || selectedVisible == 0)
            {
                ChkSelectAllHeader.IsChecked = false;
            }
            else if (selectedVisible == visibleCount)
            {
                ChkSelectAllHeader.IsChecked = true;
            }
            else
            {
                ChkSelectAllHeader.IsChecked = null;
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
        AddOption("Index", "№ (Sıra nömrəsi)", "📌 Əsas Məlumatlar", isDefault: true);
        AddOption("CreatedAt", "Tarix və Saat", "📌 Əsas Məlumatlar", isDefault: true);
        AddOption("Currency", "Kassa (USD / RUB)", "📌 Əsas Məlumatlar", isDefault: true);
        AddOption("Type", "Əməliyyat Növü", "📌 Əsas Məlumatlar", isDefault: true);

        AddOption("Amount", "Dəyişiklik Məbləği", "💰 Məbləğ və Balans", isDefault: true);
        AddOption("BalanceAfter", "Yekun Balans (Əməliyyatdan Sonra)", "💰 Məbləğ və Balans", isDefault: true);

        AddOption("TransactionId", "Tranzaksiya ID (Varsa)", "🔗 Əlaqə və Qeydlər", isDefault: true);
        AddOption("Description", "Təsvir / Qeyd", "🔗 Əlaqə və Qeydlər", isDefault: true);

        RenderColumnCategoryCards();
        UpdateSelectedColCount();
    }

    private void AddOption(string id, string header, string category, bool isDefault)
    {
        _columnOptions.Add(new ExportColumnOption
        {
            Id = id,
            HeaderName = header,
            Category = category,
            IsDefault = isDefault,
            IsSelected = isDefault
        });
    }

    private void RenderColumnCategoryCards()
    {
        PanelCategories.Children.Clear();

        var groups = _columnOptions.GroupBy(o => o.Category);

        foreach (var group in groups)
        {
            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 14),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var stack = new StackPanel();

            var headerPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var headerTitle = new TextBlock
            {
                Text = group.Key,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59))
            };
            DockPanel.SetDock(headerTitle, Dock.Left);
            headerPanel.Children.Add(headerTitle);

            var groupSelectAll = new Button
            {
                Content = "Qrupu Seç",
                Padding = new Thickness(8, 2, 8, 2),
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderThickness = new Thickness(0),
                FontSize = 11,
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var capturedGroup = group.ToList();
            groupSelectAll.Click += (s, e) =>
            {
                bool allChecked = capturedGroup.All(x => x.IsSelected);
                foreach (var opt in capturedGroup) opt.IsSelected = !allChecked;
                UpdateSelectedColCount();
            };
            headerPanel.Children.Add(groupSelectAll);
            stack.Children.Add(headerPanel);

            var wrap = new WrapPanel();
            foreach (var opt in group)
            {
                var chk = new CheckBox
                {
                    Content = opt.HeaderName,
                    IsChecked = opt.IsSelected,
                    Margin = new Thickness(0, 4, 18, 4),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold
                };
                var capturedOpt = opt;
                chk.Checked += (s, e) => { capturedOpt.IsSelected = true; UpdateSelectedColCount(); };
                chk.Unchecked += (s, e) => { capturedOpt.IsSelected = false; UpdateSelectedColCount(); };

                opt.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ExportColumnOption.IsSelected))
                    {
                        chk.IsChecked = opt.IsSelected;
                    }
                };

                wrap.Children.Add(chk);
            }

            stack.Children.Add(wrap);
            card.Child = stack;
            PanelCategories.Children.Add(card);
        }
    }

    private void UpdateSelectedColCount()
    {
        int selected = _columnOptions.Count(o => o.IsSelected);
        int total = _columnOptions.Count;
        TxtSelectedColCount.Text = $"{selected} / {total}";
        TxtError.Text = string.Empty;
    }

    private void BtnSelectAllCols_Click(object sender, RoutedEventArgs e)
    {
        foreach (var opt in _columnOptions) opt.IsSelected = true;
        UpdateSelectedColCount();
    }

    private void BtnDeselectAllCols_Click(object sender, RoutedEventArgs e)
    {
        foreach (var opt in _columnOptions) opt.IsSelected = false;
        UpdateSelectedColCount();
    }

    private void BtnResetDefaultCols_Click(object sender, RoutedEventArgs e)
    {
        foreach (var opt in _columnOptions) opt.IsSelected = opt.IsDefault;
        UpdateSelectedColCount();
    }

    // ═══════════════════════════════════════════════════════════════════
    // NAVIGATION & EXPORT
    // ═══════════════════════════════════════════════════════════════════

    private void BtnNextExport_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 1)
        {
            int selectedCount = _exportItems.Count(x => x.IsSelectedForExport);
            if (selectedCount == 0)
            {
                TxtError.Text = "⚠️ Heç bir əməliyyat seçilməyib! Ən azı 1 əməliyyat seçin.";
                return;
            }

            GoToStep2();
        }
        else if (_currentStep == 2)
        {
            var selectedCols = _columnOptions.Where(o => o.IsSelected).Select(o => o.Id).ToList();
            if (selectedCols.Count == 0)
            {
                TxtError.Text = "⚠️ Heç bir sütun seçilməyib! Ən azı 1 sütun seçin.";
                return;
            }

            ExecuteExport(selectedCols);
        }
    }

    private void GoToStep2()
    {
        _currentStep = 2;

        PanelStep1.Visibility = Visibility.Collapsed;
        PanelStep2.Visibility = Visibility.Visible;

        BtnBack.Visibility = Visibility.Visible;

        Step1Circle.Background = new SolidColorBrush(Color.FromRgb(22, 163, 74));
        Step1Label.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
        StepConnector.Background = new SolidColorBrush(Color.FromRgb(22, 163, 74));

        Step2Circle.Background = new SolidColorBrush(Color.FromRgb(16, 124, 65));
        Step2Number.Foreground = Brushes.White;
        Step2Label.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));
        Step2Label.FontWeight = FontWeights.Bold;

        TxtTitle.Text = "Excel İxracı — Sütun Seçimi";
        int selCount = _exportItems.Count(x => x.IsSelectedForExport);
        TxtSubtitle.Text = $"{_customerName} — {selCount} əməliyyat üçün sütunları seçin";

        BtnNextText.Text = "Excel Faylı Yarat və İxrac Et";
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        _currentStep = 1;

        PanelStep2.Visibility = Visibility.Collapsed;
        PanelStep1.Visibility = Visibility.Visible;

        BtnBack.Visibility = Visibility.Collapsed;

        Step1Circle.Background = new SolidColorBrush(Color.FromRgb(16, 124, 65));
        Step1Label.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));
        StepConnector.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));

        Step2Circle.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));
        Step2Number.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        Step2Label.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        Step2Label.FontWeight = FontWeights.SemiBold;

        TxtTitle.Text = "Balans Tarixçəsi — Əməliyyat Seçimi";
        int total = _exportItems.Count;
        TxtSubtitle.Text = $"{_customerName} — {total} balans əməliyyatı mövcuddur";

        BtnNextText.Text = "Davam Et — Sütun Seçimi";
    }

    private async void ExecuteExport(List<string> selectedColIds)
    {
        var selectedHistories = _exportItems
            .Where(x => x.IsSelectedForExport)
            .Select(x => x.History)
            .ToList();

        var saveDialog = new SaveFileDialog
        {
            Title = "Balans Tarixçəsini Excel (.xlsx) olaraq saxla",
            Filter = "Excel İş Kitabı (*.xlsx)|*.xlsx",
            FileName = $"{_customerName}_Balans_Tarixcesi_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            DefaultExt = ".xlsx"
        };

        if (saveDialog.ShowDialog() != true) return;

        try
        {
            IsEnabled = false;

            await _excelService.ExportBalanceHistoryToExcel(
                _customerId,
                saveDialog.FileName,
                selectedColIds,
                selectedHistories);

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
                catch { }
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            TxtError.Text = $"İxrac zamanı xəta: {ex.Message}";
            IsEnabled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

/// <summary>
/// Wrapper for CustomerBalanceHistoryResponse with a mutable IsSelectedForExport property.
/// </summary>
public class BalanceHistoryExportItem : INotifyPropertyChanged
{
    public CustomerBalanceHistoryResponse History { get; }

    private bool _isSelectedForExport = true;
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

    public BalanceHistoryExportItem(CustomerBalanceHistoryResponse history)
    {
        History = history;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
