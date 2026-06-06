using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using TableCreater.WPF.Data;
using TableCreater.WPF.Enums;

namespace TableCreater.WPF.Services;

/// <summary>
/// Excel export service using ClosedXML.
/// Replaces Java Apache POI (XSSF) export.
/// Generates professional, print-ready .xlsx workbooks.
/// </summary>
public class ExcelService : IExcelService
{
    private readonly AppDbContext _db;

    public ExcelService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Exports all transactions for a customer to an Excel file.
    /// Mapped from Java TransactionService.exportCustomerTransactions (T8).
    ///
    /// Layout:
    ///   Row 1-3: Customer header (name, phone, generation date)
    ///   Row 5:   Column headers (bold, dark background)
    ///   Row 6+:  Transaction data with alternating row colors
    ///   Last:    Summary row with totals
    /// </summary>
    public async Task ExportToExcel(long customerId, string filePath)
    {
        var customer = await _db.Customers.FindAsync(customerId)
            ?? throw new InvalidOperationException("Customer not found");

        var transactions = await _db.Transactions
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Transactions");

        // ── Customer Header ─────────────────────────────────────────
        ws.Cell("A1").Value = "Customer:";
        ws.Cell("B1").Value = customer.Name;
        ws.Cell("A2").Value = "Phone:";
        ws.Cell("B2").Value = customer.Phone ?? "N/A";
        ws.Cell("A3").Value = "Generated:";
        ws.Cell("B3").Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        ws.Range("A1:A3").Style.Font.Bold = true;
        ws.Range("B1:B3").Style.Font.FontColor = XLColor.DarkBlue;
        ws.Cell("B1").Style.Font.Bold = true;
        ws.Cell("B1").Style.Font.FontSize = 14;

        // ── Column Headers ──────────────────────────────────────────
        int headerRow = 5;
        string[] headers = {
            "#", "Date", "Product", "Receiving Company",
            "Weight (Ton)", "Price/Ton (RUB)", "Transport",
            "Vehicles", "Price/Vehicle", "Exchange Rate",
            "Total Expense (USD)", "Paid Amount", "Paid Currency",
            "Paid (USD)", "Remaining Debt (USD)", "Completed"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.DarkSlateGray;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // ── Transaction Data ────────────────────────────────────────
        decimal totalExpense = 0;
        decimal totalPaidUsd = 0;
        int dataRow = headerRow + 1;

        for (int i = 0; i < transactions.Count; i++)
        {
            var t = transactions[i];
            int row = dataRow + i;

            // Calculate PaidInUsd for this transaction
            decimal paidAmount = t.PaidAmount ?? 0m;
            decimal paidInUsd = t.PaidCurrency == PaymentCurrency.Rub
                ? paidAmount * t.HistoricalExchangeRate
                : paidAmount;

            ws.Cell(row, 1).Value = i + 1;
            ws.Cell(row, 2).Value = t.TransactionDate.ToString("yyyy-MM-dd");
            ws.Cell(row, 3).Value = t.ProductName ?? "";
            ws.Cell(row, 4).Value = t.ReceivingCompany ?? "";
            ws.Cell(row, 5).Value = (double)t.WeightTon;
            ws.Cell(row, 6).Value = (double)t.PricePerTonRub;
            ws.Cell(row, 7).Value = t.TransportType.ToString();
            ws.Cell(row, 8).Value = t.VehicleCount ?? 0;
            ws.Cell(row, 9).Value = (double)(t.PricePerVehicle ?? 0);
            ws.Cell(row, 10).Value = (double)t.HistoricalExchangeRate;
            ws.Cell(row, 11).Value = (double)t.HistoricalTotalExpenseUsd;
            ws.Cell(row, 12).Value = (double)paidAmount;
            ws.Cell(row, 13).Value = t.PaidCurrency.ToString();
            ws.Cell(row, 14).Value = (double)paidInUsd;
            ws.Cell(row, 15).Value = (double)t.HistoricalRemainingDebtUsd;
            ws.Cell(row, 16).Value = t.IsCompleted ? "Yes" : "No";

            // Currency formatting
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 10).Style.NumberFormat.Format = "0.000000";
            ws.Cell(row, 11).Style.NumberFormat.Format = "$#,##0.00";
            ws.Cell(row, 12).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 14).Style.NumberFormat.Format = "$#,##0.00";
            ws.Cell(row, 15).Style.NumberFormat.Format = "$#,##0.00";

            // Alternating row colors
            if (i % 2 == 1)
            {
                ws.Range(row, 1, row, headers.Length).Style
                    .Fill.BackgroundColor = XLColor.FromArgb(240, 240, 245);
            }

            // Color-code remaining debt
            if (t.HistoricalRemainingDebtUsd < 0)
                ws.Cell(row, 15).Style.Font.FontColor = XLColor.Red;
            else if (t.HistoricalRemainingDebtUsd > 0)
                ws.Cell(row, 15).Style.Font.FontColor = XLColor.DarkGreen;

            // Borders
            ws.Range(row, 1, row, headers.Length).Style
                .Border.OutsideBorder = XLBorderStyleValues.Thin;

            totalExpense += t.HistoricalTotalExpenseUsd;
            totalPaidUsd += paidInUsd;
        }

        // ── Summary Row ─────────────────────────────────────────────
        int summaryRow = dataRow + transactions.Count + 1;
        decimal totalBenefit = totalPaidUsd - totalExpense;

        ws.Cell(summaryRow, 1).Value = "TOTALS";
        ws.Cell(summaryRow, 1).Style.Font.Bold = true;

        ws.Cell(summaryRow, 11).Value = (double)totalExpense;
        ws.Cell(summaryRow, 11).Style.NumberFormat.Format = "$#,##0.00";
        ws.Cell(summaryRow, 11).Style.Font.Bold = true;

        ws.Cell(summaryRow, 14).Value = (double)totalPaidUsd;
        ws.Cell(summaryRow, 14).Style.NumberFormat.Format = "$#,##0.00";
        ws.Cell(summaryRow, 14).Style.Font.Bold = true;

        ws.Cell(summaryRow, 15).Value = (double)totalBenefit;
        ws.Cell(summaryRow, 15).Style.NumberFormat.Format = "$#,##0.00";
        ws.Cell(summaryRow, 15).Style.Font.Bold = true;
        ws.Cell(summaryRow, 15).Style.Font.FontColor =
            totalBenefit >= 0 ? XLColor.DarkGreen : XLColor.Red;

        ws.Range(summaryRow, 1, summaryRow, headers.Length).Style
            .Fill.BackgroundColor = XLColor.FromArgb(220, 220, 230);
        ws.Range(summaryRow, 1, summaryRow, headers.Length).Style
            .Border.OutsideBorder = XLBorderStyleValues.Medium;

        // ── Auto-fit columns ────────────────────────────────────────
        ws.Columns().AdjustToContents();

        // ── Save ────────────────────────────────────────────────────
        workbook.SaveAs(filePath);
    }
}
