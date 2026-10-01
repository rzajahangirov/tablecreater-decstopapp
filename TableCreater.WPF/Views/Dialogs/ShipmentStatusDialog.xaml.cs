using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;
using TableCreater.WPF.Services;

namespace TableCreater.WPF.Views.Dialogs;

/// <summary>
/// Modal dialog for updating the shipment status and corresponding tracking dates of a transaction.
/// Supports transitions: Pending ↔ Loaded ↔ InTransit ↔ Delivered.
/// </summary>
public partial class ShipmentStatusDialog : Window
{
    private readonly long _transactionId;
    private readonly ITransactionService _transactionService;
    private readonly TransactionReadResponse _original;

    private static readonly SolidColorBrush ActiveBorderBrush = new(Color.FromRgb(0, 95, 184));
    private static readonly SolidColorBrush InactiveBorderBrush = new(Color.FromRgb(221, 221, 221));
    private static readonly SolidColorBrush ActiveBgBrush = new(Color.FromRgb(240, 247, 255));
    private static readonly SolidColorBrush InactiveBgBrush = Brushes.White;

    public ShipmentStatusDialog(TransactionReadResponse transaction, ITransactionService transactionService)
    {
        InitializeComponent();

        _original = transaction;
        _transactionId = transaction.Id;
        _transactionService = transactionService;

        TxtTransactionSummary.Text = $"{transaction.CustomerName} — {transaction.ProductName} ({transaction.TransactionDate:yyyy-MM-dd})";

        InitializeValues();
    }

    private void InitializeValues()
    {
        // Pre-fill existing dates
        if (_original.LoadedDate.HasValue)
        {
            var loadedDt = _original.LoadedDate.Value.ToDateTime(TimeOnly.MinValue);
            DpLoadedDate.SelectedDate = loadedDt;
            DpInTransitLoadedDate.SelectedDate = loadedDt;
            DpDeliveredLoadedDate.SelectedDate = loadedDt;
        }
        else
        {
            DpLoadedDate.SelectedDate = DateTime.Today;
        }

        if (_original.InTransitStartDate.HasValue)
        {
            var transitStart = _original.InTransitStartDate.Value.ToDateTime(TimeOnly.MinValue);
            DpInTransitStart.SelectedDate = transitStart;
            DpDeliveredTransitStart.SelectedDate = transitStart;
        }
        else
        {
            var defaultStart = _original.LoadedDate?.ToDateTime(TimeOnly.MinValue) ?? _original.TransactionDate.ToDateTime(TimeOnly.MinValue);
            DpInTransitStart.SelectedDate = defaultStart;
            DpDeliveredTransitStart.SelectedDate = defaultStart;
        }

        if (_original.DeliveredDate.HasValue)
        {
            DpDeliveredDate.SelectedDate = _original.DeliveredDate.Value.ToDateTime(TimeOnly.MinValue);
        }
        else
        {
            DpDeliveredDate.SelectedDate = DateTime.Today;
        }

        // Set active radio button based on current status
        switch (_original.ShipmentStatus)
        {
            case ShipmentStatus.Pending:
                RbPending.IsChecked = true;
                break;
            case ShipmentStatus.Loaded:
                RbLoaded.IsChecked = true;
                break;
            case ShipmentStatus.InTransit:
                RbInTransit.IsChecked = true;
                break;
            case ShipmentStatus.Delivered:
                RbDelivered.IsChecked = true;
                break;
            default:
                RbPending.IsChecked = true;
                break;
        }

        UpdatePanelsVisibility();
    }

    // Card click events
    private void CardPending_Click(object sender, MouseButtonEventArgs e) => RbPending.IsChecked = true;
    private void CardLoaded_Click(object sender, MouseButtonEventArgs e) => RbLoaded.IsChecked = true;
    private void CardInTransit_Click(object sender, MouseButtonEventArgs e) => RbInTransit.IsChecked = true;
    private void CardDelivered_Click(object sender, MouseButtonEventArgs e) => RbDelivered.IsChecked = true;

