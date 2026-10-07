using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using TableCreater.WPF.Data;
using TableCreater.WPF.Data.Entities;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;

namespace TableCreater.WPF.Services;

/// <summary>
/// Professional Excel export service using ClosedXML.
/// Generates print-ready, beautifully styled .xlsx workbooks with custom column selection,
/// alternating row coloring, currency/date formatting, and summary calculations.
/// </summary>
public class ExcelService : IExcelService
{
    private readonly AppDbContext _db;

    public ExcelService(AppDbContext db)
    {
        _db = db;
    }

    // =========================================================================
    // 1. TRANSACTIONS EXPORT (CUSTOMIZABLE COLUMNS)
    // =========================================================================

    public async Task ExportTransactionsToExcel(
        long customerId,
        string filePath,
        IEnumerable<string> selectedColumnIds,
        IEnumerable<TransactionReadResponse>? specificTransactions = null)
    {
        Customer? customer = customerId > 0 ? await _db.Customers.FindAsync(customerId) : null;

        List<TransactionReadResponse> list;
        if (specificTransactions != null)
        {
            list = specificTransactions.ToList();
        }
        else if (customer != null)
        {
            var entities = await _db.Transactions
                .Where(t => t.CustomerId == customerId)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            list = entities.Select(e => MapEntityToReadResponse(e, customer.Name)).ToList();
        }
        else
        {
            var entities = await _db.Transactions
                .Include(t => t.Customer)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            list = entities.Select(e => MapEntityToReadResponse(e, e.Customer?.Name ?? string.Empty)).ToList();
        }

        var allDefinitions = GetTransactionColumnDefinitions();
        var selectedSet = new HashSet<string>(selectedColumnIds, StringComparer.OrdinalIgnoreCase);

        // Filter definitions by user selection, keeping logical order
        var activeColumns = allDefinitions.Where(d => selectedSet.Contains(d.Id)).ToList();
        if (activeColumns.Count == 0)
        {
            // If user selected nothing, default to all
            activeColumns = allDefinitions;
        }

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Tranzaksiyalar");
        ws.ShowGridLines = true;

        // ── Brand Header Block (Rows 1-4) ───────────────────────────
        if (customer != null)
        {
            ws.Cell("A1").Value = "Müştəri:";
            ws.Cell("B1").Value = customer.Name;
            ws.Cell("B1").Style.Font.Bold = true;
            ws.Cell("B1").Style.Font.FontSize = 14;
            ws.Cell("B1").Style.Font.FontColor = XLColor.FromArgb(16, 124, 65); // Office Green

            ws.Cell("A2").Value = "Əlaqə nömrəsi:";
            ws.Cell("B2").Value = string.IsNullOrWhiteSpace(customer.Phone) ? "—" : customer.Phone;

            ws.Cell("A3").Value = "Cari Balans:";
            ws.Cell("B3").Value = (double)customer.BalanceUsd;
            ws.Cell("B3").Style.NumberFormat.Format = "$#,##0.00";
            ws.Cell("B3").Style.Font.Bold = true;
            ws.Cell("B3").Style.Font.FontColor = customer.BalanceUsd >= 0
                ? XLColor.FromArgb(46, 125, 50)
                : XLColor.FromArgb(198, 40, 40);

            ws.Cell("A4").Value = "Hesabat tarixi:";
            ws.Cell("B4").Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        }
        else
        {
            ws.Cell("A1").Value = "Hesabat:";
            ws.Cell("B1").Value = "Maliyyə və Tranzaksiya Hesabatı";
            ws.Cell("B1").Style.Font.Bold = true;
            ws.Cell("B1").Style.Font.FontSize = 14;
            ws.Cell("B1").Style.Font.FontColor = XLColor.FromArgb(16, 124, 65);

            ws.Cell("A2").Value = "Tranzaksiya sayı:";
            ws.Cell("B2").Value = list.Count;

            ws.Cell("A3").Value = "Cəmi Xərc + Qazanc:";
            ws.Cell("B3").Value = (double)list.Sum(x => x.HistoricalCustomerBilledUsd);
            ws.Cell("B3").Style.NumberFormat.Format = "$#,##0.00";
            ws.Cell("B3").Style.Font.Bold = true;
            ws.Cell("B3").Style.Font.FontColor = XLColor.FromArgb(67, 56, 202);

            ws.Cell("A4").Value = "Hesabat tarixi:";
            ws.Cell("B4").Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        }

        ws.Range("A1:A4").Style.Font.Bold = true;
        ws.Range("A1:A4").Style.Font.FontColor = XLColor.FromArgb(71, 85, 105);

        // ── Table Column Headers (Row 6) ────────────────────────────
        int headerRow = 6;
        for (int c = 0; c < activeColumns.Count; c++)
        {
            var colDef = activeColumns[c];
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = colDef.Header;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 11;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(16, 124, 65); // Office Green
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromArgb(12, 94, 49);
        }
        ws.Row(headerRow).Height = 26;

        // ── Data Rows ───────────────────────────────────────────────
        int startRow = headerRow + 1;
        var sums = new decimal[activeColumns.Count];

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            int rowIdx = startRow + i;
            var xlRow = ws.Row(rowIdx);
            xlRow.Height = 20;

            bool isEven = i % 2 == 1;
            var rowBg = isEven ? XLColor.FromArgb(248, 250, 252) : XLColor.White;

            for (int c = 0; c < activeColumns.Count; c++)
            {
                var colDef = activeColumns[c];
                var cell = ws.Cell(rowIdx, c + 1);
                var val = colDef.GetValue(item, i);

                SetCellValue(cell, val);

                if (!string.IsNullOrEmpty(colDef.NumberFormat))
                {
                    cell.Style.NumberFormat.Format = colDef.NumberFormat;
                }

                cell.Style.Alignment.Horizontal = colDef.Alignment;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Fill.BackgroundColor = rowBg;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromArgb(226, 232, 240);

                // Specific color overrides
                if (colDef.Id == "HistoricalBalanceDeltaUsd")
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = item.HistoricalBalanceDeltaUsd >= 0
                        ? XLColor.FromArgb(46, 125, 50)
                        : XLColor.FromArgb(198, 40, 40);
                }
                else if (colDef.Id == "PaymentStatus")
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = item.PaymentStatus == PaymentStatus.Paid
                        ? XLColor.FromArgb(46, 125, 50)
                        : XLColor.FromArgb(198, 40, 40);
                }

