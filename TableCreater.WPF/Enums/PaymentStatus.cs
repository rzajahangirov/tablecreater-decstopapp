namespace TableCreater.WPF.Enums;

/// <summary>
/// Tranzaksiyanın ödəniş statusu.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// Ödənilib — müştəri ödəniş edib (tam və ya hissəvi).
    /// </summary>
    Paid,

    /// <summary>
    /// Ödənilməyib — müştəri ödəniş etməyib, cəmi məbləğ (xərclər + şirkət gəliri) müştərinin balansından mənfi çıxılır.
    /// </summary>
    Unpaid,

    /// <summary>
    /// Kassadan Ödənilsin — müştərinin USD və/və ya RUB kassasından (balansından) birbaşa çıxılır.
    /// Hər iki kassadan eyni anda ödəniş mümkündür.
    /// </summary>
    PaidFromBalance
}
