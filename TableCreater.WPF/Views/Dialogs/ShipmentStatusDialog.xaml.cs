using System.Windows;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;
using TableCreater.WPF.Services;

namespace TableCreater.WPF.Views.Dialogs;

/// <summary>
/// Modal dialog for updating the shipment status and corresponding tracking dates of a transaction.
/// Supports both database updates and in-memory selection during transaction creation.
/// </summary>
public partial class ShipmentStatusDialog : Window
{
    private readonly long? _transactionId;
    private readonly ITransactionService? _transactionService;
    private readonly TransactionReadResponse? _original;

    public ShipmentStatus ResultStatus { get; private set; } = ShipmentStatus.Pending;
    public DateOnly? ResultLoadedDate { get; private set; }
    public DateOnly? ResultInTransitStartDate { get; private set; }
    public DateOnly? ResultDeliveredDate { get; private set; }

    /// <summary>
    /// Constructor for existing transaction database update.
    /// </summary>
    public ShipmentStatusDialog(TransactionReadResponse transaction, ITransactionService transactionService)
    {
        InitializeComponent();

        _original = transaction;
        _transactionId = transaction.Id;
        _transactionService = transactionService;

        TxtTransactionSummary.Text = $"{transaction.CustomerName} — {transaction.ProductName} ({transaction.TransactionDate:yyyy-MM-dd})";
        TxtSaveButton.Text = "Statusu Yenilə";

        InitializeValues(transaction.ShipmentStatus, transaction.LoadedDate, transaction.InTransitStartDate, transaction.DeliveredDate);
    }

    /// <summary>
    /// Constructor for in-memory status editing (e.g. from TransactionEntryPage).
    /// </summary>
    public ShipmentStatusDialog(ShipmentStatus currentStatus, DateOnly? loadedDate, DateOnly? inTransitStartDate, DateOnly? deliveredDate)
    {
        InitializeComponent();

        TxtTransactionSummary.Text = "Tranzaksiya Göndərmə Statusu və Tarixləri";
        TxtSaveButton.Text = "Təsdiqlə";

        InitializeValues(currentStatus, loadedDate, inTransitStartDate, deliveredDate);
    }

    private void InitializeValues(ShipmentStatus status, DateOnly? loadedDate, DateOnly? inTransitStartDate, DateOnly? deliveredDate)
    {
        // Dates are all optional. If not set, leave empty (null).
        if (loadedDate.HasValue)
        {
            var loadedDt = loadedDate.Value.ToDateTime(TimeOnly.MinValue);
            DpLoadedDate.SelectedDate = loadedDt;
            DpInTransitLoadedDate.SelectedDate = loadedDt;
            DpDeliveredLoadedDate.SelectedDate = loadedDt;
        }
        else
        {
            DpLoadedDate.SelectedDate = null;
            DpInTransitLoadedDate.SelectedDate = null;
            DpDeliveredLoadedDate.SelectedDate = null;
        }

        if (inTransitStartDate.HasValue)
        {
            var transitStart = inTransitStartDate.Value.ToDateTime(TimeOnly.MinValue);
            DpInTransitStart.SelectedDate = transitStart;
            DpDeliveredTransitStart.SelectedDate = transitStart;
        }
        else
        {
            DpInTransitStart.SelectedDate = null;
            DpDeliveredTransitStart.SelectedDate = null;
        }

        if (deliveredDate.HasValue)
        {
            DpDeliveredDate.SelectedDate = deliveredDate.Value.ToDateTime(TimeOnly.MinValue);
        }
        else
        {
            DpDeliveredDate.SelectedDate = null;
        }

        // Set active radio button based on status
        switch (status)
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

    private void StatusRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        UpdatePanelsVisibility();
    }

    private void UpdatePanelsVisibility()
    {
        if (PanelPending == null || PanelLoaded == null || PanelInTransit == null || PanelDelivered == null)
            return;

        PanelPending.Visibility = Visibility.Collapsed;
        PanelLoaded.Visibility = Visibility.Collapsed;
        PanelInTransit.Visibility = Visibility.Collapsed;
        PanelDelivered.Visibility = Visibility.Collapsed;

        if (RbPending.IsChecked == true)
        {
            PanelPending.Visibility = Visibility.Visible;
        }
        else if (RbLoaded.IsChecked == true)
        {
            PanelLoaded.Visibility = Visibility.Visible;
        }
        else if (RbInTransit.IsChecked == true)
        {
            PanelInTransit.Visibility = Visibility.Visible;
        }
        else if (RbDelivered.IsChecked == true)
        {
            PanelDelivered.Visibility = Visibility.Visible;
        }
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
            if (DpLoadedDate.SelectedDate.HasValue)
            {
                loadedDate = DateOnly.FromDateTime(DpLoadedDate.SelectedDate.Value);
            }
        }
        else if (RbInTransit.IsChecked == true)
        {
            status = ShipmentStatus.InTransit;
            if (DpInTransitStart.SelectedDate.HasValue)
            {
                inTransitStartDate = DateOnly.FromDateTime(DpInTransitStart.SelectedDate.Value);
            }
            if (DpInTransitLoadedDate.SelectedDate.HasValue)
            {
                loadedDate = DateOnly.FromDateTime(DpInTransitLoadedDate.SelectedDate.Value);
            }
            else if (_original?.LoadedDate != null)
            {
                loadedDate = _original.LoadedDate;
            }
        }
        else if (RbDelivered.IsChecked == true)
        {
            status = ShipmentStatus.Delivered;
            if (DpDeliveredDate.SelectedDate.HasValue)
            {
                deliveredDate = DateOnly.FromDateTime(DpDeliveredDate.SelectedDate.Value);
            }

            if (DpDeliveredTransitStart.SelectedDate.HasValue)
            {
                inTransitStartDate = DateOnly.FromDateTime(DpDeliveredTransitStart.SelectedDate.Value);
            }
            else if (_original?.InTransitStartDate != null)
            {
                inTransitStartDate = _original.InTransitStartDate;
            }

            if (DpDeliveredLoadedDate.SelectedDate.HasValue)
            {
                loadedDate = DateOnly.FromDateTime(DpDeliveredLoadedDate.SelectedDate.Value);
            }
            else if (_original?.LoadedDate != null)
            {
                loadedDate = _original.LoadedDate;
            }
        }
        else
        {
            ShowError("Zəhmət olmasa bir status seçin.");
            return;
        }

        ResultStatus = status;
        ResultLoadedDate = loadedDate;
        ResultInTransitStartDate = inTransitStartDate;
        ResultDeliveredDate = deliveredDate;

        if (_transactionService != null && _transactionId.HasValue)
        {
            try
            {
                var request = new ShipmentStatusUpdateRequest
                {
                    Status = status,
                    LoadedDate = loadedDate,
                    InTransitStartDate = inTransitStartDate,
                    DeliveredDate = deliveredDate
                };

                await _transactionService.UpdateShipmentStatus(_transactionId.Value, request);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                return;
            }
        }

        DialogResult = true;
        Close();
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
