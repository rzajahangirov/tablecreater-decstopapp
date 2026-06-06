namespace TableCreater.WPF.Services;

/// <summary>
/// Excel export service interface.
/// Replaces Apache POI (XSSF) with ClosedXML for .xlsx generation.
/// </summary>
public interface IExcelService
{
    Task ExportToExcel(long customerId, string filePath);
}
