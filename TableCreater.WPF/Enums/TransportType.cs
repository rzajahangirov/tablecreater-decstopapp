namespace TableCreater.WPF.Enums;

/// <summary>
/// Type of transport used for shipping goods.
/// TRUCK — TIR daşıması. WAGON — Vaqon/dəmir yolu daşıması.
/// Nəqliyyat valyutası artıq avtomatik deyil, manual seçilir (TransportCurrency).
/// </summary>
public enum TransportType
{
    Truck,
    Wagon
}
