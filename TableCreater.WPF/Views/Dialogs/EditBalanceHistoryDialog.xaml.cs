using System.Globalization;
using System.Windows;
using TableCreater.WPF.Models;
using TableCreater.WPF.Services;

namespace TableCreater.WPF.Views.Dialogs;

public partial class EditBalanceHistoryDialog : Window
{
    private readonly CustomerBalanceHistoryResponse _history;
    private readonly ICustomerService _customerService;
    private readonly TimeSpan _originalTime;

    public DateTime UpdatedDate { get; private set; }
    public string? UpdatedDescription { get; private set; }

    public EditBalanceHistoryDialog(CustomerBalanceHistoryResponse history, ICustomerService customerService)
    {
        InitializeComponent();

        _history = history ?? throw new ArgumentNullException(nameof(history));
        _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));

        // Display summary
        TxtSummary.Text = $"{history.Type} • {history.FormattedAmount} • {history.KassaName}";

        // Prepopulate date and time (convert to local time for user display)
        var localTime = history.CreatedAt.Kind == DateTimeKind.Utc ? history.CreatedAt.ToLocalTime() : history.CreatedAt;
        _originalTime = localTime.TimeOfDay;
        DpDate.SelectedDate = localTime.Date;
        TxtTime.Text = localTime.ToString("HH:mm");

        // Prepopulate description
        TxtDescription.Text = history.Description ?? string.Empty;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        BorderError.Visibility = Visibility.Collapsed;
        TxtError.Text = string.Empty;

        if (!DpDate.SelectedDate.HasValue)
        {
            ShowError("Zəhmət olmasa əməliyyat tarixini seçin.");
            return;
        }

        TimeSpan timeOfDay = _originalTime;
        if (!string.IsNullOrWhiteSpace(TxtTime.Text))
        {
            if (TimeSpan.TryParseExact(TxtTime.Text.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out var parsed) ||
                TimeSpan.TryParseExact(TxtTime.Text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out parsed) ||
                TimeSpan.TryParse(TxtTime.Text.Trim(), out parsed))
            {
                timeOfDay = parsed;
            }
            else
            {
                ShowError("Saat formatı düzgün deyil (Nümunə: 14:30).");
                return;
            }
        }

        var operationDateTime = DpDate.SelectedDate.Value.Date + timeOfDay;

        var description = TxtDescription.Text.Trim();

        BtnSave.IsEnabled = false;

        try
        {
            var request = new UpdateBalanceHistoryRequest
            {
                HistoryId = _history.Id,
                CreatedAt = operationDateTime,
                Description = string.IsNullOrWhiteSpace(description) ? null : description
            };

            await _customerService.UpdateBalanceHistory(request);

            UpdatedDate = operationDateTime;
            UpdatedDescription = description;

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Yadda saxlamaq mümkün olmadı: {ex.Message}");
            BtnSave.IsEnabled = true;
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
