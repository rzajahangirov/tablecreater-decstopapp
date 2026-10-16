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
                .Include(t => t.AdditionalExpenses)
                .Where(t => t.CustomerId == customerId)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            list = entities.Select(e => MapEntityToReadResponse(e, customer.Name)).ToList();
        }
        else
        {
            var entities = await _db.Transactions
                .Include(t => t.Customer)
                .Include(t => t.AdditionalExpenses)
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

        // ── Brand Header Block ─────────────────────────────────────────────────────
        int headerRow = 4;
        ws.Row(1).Height = 28;
        ws.Row(2).Height = 20;
        ws.Row(3).Height = 10;

        // Title & Meta Info
        ws.Cell("A1").Value = customer != null ? "MÜŞTƏRİ TRANZAKSİYA VƏ MALİYYƏ HESABATI" : "ÜMUMİ TRANZAKSİYA VƏ MALİYYƏ HESABATI";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 15;
        ws.Cell("A1").Style.Font.FontColor = XLColor.FromArgb(16, 124, 65); // Excel Green

        if (customer != null)
        {
            ws.Cell("A2").Value = $"Müştəri: {customer.Name}   |   Əlaqə: {(string.IsNullOrWhiteSpace(customer.Phone) ? "—" : customer.Phone)}   |   Tarix: {DateTime.Now:yyyy-MM-dd HH:mm}";
        }
        else
        {
            ws.Cell("A2").Value = $"Hesabat Tarixi: {DateTime.Now:yyyy-MM-dd HH:mm}   |   Sistem Üzrə Bütün Tranzaksiyalar";
        }
        ws.Cell("A2").Style.Font.FontSize = 10;
        ws.Cell("A2").Style.Font.FontColor = XLColor.FromArgb(100, 116, 139);

        // ── Table Column Headers ────────────────────────────
        for (int c = 0; c < activeColumns.Count; c++)
        {
            var colDef = activeColumns[c];
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = colDef.Header;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 11;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(16, 124, 65); // Excel Green #107C41
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromArgb(13, 101, 53);
        }
        ws.Row(headerRow).Height = 28;

        // ── Data Rows ───────────────────────────────────────────────
        int startRow = headerRow + 1;
        var sums = new decimal[activeColumns.Count];

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            int rowIdx = startRow + i;
            var xlRow = ws.Row(rowIdx);
            xlRow.Height = 22;

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

                if (colDef.Id == "AdditionalExpensesDetail")
                {
                    cell.Style.Alignment.WrapText = true;
                }

                // Specific styling for key financial / status columns
                if (colDef.Id == "ShipmentStatus")
                {
                    cell.Style.Font.Bold = true;
                    switch (item.ShipmentStatus)
                    {
                        case ShipmentStatus.Delivered:
                            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 252, 231);
                            cell.Style.Font.FontColor = XLColor.FromArgb(21, 128, 61);
                            break;
                        case ShipmentStatus.InTransit:
                            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(243, 232, 255);
                            cell.Style.Font.FontColor = XLColor.FromArgb(126, 34, 206);
                            break;
                        case ShipmentStatus.Loaded:
                            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(219, 234, 254);
                            cell.Style.Font.FontColor = XLColor.FromArgb(29, 78, 216);
                            break;
                        default:
                            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(254, 243, 199);
                            cell.Style.Font.FontColor = XLColor.FromArgb(180, 83, 9);
                            break;
                    }
                }
                else if (colDef.Id == "TotalBilledRub")
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.FromArgb(112, 26, 117);
                }
                else if (colDef.Id == "TotalBilledUsd")
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.FromArgb(29, 78, 216);
                }
                else if (colDef.Id == "TotalExpenseUnifiedUsd")
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.FromArgb(21, 128, 61); // Green
                }
                else if (colDef.Id == "BaseCostUnifiedUsd")
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.FromArgb(180, 83, 9); // Amber
                }
                else if (colDef.Id == "AdditionalExpensesUnifiedUsd")
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.FromArgb(180, 83, 9); // Amber
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
        sumRow.Height = 28;

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
            if (ws.Column(c).Width < 13) ws.Column(c).Width = 13;
            if (ws.Column(c).Width > 50) ws.Column(c).Width = 50;
        }

        // Freeze panes at header row (sticky header on scroll)
        ws.SheetView.FreezeRows(headerRow);

        // Native Excel AutoFilter on headers
        if (list.Count > 0)
        {
            ws.Range(headerRow, 1, summaryRow - 1, activeColumns.Count).SetAutoFilter();
        }

        // Print & Page Setup (Executive Standard A4 Landscape)
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0); // All columns fit on 1 page wide
        ws.PageSetup.Margins.Left = 0.5;
        ws.PageSetup.Margins.Right = 0.5;
        ws.PageSetup.Margins.Top = 0.75;
        ws.PageSetup.Margins.Bottom = 0.75;

        workbook.SaveAs(filePath);
    }

    // =========================================================================
    // 2. BALANCE HISTORY EXPORT (LEDGER)
    // =========================================================================

    public async Task ExportBalanceHistoryToExcel(
        long customerId,
        string filePath,
        IEnumerable<string>? selectedColumnIds = null,
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
                Currency = e.Currency,
                Amount = e.Amount != 0 ? e.Amount : e.AmountUsd,
                BalanceAfter = e.BalanceAfter != 0 ? e.BalanceAfter : e.BalanceAfterUsd,
                AmountUsd = e.AmountUsd,
                BalanceAfterUsd = e.BalanceAfterUsd,
                RelatedHistoryId = e.RelatedHistoryId,
                Description = e.Description
            }).ToList();
        }

        var allDefinitions = GetBalanceHistoryColumnDefinitions();
        var selectedSet = selectedColumnIds != null
            ? new HashSet<string>(selectedColumnIds, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(allDefinitions.Where(d => d.IsDefault).Select(d => d.Id), StringComparer.OrdinalIgnoreCase);

        var activeColumns = allDefinitions.Where(d => selectedSet.Contains(d.Id)).ToList();
        if (activeColumns.Count == 0)
        {
            activeColumns = allDefinitions;
        }

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Balans Tarixçəsi");
        ws.ShowGridLines = true;

        // ── Brand Header Block (Rows 1-5) ───────────────────────────
        ws.Cell("A1").Value = "Müştəri:";
        ws.Cell("B1").Value = customer.Name;
        ws.Cell("B1").Style.Font.Bold = true;
        ws.Cell("B1").Style.Font.FontSize = 14;
        ws.Cell("B1").Style.Font.FontColor = XLColor.FromArgb(16, 124, 65);

        ws.Cell("A2").Value = "Əlaqə nömrəsi:";
        ws.Cell("B2").Value = string.IsNullOrWhiteSpace(customer.Phone) ? "—" : customer.Phone;

        ws.Cell("A3").Value = "Cari Balans (USD):";
        ws.Cell("B3").Value = (double)customer.BalanceUsd;
        ws.Cell("B3").Style.NumberFormat.Format = "$#,##0.00";
        ws.Cell("B3").Style.Font.Bold = true;
        ws.Cell("B3").Style.Font.FontColor = customer.BalanceUsd >= 0
            ? XLColor.FromArgb(46, 125, 50)
            : XLColor.FromArgb(198, 40, 40);

        ws.Cell("A4").Value = "Cari Balans (RUB):";
        ws.Cell("B4").Value = (double)customer.BalanceRub;
        ws.Cell("B4").Style.NumberFormat.Format = "#,##0.00 ₽";
        ws.Cell("B4").Style.Font.Bold = true;
        ws.Cell("B4").Style.Font.FontColor = customer.BalanceRub >= 0
            ? XLColor.FromArgb(46, 125, 50)
            : XLColor.FromArgb(198, 40, 40);

        ws.Cell("A5").Value = "Hesabat tarixi:";
        ws.Cell("B5").Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        ws.Range("A1:A5").Style.Font.Bold = true;
        ws.Range("A1:A5").Style.Font.FontColor = XLColor.FromArgb(71, 85, 105);

        // ── Table Column Headers (Row 7) ────────────────────────────
        int headerRow = 7;
        for (int c = 0; c < activeColumns.Count; c++)
        {
            var colDef = activeColumns[c];
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = colDef.Header;
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

                if (!string.IsNullOrEmpty(colDef.NumberFormat) && val is double or decimal)
                {
                    cell.Style.NumberFormat.Format = colDef.NumberFormat;
                }

                cell.Style.Alignment.Horizontal = colDef.Alignment;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Fill.BackgroundColor = rowBg;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromArgb(226, 232, 240);

                if (colDef.Id == "Amount" && val is double amtVal)
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = amtVal >= 0
                        ? XLColor.FromArgb(46, 125, 50)
                        : XLColor.FromArgb(198, 40, 40);
                }
            }
        }

        // Auto-fit columns
        int endRow = startRow + list.Count;
        if (list.Count > 0)
        {
            ws.Columns().AdjustToContents(headerRow, endRow);
            for (int c = 1; c <= activeColumns.Count; c++)
            {
                if (ws.Column(c).Width < 12) ws.Column(c).Width = 12;
                if (ws.Column(c).Width > 50) ws.Column(c).Width = 50;
            }
        }

        // Freeze panes at header row (sticky header on scroll)
        ws.SheetView.FreezeRows(headerRow);

        // Native Excel AutoFilter on headers
        if (list.Count > 0)
        {
            ws.Range(headerRow, 1, endRow - 1, activeColumns.Count).SetAutoFilter();
        }

        // Print & Page Setup (Executive Standard A4 Portrait)
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.Margins.Left = 0.5;
        ws.PageSetup.Margins.Right = 0.5;

        workbook.SaveAs(filePath);
    }

    public static List<BalanceHistoryColumnDef> GetBalanceHistoryColumnDefinitions()
    {
        return new List<BalanceHistoryColumnDef>
        {
            new() { Id = "Index", Header = "№", Group = "Əsas", IsDefault = true, GetValue = (h, i) => i + 1, Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "CreatedAt", Header = "Tarix", Group = "Əsas", IsDefault = true, GetValue = (h, i) => h.CreatedAt.ToString("yyyy-MM-dd"), Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "Currency", Header = "Kassa", Group = "Əsas", IsDefault = true, GetValue = (h, i) => h.KassaName, Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "Type", Header = "Əməliyyat Növü", Group = "Əsas", IsDefault = true, GetValue = (h, i) => FormatBalanceType(h.Type), Alignment = XLAlignmentHorizontalValues.Left },
            new() { Id = "Amount", Header = "Dəyişiklik Məbləği", Group = "Məbləğlər", IsDefault = true, GetValue = (h, i) => (double)(h.Amount != 0 ? h.Amount : h.AmountUsd), NumberFormat = "#,##0.00", Alignment = XLAlignmentHorizontalValues.Right },
            new() { Id = "BalanceAfter", Header = "Yekun Balans", Group = "Məbləğlər", IsDefault = true, GetValue = (h, i) => (double)(h.BalanceAfter != 0 ? h.BalanceAfter : h.BalanceAfterUsd), NumberFormat = "#,##0.00", Alignment = XLAlignmentHorizontalValues.Right },
            new() { Id = "TransactionId", Header = "Tranzaksiya ID", Group = "Əlaqə", IsDefault = true, GetValue = (h, i) => h.TransactionId.HasValue ? $"#{h.TransactionId.Value}" : "—", Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "Description", Header = "Təsvir / Qeyd", Group = "Əlaqə", IsDefault = true, GetValue = (h, i) => string.IsNullOrWhiteSpace(h.Description) ? "—" : h.Description, Alignment = XLAlignmentHorizontalValues.Left }
        };
    }

    private static string FormatBalanceType(BalanceTransactionType type) => type switch
    {
        BalanceTransactionType.Initial => "İlkin Balans",
        BalanceTransactionType.TransactionCharge => "Tranzaksiya Xərci",
        BalanceTransactionType.TransactionUpdate => "Tranzaksiya Düzəlişi",
        BalanceTransactionType.TransactionRollback => "Tranzaksiya Ləğvi",
        BalanceTransactionType.ManualDeposit => "Mədaxil (Artırma)",
        BalanceTransactionType.ManualWithdrawal => "Məxaric (Çıxarış)",
        BalanceTransactionType.Adjustment => "Düzəliş",
        BalanceTransactionType.Transfer => "Valyuta Köçürməsi",
        _ => type.ToString()
    };

    public class BalanceHistoryColumnDef
    {
        public string Id { get; init; } = string.Empty;
        public string Header { get; init; } = string.Empty;
        public string Group { get; init; } = string.Empty;
        public bool IsDefault { get; init; } = true;
        public Func<CustomerBalanceHistoryResponse, int, object?> GetValue { get; init; } = (_, _) => null;
        public string? NumberFormat { get; init; }
        public XLAlignmentHorizontalValues Alignment { get; init; } = XLAlignmentHorizontalValues.Left;
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
            new() { Id = "PricePerTonRub", Header = "Qiymət/Ton (₽)", GetValue = (t, i) => (double)t.PricePerTonRub, NumberFormat = "#,##0.00 ₽", Alignment = XLAlignmentHorizontalValues.Right },
            new() { Id = "GoodsCostRub", Header = "Məhsul Dəyəri (₽)", GetValue = (t, i) => (double)(t.WeightTon * t.PricePerTonRub), NumberFormat = "#,##0.00 ₽", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.WeightTon * t.PricePerTonRub },
            new() { Id = "TransportType", Header = "Nəqliyyat Növü", GetValue = (t, i) => t.TransportType == TransportType.Truck ? "TIR" : (t.TransportType == TransportType.Wagon ? "Vaqon" : (t.TransportType == TransportType.Ship ? "Gəmi" : t.TransportType.ToString())), Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "VehicleCount", Header = "Vasitə Sayı", GetValue = (t, i) => t.VehicleCount ?? 0, NumberFormat = "#,##0", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.VehicleCount ?? 0 },
            new() { Id = "PricePerVehicle", Header = "Nəqliyyat Qiyməti", GetValue = (t, i) => (double)(t.PricePerVehicle ?? 0), NumberFormat = "#,##0.00", Alignment = XLAlignmentHorizontalValues.Right },
            new() { Id = "TransportCurrency", Header = "Nəql. Valyutası", GetValue = (t, i) => t.TransportCurrency.ToString().ToUpperInvariant(), Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "TransportTotalCost", Header = "Cəmi Nəqliyyat Xərci", GetValue = (t, i) => {
                decimal total = (t.PricePerVehicle ?? 0m) * (t.VehicleCount ?? 0);
                return $"{total:N2} {(t.TransportCurrency == PaymentCurrency.Rub ? "₽" : "$")}";
            }, Alignment = XLAlignmentHorizontalValues.Right },
            new() { Id = "AdditionalExpenses", Header = "Əlavə Xərclər", GetValue = (t, i) => t.AdditionalExpenseFormatted, Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "AdditionalExpensesDetail", Header = "Əlavə Xərclər Təsviri", GetValue = (t, i) => t.AdditionalExpenseDescription, Alignment = XLAlignmentHorizontalValues.Left },
            new() { Id = "AdditionalExpensesUnifiedUsd", Header = "Əlavə Xərclər (USD)", GetValue = (t, i) => (double)t.AdditionalExpensesUnifiedUsd, NumberFormat = "$#,##0.00", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.AdditionalExpensesUnifiedUsd },
            new() { Id = "ProfitPerTon", Header = "Ton Başı Qazanc", GetValue = (t, i) => (double)t.ProfitPerTon, NumberFormat = "#,##0.00", Alignment = XLAlignmentHorizontalValues.Right },
            new() { Id = "ProfitPerTonCurrency", Header = "Qazanc Valyutası", GetValue = (t, i) => t.ProfitPerTonCurrency.ToString().ToUpperInvariant(), Alignment = XLAlignmentHorizontalValues.Center },
            new() { Id = "TotalProfit", Header = "Şirkət Qazancı", GetValue = (t, i) => {
                decimal total = t.WeightTon * t.ProfitPerTon;
                return $"{total:N2} {(t.ProfitPerTonCurrency == PaymentCurrency.Rub ? "₽" : "$")}";
            }, Alignment = XLAlignmentHorizontalValues.Right },
            new() { Id = "TotalBilledRub", Header = "Balansa Təsiri (RUB)", GetValue = (t, i) => (double)t.TotalBilledRub, NumberFormat = "#,##0.00 ₽", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.TotalBilledRub },
            new() { Id = "TotalBilledUsd", Header = "Balansa Təsiri (USD)", GetValue = (t, i) => (double)t.TotalBilledUsd, NumberFormat = "$#,##0.00", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.TotalBilledUsd },
            new() { Id = "HistoricalExchangeRate", Header = "Məzənnə (₽/$)", GetValue = (t, i) => (double)t.HistoricalExchangeRate, NumberFormat = "#,##0.00", Alignment = XLAlignmentHorizontalValues.Right },
            new() { Id = "BaseCostUnifiedUsd", Header = "Maya Dəyəri (USD)", GetValue = (t, i) => (double)t.BaseCostUnifiedUsd, NumberFormat = "$#,##0.00", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.BaseCostUnifiedUsd },
            new() { Id = "TotalExpenseUnifiedUsd", Header = "Ümumi Xərc (USD)", GetValue = (t, i) => (double)t.TotalExpenseUnifiedUsd, NumberFormat = "$#,##0.00", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.TotalExpenseUnifiedUsd },
            new() { Id = "ProfitUnifiedUsd", Header = "Qazanc (USD)", GetValue = (t, i) => (double)t.ProfitUnifiedUsd, NumberFormat = "$#,##0.00", Alignment = XLAlignmentHorizontalValues.Right, IsSummable = true, GetSumValue = t => t.ProfitUnifiedUsd },
            new() { Id = "ShipmentStatus", Header = "Status", GetValue = (t, i) => t.ShipmentStatusDisplay, Alignment = XLAlignmentHorizontalValues.Center },
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
        var expDtos = entity.AdditionalExpenses?.Select(e => new TransactionAdditionalExpenseDto
        {
            Title = e.Title,
            Type = e.ExpenseType,
            AmountPerTon = e.AmountPerTon,
            Currency = e.Currency,
            TotalAmount = e.ExpenseType == AdditionalExpenseType.Fixed 
                ? e.AmountPerTon 
                : (entity.WeightTon * e.AmountPerTon)
        }).ToList() ?? new List<TransactionAdditionalExpenseDto>();

        decimal expRubTotal = expDtos.Where(e => e.Currency == PaymentCurrency.Rub).Sum(e => e.TotalAmount);
        decimal expUsdTotal = expDtos.Where(e => e.Currency == PaymentCurrency.Usd).Sum(e => e.TotalAmount);

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
            DocumentPath = entity.DocumentPath,
            IsCompleted = entity.IsCompleted,
            ProfitPerTon = entity.ProfitPerTon,
            ProfitPerTonCurrency = entity.ProfitPerTonCurrency,
            HistoricalExchangeRate = entity.HistoricalExchangeRate,
            HistoricalTotalExpenseUsd = entity.HistoricalTotalExpenseUsd,
            HistoricalUserProfitUsd = entity.HistoricalUserProfitUsd,
            HistoricalCustomerBilledUsd = entity.HistoricalCustomerBilledUsd,
            TotalBilledRub = entity.TotalBilledRub,
            TotalBilledUsd = entity.TotalBilledUsd,
            ShipmentStatus = entity.ShipmentStatus,
            LoadedDate = entity.LoadedDate,
            InTransitStartDate = entity.InTransitStartDate,
            InTransitEndDate = entity.InTransitEndDate,
            DeliveredDate = entity.DeliveredDate,
            AdditionalExpenses = expDtos,
            AdditionalExpensesRubTotal = expRubTotal,
            AdditionalExpensesUsdTotal = expUsdTotal
        };
    }
}
