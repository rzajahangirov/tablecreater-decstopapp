using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TableCreater.WPF.Models;
using TableCreater.WPF.ViewModels;

namespace TableCreater.WPF.Views.Pages;

/// <summary>
/// Code-behind for CustomerDetailsPage.
/// Handles per-row Edit/Delete actions and document preview.
/// </summary>
public partial class CustomerDetailsPage : UserControl
{
    public CustomerDetailsPage()
    {
        InitializeComponent();
    }

    private CustomerDetailsViewModel? ViewModel => DataContext as CustomerDetailsViewModel;

    /// <summary>
    /// Navigate back to the Customer List.
    /// </summary>
    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.NavigateToCustomers();
    }

    /// <summary>
    /// Navigate to TransactionEntryPage with current customer pre-selected.
    /// </summary>
    private void BtnNewTransaction_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToNewTransaction(ViewModel.CustomerId);
        }
    }

    /// <summary>
    /// Open the customizable column Excel export dialog for transactions.
    /// </summary>
    private void BtnExportTransactionsExcel_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            var dialog = new Dialogs.ExcelExportColumnsDialog(
                ViewModel.CustomerId,
                ViewModel.CustomerName,
                ViewModel.ExcelService,
                ViewModel.AllTransactions,
                ViewModel.Transactions)
            {
                Owner = Window.GetWindow(this)
            };

            dialog.ShowDialog();
        }
    }

    /// <summary>
    /// Open the shipment status update dialog for the selected transaction.
    /// </summary>
    private void BtnChangeStatus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TransactionReadResponse transaction && ViewModel != null)
        {
            var dialog = new Dialogs.ShipmentStatusDialog(transaction, ViewModel.TransactionService)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                ViewModel.RefreshDataCommand.Execute(null);
            }
        }
    }

    /// <summary>
    /// Navigate to TransactionEntryPage in Edit mode with data prepopulated.
    /// </summary>
    private void BtnEditTransaction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is long transactionId)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToEditTransaction(transactionId, ViewModel?.CustomerId ?? 0);
        }
    }

    /// <summary>
    /// Delete a transaction with confirmation dialog.
    /// </summary>
    private void BtnDeleteTransaction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is long transactionId)
        {
            var result = MessageBox.Show(
                "Bu tranzaksiyanı silməyə əminsiniz?\nBu əməliyyat geri qaytarıla bilməz.",
                "Silməni Təsdiqlə",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ViewModel?.DeleteTransactionCommand.Execute(transactionId);
            }
        }
    }

    /// <summary>
    /// Open attached document file using the system default app.
    /// </summary>
    private void BtnOpenDocument_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string documentPath)
        {
            ViewModel?.OpenDocumentCommand.Execute(documentPath);
        }
    }

    /// <summary>
    /// Navigate to TransactionDetailPage on double-click.
    /// </summary>
    private void TransactionGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is TransactionReadResponse tx)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToTransactionDetail(tx.Id, tx.CustomerId);
        }
    }

    /// <summary>
    /// Toggle IsCompleted status for a transaction.
    /// </summary>
    private async void BtnToggleCompleted_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is long transactionId && ViewModel != null)
        {
            await ViewModel.ToggleTransactionCompletedCommand.ExecuteAsync(transactionId);
        }
    }

    /// <summary>
    /// Smooth mouse wheel scrolling for the customer details page.
    /// </summary>
    private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scv)
        {
            scv.ScrollToVerticalOffset(scv.VerticalOffset - (e.Delta * 0.75));
            e.Handled = true;
        }
    }
}
