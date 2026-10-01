namespace TableCreater.WPF.Enums;

/// <summary>
/// Status of a shipment/transaction in the delivery lifecycle.
/// Pending: Gözləmədə — Sifariş qəbul edilib, hələ yüklənməyib (heç bir tarix yoxdur).
/// Loaded: Yükləndi — Mallar nəqliyyata yükləndi (LoadedDate tələb olunur).
/// InTransit: Yola Çıxdı — Daşınma mərhələsindədir (InTransitStartDate və InTransitEndDate).
/// Delivered: Çatdı — Mallar ünvana çatdı və təhvil verildi (DeliveredDate tələb olunur, IsCompleted=true).
/// </summary>
public enum ShipmentStatus
{
    Pending,
    Loaded,
    InTransit,
    Delivered
}
