using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TableCreater.WPF.ViewModels.Transactions;

namespace TableCreater.WPF.Views.Pages;

/// <summary>
/// Code-behind for TransactionEntryPage.
/// Thin event handlers delegating to ViewModel commands.
/// </summary>
public partial class TransactionEntryPage : UserControl
{
    public TransactionEntryPage()
    {
        InitializeComponent();
    }

    private TransactionEntryViewModel? ViewModel => DataContext as TransactionEntryViewModel;

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        if (ViewModel?.PresetCustomerId != null)
            mainWindow?.NavigateToCustomerDetails(ViewModel.PresetCustomerId.Value);
        else
            mainWindow?.NavigateToCustomers();
    }

    private void BtnUpload_Click(object sender, RoutedEventArgs e)
        => ViewModel?.UploadFileCommand.Execute(null);

    private void BtnClearFile_Click(object sender, RoutedEventArgs e)
        => ViewModel?.ClearFileCommand.Execute(null);

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        // Force any focused input to commit its value to the binding source
        // before executing Save. Prevents "unsaved data on submit" issue.
        var focusedElement = Keyboard.FocusedElement as UIElement;
        focusedElement?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        focusedElement?.Focus();

        ViewModel?.SaveCommand.Execute(null);
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
        => ViewModel?.ResetFormCommand.Execute(null);

    private void BtnOpenStatusDialog_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var dialog = new Dialogs.ShipmentStatusDialog(
            ViewModel.ShipmentStatus,
            ViewModel.LoadedDate.HasValue ? DateOnly.FromDateTime(ViewModel.LoadedDate.Value) : null,
            ViewModel.InTransitStartDate.HasValue ? DateOnly.FromDateTime(ViewModel.InTransitStartDate.Value) : null,
            ViewModel.DeliveredDate.HasValue ? DateOnly.FromDateTime(ViewModel.DeliveredDate.Value) : null)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel.ShipmentStatus = dialog.ResultStatus;
            ViewModel.LoadedDate = dialog.ResultLoadedDate?.ToDateTime(TimeOnly.MinValue);
            ViewModel.InTransitStartDate = dialog.ResultInTransitStartDate?.ToDateTime(TimeOnly.MinValue);
            ViewModel.DeliveredDate = dialog.ResultDeliveredDate?.ToDateTime(TimeOnly.MinValue);
        }
    }
}