                if (colDef.IsSummable && colDef.GetSumValue != null)
                {
                    sums[c] += colDef.GetSumValue(item);
                }
            }
        }

        // ── Summary / Totals Row ────────────────────────────────────
        int summaryRow = startRow + list.Count;
        var sumRow = ws.Row(summaryRow);
        sumRow.Height = 24;

        for (int c = 0; c < activeColumns.Count; c++)
        {
            var colDef = activeColumns[c];
            var cell = ws.Cell(summaryRow, c + 1);

            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
            cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.TopBorderColor = XLColor.FromArgb(148, 163, 184);
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Double;
            cell.Style.Border.BottomBorderColor = XLColor.FromArgb(15, 23, 42);
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            if (c == 0)
            {
                cell.Value = "CƏM";
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            else if (colDef.IsSummable)
            {
                cell.Value = (double)sums[c];
                if (!string.IsNullOrEmpty(colDef.NumberFormat))
                {
                    cell.Style.NumberFormat.Format = colDef.NumberFormat;
                }
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }
            else
            {
                cell.Value = string.Empty;
            }
        }

        // Auto-fit columns with sensible bounds
        ws.Columns().AdjustToContents(headerRow, summaryRow);
        for (int c = 1; c <= activeColumns.Count; c++)
        {
            if (ws.Column(c).Width < 12) ws.Column(c).Width = 12;
            if (ws.Column(c).Width > 45) ws.Column(c).Width = 45;
        }

        workbook.SaveAs(filePath);
    }

    // =========================================================================
    // 2. BALANCE HISTORY EXPORT (LEDGER)
    // =========================================================================

    public async Task ExportBalanceHistoryToExcel(
        long customerId,
        string filePath,
        IEnumerable<CustomerBalanceHistoryResponse>? specificHistories = null)
    {
        var customer = await _db.Customers.FindAsync(customerId)
            ?? throw new InvalidOperationException("Müştəri tapılmadı.");

        List<CustomerBalanceHistoryResponse> list;
        if (specificHistories != null)
        {
            list = specificHistories.ToList();
        }
        else
        {
            var entities = await _db.CustomerBalanceHistories
                .Where(h => h.CustomerId == customerId)
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();

            list = entities.Select(e => new CustomerBalanceHistoryResponse
            {
                Id = e.Id,
                CustomerId = e.CustomerId,
                TransactionId = e.TransactionId,
                CreatedAt = e.CreatedAt,
                Type = e.Type,
                AmountUsd = e.AmountUsd,
                BalanceAfterUsd = e.BalanceAfterUsd,
                Description = e.Description
            }).ToList();
        }

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Balans Tarixçəsi");
        ws.ShowGridLines = true;

        // ── Brand Header Block ──────────────────────────────────────
        ws.Cell("A1").Value = "Müştəri:";
        ws.Cell("B1").Value = customer.Name;
        ws.Cell("B1").Style.Font.Bold = true;
        ws.Cell("B1").Style.Font.FontSize = 14;
        ws.Cell("B1").Style.Font.FontColor = XLColor.FromArgb(16, 124, 65);

        ws.Cell("A2").Value = "Əlaqə nömrəsi:";
        ws.Cell("B2").Value = string.IsNullOrWhiteSpace(customer.Phone) ? "—" : customer.Phone;

        ws.Cell("A3").Value = "Cari Balans:";
        ws.Cell("B3").Value = (double)customer.BalanceUsd;
        ws.Cell("B3").Style.NumberFormat.Format = "$#,##0.00";
        ws.Cell("B3").Style.Font.Bold = true;
        ws.Cell("B3").Style.Font.FontColor = customer.BalanceUsd >= 0
            ? XLColor.FromArgb(46, 125, 50)
            : XLColor.FromArgb(198, 40, 40);

        ws.Cell("A4").Value = "Hesabat tarixi:";
        ws.Cell("B4").Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        ws.Range("A1:A4").Style.Font.Bold = true;
        ws.Range("A1:A4").Style.Font.FontColor = XLColor.FromArgb(71, 85, 105);

        // ── Table Column Headers (Row 6) ────────────────────────────
        string[] headers =
        {
            "№",
            "Tarix və Saat",
            "Əməliyyat Növü",
            "Məbləğ (USD)",
            "Balansdan Sonra (USD)",
            "Tranzaksiya ID",
            "Təsvir / Qeyd"
        };

        int headerRow = 6;
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 11;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(16, 124, 65);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromArgb(12, 94, 49);
        }
        ws.Row(headerRow).Height = 26;

        // ── Data Rows ───────────────────────────────────────────────
        int startRow = headerRow + 1;
        decimal totalNetDelta = 0;

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            int rowIdx = startRow + i;
            var xlRow = ws.Row(rowIdx);
            xlRow.Height = 20;

            bool isEven = i % 2 == 1;
            var rowBg = isEven ? XLColor.FromArgb(248, 250, 252) : XLColor.White;

            string typeText = item.Type switch
            {
                BalanceTransactionType.Initial => "İlkin Balans",
                BalanceTransactionType.TransactionCharge => "Tranzaksiya Xərci",
                BalanceTransactionType.TransactionUpdate => "Tranzaksiya Düzəlişi",
                BalanceTransactionType.TransactionRollback => "Tranzaksiya Ləğvi",
                BalanceTransactionType.ManualDeposit => "Mədaxil (Artırma)",
                BalanceTransactionType.ManualWithdrawal => "Məxaric (Çıxarış)",
                BalanceTransactionType.Adjustment => "Düzəliş",
                _ => item.Type.ToString()
            };

            ws.Cell(rowIdx, 1).Value = i + 1;
            ws.Cell(rowIdx, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(rowIdx, 2).Value = item.CreatedAt.ToString("yyyy-MM-dd HH:mm");
            ws.Cell(rowIdx, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(rowIdx, 3).Value = typeText;
            ws.Cell(rowIdx, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            var amountCell = ws.Cell(rowIdx, 4);
            amountCell.Value = (double)item.AmountUsd;
            amountCell.Style.NumberFormat.Format = "$#,##0.00";
            amountCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            amountCell.Style.Font.Bold = true;
            amountCell.Style.Font.FontColor = item.AmountUsd >= 0
                ? XLColor.FromArgb(46, 125, 50)
                : XLColor.FromArgb(198, 40, 40);

            var afterCell = ws.Cell(rowIdx, 5);
            afterCell.Value = (double)item.BalanceAfterUsd;
            afterCell.Style.NumberFormat.Format = "$#,##0.00";
            afterCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            afterCell.Style.Font.Bold = true;

            ws.Cell(rowIdx, 6).Value = item.TransactionId.HasValue ? $"#{item.TransactionId.Value}" : "—";
            ws.Cell(rowIdx, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(rowIdx, 7).Value = string.IsNullOrWhiteSpace(item.Description) ? "—" : item.Description;
            ws.Cell(rowIdx, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            for (int c = 1; c <= headers.Length; c++)
            {
                var cell = ws.Cell(rowIdx, c);
                cell.Style.Fill.BackgroundColor = rowBg;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromArgb(226, 232, 240);
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            totalNetDelta += item.AmountUsd;
        }

        // ── Summary Row ─────────────────────────────────────────────
        int summaryRow = startRow + list.Count;
        var sumRow = ws.Row(summaryRow);
        sumRow.Height = 24;

        for (int c = 1; c <= headers.Length; c++)
        {
            var cell = ws.Cell(summaryRow, c);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
            cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.TopBorderColor = XLColor.FromArgb(148, 163, 184);
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Double;
            cell.Style.Border.BottomBorderColor = XLColor.FromArgb(15, 23, 42);
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            if (c == 1)
            {
                cell.Value = "CƏM DƏYİŞİM";
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            else if (c == 4)
            {
                cell.Value = (double)totalNetDelta;
                cell.Style.NumberFormat.Format = "$#,##0.00";
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                cell.Style.Font.FontColor = totalNetDelta >= 0
                    ? XLColor.FromArgb(46, 125, 50)
                    : XLColor.FromArgb(198, 40, 40);
            }
            else
            {
                cell.Value = string.Empty;
            }
        }

        // Auto-fit columns
        ws.Columns().AdjustToContents(headerRow, summaryRow);
        for (int c = 1; c <= headers.Length; c++)
        {
            if (ws.Column(c).Width < 12) ws.Column(c).Width = 12;
            if (ws.Column(c).Width > 50) ws.Column(c).Width = 50;
        }

        workbook.SaveAs(filePath);
    }

    // =========================================================================
    // 3. BACKWARD-COMPATIBLE METHOD
    // =========================================================================

    public Task ExportToExcel(long customerId, string filePath)
    {
        // Default export uses all available columns
        var allIds = GetTransactionColumnDefinitions().Select(d => d.Id);
        return ExportTransactionsToExcel(customerId, filePath, allIds);
    }

    // =========================================================================
    // HELPER: COLUMN DEFINITIONS & MAPPING
    // =========================================================================

    private class ColumnDef
    {
        public string Id { get; init; } = string.Empty;
        public string Header { get; init; } = string.Empty;
        public Func<TransactionReadResponse, int, object?> GetValue { get; init; } = null!;
        public string? NumberFormat { get; init; }
        public XLAlignmentHorizontalValues Alignment { get; init; } = XLAlignmentHorizontalValues.Left;
        public bool IsSummable { get; init; }
        public Func<TransactionReadResponse, decimal>? GetSumValue { get; init; }
    }

    private static List<ColumnDef> GetTransactionColumnDefinitions()
    {
        return new List<ColumnDef>
        {
            new() { Id = "Id", Header = "№", GetValue = (t, i) => i + 1, Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "TransactionDate", Header = "Tarix", GetValue = (t, i) => t.TransactionDate.ToString("yyyy-MM-dd"), Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "ProductName", Header = "Məhsul", GetValue = (t, i) => t.ProductName, Alignment = XLAlignmentHorizontalValues.Left },
            new() { Id = "SendingCompany", Header = "Göndərən Firma", GetValue = (t, i) => t.SendingCompany ?? "—", Alignment = XLAlignmentHorizontalValues.Left },
            new() { Id = "ReceivingCompany", Header = "Qəbul Edən Firma", GetValue = (t, i) => t.ReceivingCompany ?? "—", Alignment = XLAlignmentHorizontalValues.Left },
            new() { Id = "WeightTon", Header = "Çəki (Ton)", GetValue = (t, i) => (double)t.WeightTon, NumberFormat = "#,##0.00", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.WeightTon },
            new() { Id = "PricePerTonRub", Header = "Qiymət/Ton (₽)", GetValue = (t, i) => (double)t.PricePerTonRub, NumberFormat = "#,##0.00", Alignment = XLAlignmentHorizontalValues.Right },
            new() { Id = "TransportType", Header = "Nəqliyyat Növü", GetValue = (t, i) => t.TransportType == TransportType.Truck ? "TIR" : (t.TransportType == TransportType.Wagon ? "Vaqon" : t.TransportType.ToString()), Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "VehicleCount", Header = "Vasitə Sayı", GetValue = (t, i) => t.VehicleCount ?? 0, NumberFormat = "#,##0", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.VehicleCount ?? 0 },
            new() { Id = "PricePerVehicle", Header = "Nəqliyyat Qiyməti", GetValue = (t, i) => (double)(t.PricePerVehicle ?? 0), NumberFormat = "#,##0.00", Alignment = XLAlignmentHorizontalValues.Right },
            new() { Id = "TransportCurrency", Header = "Nəql. Valyutası", GetValue = (t, i) => t.TransportCurrency.ToString().ToUpperInvariant(), Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "AdditionalExpenseAmount", Header = "Əlavə Xərc", GetValue = (t, i) => (double)(t.AdditionalExpenseAmount ?? 0), NumberFormat = "#,##0.00", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.AdditionalExpenseAmount ?? 0 },
            new() { Id = "AdditionalExpenseCurrency", Header = "Əlavə Xərc Valyutası", GetValue = (t, i) => t.AdditionalExpenseCurrency?.ToString().ToUpperInvariant() ?? "—", Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "AdditionalExpenseDescription", Header = "Əlavə Xərc Təsviri", GetValue = (t, i) => t.AdditionalExpenseDescription ?? "—", Alignment = XLAlignmentHorizontalValues.Left },
            new() { Id = "HistoricalExchangeRate", Header = "Məzənnə", GetValue = (t, i) => (double)t.HistoricalExchangeRate, NumberFormat = "0.000000", Alignment = XLAlignmentHorizontalValues.Right },
            new() { Id = "HistoricalTotalExpenseUsd", Header = "Ümumi Xərc (USD)", GetValue = (t, i) => (double)t.HistoricalTotalExpenseUsd, NumberFormat = "$#,##0.00", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.HistoricalTotalExpenseUsd },
            new() { Id = "ProfitPerTon", Header = "Ton Başına Qazanc", GetValue = (t, i) => (double)t.ProfitPerTon, NumberFormat = "#,##0.00", Alignment = XLAlignmentHorizontalValues.Right },
            new() { Id = "ProfitPerTonCurrency", Header = "Qazanc Valyutası", GetValue = (t, i) => t.ProfitPerTonCurrency.ToString().ToUpperInvariant(), Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "HistoricalUserProfitUsd", Header = "Şirkət Qazancı (USD)", GetValue = (t, i) => (double)t.HistoricalUserProfitUsd, NumberFormat = "$#,##0.00", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.HistoricalUserProfitUsd },
            new() { Id = "HistoricalCustomerBilledUsd", Header = "Xərc + Qazanc (USD)", GetValue = (t, i) => (double)t.HistoricalCustomerBilledUsd, NumberFormat = "$#,##0.00", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.HistoricalCustomerBilledUsd },
            new() { Id = "PaidAmount", Header = "Ödənilən Məbləğ", GetValue = (t, i) => (double)(t.PaidAmount ?? 0), NumberFormat = "#,##0.00", Alignment = XLAlignmentHorizontalValues.Right },
            new() { Id = "PaidCurrency", Header = "Ödəniş Valyutası", GetValue = (t, i) => t.PaidCurrency.ToString().ToUpperInvariant(), Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "PaidInUsd", Header = "Ödəniş (USD)", GetValue = (t, i) => (double)t.PaidInUsd, NumberFormat = "$#,##0.00", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.PaidInUsd },
            new() { Id = "PaymentStatus", Header = "Ödəniş Statusu", GetValue = (t, i) => t.PaymentStatus == PaymentStatus.Paid ? "Ödənilib" : "Ödənilməyib", Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "HistoricalBalanceDeltaUsd", Header = "Balans Təsiri (USD)", GetValue = (t, i) => (double)t.HistoricalBalanceDeltaUsd, NumberFormat = "$#,##0.00", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.HistoricalBalanceDeltaUsd },
            new() { Id = "ShipmentStatus", Header = "Göndərmə Statusu", GetValue = (t, i) => t.ShipmentStatusDisplay, Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "LoadedDate", Header = "Yüklənmə Tarixi", GetValue = (t, i) => t.LoadedDate.HasValue ? t.LoadedDate.Value.ToString("yyyy-MM-dd") : "—", Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "InTransitStartDate", Header = "Yola Çıxma Tarixi", GetValue = (t, i) => t.InTransitStartDate.HasValue ? t.InTransitStartDate.Value.ToString("yyyy-MM-dd") : "—", Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "DeliveredDate", Header = "Çatdırılma Tarixi", GetValue = (t, i) => t.DeliveredDate.HasValue ? t.DeliveredDate.Value.ToString("yyyy-MM-dd") : "—", Alignment = XLAlignmentHorizontalValues.Center }
        };
    }

    private static void SetCellValue(IXLCell cell, object? val)
    {
        if (val is null)
        {
            cell.Value = string.Empty;
        }
        else if (val is double d)
        {
            cell.Value = d;
        }
        else if (val is int n)
        {
            cell.Value = n;
        }
        else if (val is long l)
        {
            cell.Value = l;
        }
        else if (val is decimal dec)
        {
            cell.Value = (double)dec;
        }
        else
        {
            cell.Value = val.ToString() ?? string.Empty;
        }
    }

    private static TransactionReadResponse MapEntityToReadResponse(Transaction entity, string customerName)
    {
        decimal paidAmount = entity.PaidAmount ?? 0m;
        decimal paidInUsd = entity.PaidCurrency == PaymentCurrency.Rub
            ? (entity.HistoricalExchangeRate > 0 ? paidAmount / entity.HistoricalExchangeRate : 0m)
            : paidAmount;

        return new TransactionReadResponse
        {
            Id = entity.Id,
            CustomerId = entity.CustomerId,
            CustomerName = customerName,
            TransactionDate = entity.TransactionDate,
            CreatedAt = DateOnly.FromDateTime(entity.CreatedAt),
            ProductName = entity.ProductName ?? string.Empty,
            ReceivingCompany = entity.ReceivingCompany ?? string.Empty,
            SendingCompany = entity.SendingCompany,
            WeightTon = entity.WeightTon,
            PricePerTonRub = entity.PricePerTonRub,
            TransportType = entity.TransportType,
            TransportCurrency = entity.TransportCurrency,
            VehicleCount = entity.VehicleCount,
            PricePerVehicle = entity.PricePerVehicle,
            PaidAmount = entity.PaidAmount,
            PaidCurrency = entity.PaidCurrency,
            HistoricalExchangeRate = entity.HistoricalExchangeRate,
            HistoricalTotalExpenseUsd = entity.HistoricalTotalExpenseUsd,
            HistoricalRemainingDebtUsd = entity.HistoricalRemainingDebtUsd,
            PaidInUsd = paidInUsd,
            IsCompleted = entity.IsCompleted,
            PaymentStatus = entity.PaymentStatus,
            ProfitPerTon = entity.ProfitPerTon,
            ProfitPerTonCurrency = entity.ProfitPerTonCurrency,
            HistoricalUserProfitUsd = entity.HistoricalUserProfitUsd,
            HistoricalCustomerBilledUsd = entity.HistoricalCustomerBilledUsd,
            HistoricalBalanceDeltaUsd = entity.HistoricalBalanceDeltaUsd,
            ShipmentStatus = entity.ShipmentStatus,
            LoadedDate = entity.LoadedDate,
            InTransitStartDate = entity.InTransitStartDate,
            InTransitEndDate = entity.InTransitEndDate,
            DeliveredDate = entity.DeliveredDate,
            AdditionalExpenseAmount = entity.AdditionalExpenseAmount,
            AdditionalExpenseCurrency = entity.AdditionalExpenseCurrency,
            AdditionalExpenseDescription = entity.AdditionalExpenseDescription
        };
    }
}