    private void StatusRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        UpdatePanelsVisibility();
    }

    private void UpdatePanelsVisibility()
    {
        if (PanelPending == null || PanelLoaded == null || PanelInTransit == null || PanelDelivered == null)
            return;

        // Reset card borders
        ResetCardStyles();

        PanelPending.Visibility = Visibility.Collapsed;
        PanelLoaded.Visibility = Visibility.Collapsed;
        PanelInTransit.Visibility = Visibility.Collapsed;
        PanelDelivered.Visibility = Visibility.Collapsed;

        if (RbPending.IsChecked == true)
        {
            PanelPending.Visibility = Visibility.Visible;
            HighlightCard(CardPending);
        }
        else if (RbLoaded.IsChecked == true)
        {
            PanelLoaded.Visibility = Visibility.Visible;
            HighlightCard(CardLoaded);
        }
        else if (RbInTransit.IsChecked == true)
        {
            PanelInTransit.Visibility = Visibility.Visible;
            HighlightCard(CardInTransit);
        }
        else if (RbDelivered.IsChecked == true)
        {
            PanelDelivered.Visibility = Visibility.Visible;
            HighlightCard(CardDelivered);
        }
    }

    private void HighlightCard(System.Windows.Controls.Border card)
    {
        card.BorderBrush = ActiveBorderBrush;
        card.Background = ActiveBgBrush;
    }

    private void ResetCardStyles()
    {
        CardPending.BorderBrush = InactiveBorderBrush;
        CardPending.Background = InactiveBgBrush;
        CardLoaded.BorderBrush = InactiveBorderBrush;
        CardLoaded.Background = InactiveBgBrush;
        CardInTransit.BorderBrush = InactiveBorderBrush;
        CardInTransit.Background = InactiveBgBrush;
        CardDelivered.BorderBrush = InactiveBorderBrush;
        CardDelivered.Background = InactiveBgBrush;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        BorderError.Visibility = Visibility.Collapsed;
        TxtError.Text = string.Empty;

        ShipmentStatus status;
        DateOnly? loadedDate = null;
        DateOnly? inTransitStartDate = null;
        DateOnly? deliveredDate = null;

        if (RbPending.IsChecked == true)
        {
            status = ShipmentStatus.Pending;
        }
        else if (RbLoaded.IsChecked == true)
        {
            status = ShipmentStatus.Loaded;
            if (!DpLoadedDate.SelectedDate.HasValue)
            {
                ShowError("Yüklənmə tarixi mütləq qeyd edilməlidir.");
                return;
            }
            loadedDate = DateOnly.FromDateTime(DpLoadedDate.SelectedDate.Value);
        }
        else if (RbInTransit.IsChecked == true)
        {
            status = ShipmentStatus.InTransit;
            if (!DpInTransitStart.SelectedDate.HasValue)
            {
                ShowError("Yola çıxma tarixi mütləq seçilməlidir.");
                return;
            }
            inTransitStartDate = DateOnly.FromDateTime(DpInTransitStart.SelectedDate.Value);

            if (DpInTransitLoadedDate.SelectedDate.HasValue)
            {
                loadedDate = DateOnly.FromDateTime(DpInTransitLoadedDate.SelectedDate.Value);
            }
        }
        else if (RbDelivered.IsChecked == true)
        {
            status = ShipmentStatus.Delivered;
            if (!DpDeliveredDate.SelectedDate.HasValue)
            {
                ShowError("Çatdırılma tarixi mütləq qeyd edilməlidir.");
                return;
            }
            deliveredDate = DateOnly.FromDateTime(DpDeliveredDate.SelectedDate.Value);

            if (DpDeliveredTransitStart.SelectedDate.HasValue)
            {
                inTransitStartDate = DateOnly.FromDateTime(DpDeliveredTransitStart.SelectedDate.Value);
            }
            else
            {
                inTransitStartDate = _original.InTransitStartDate ?? _original.LoadedDate ?? _original.TransactionDate;
            }

            if (DpDeliveredLoadedDate.SelectedDate.HasValue)
            {
                loadedDate = DateOnly.FromDateTime(DpDeliveredLoadedDate.SelectedDate.Value);
            }
            else
            {
                loadedDate = _original.LoadedDate;
            }
        }
        else
        {
            ShowError("Zəhmət olmasa bir status seçin.");
            return;
        }

        try
        {
            var request = new ShipmentStatusUpdateRequest
            {
                Status = status,
                LoadedDate = loadedDate,
                InTransitStartDate = inTransitStartDate,
                DeliveredDate = deliveredDate
            };

            await _transactionService.UpdateShipmentStatus(_transactionId, request);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ShowError(string message)
    {
        TxtError.Text = message;
        BorderError.Visibility = Visibility.Visible;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
