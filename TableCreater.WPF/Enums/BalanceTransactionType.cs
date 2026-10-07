namespace TableCreater.WPF.Enums;

/// <summary>
/// Müştəri balansı üzərində aparılan əməliyyatın növü.
/// </summary>
public enum BalanceTransactionType
{
    /// <summary>
    /// İlkin balans təyinatı (müştəri yaradılanda).
    /// </summary>
    Initial,

    /// <summary>
    /// Tranzaksiya əsasında yaranan ödəniş / borc təsiri.
    /// </summary>
    TransactionCharge,

    /// <summary>
    /// Tranzaksiya redaktə edildikdə balans fərqinin tənzimlənməsi.
    /// </summary>
    TransactionUpdate,

    /// <summary>
    /// Tranzaksiya silindikdə balans təsirinin geri qaytarılması.
    /// </summary>
    TransactionRollback,

    /// <summary>
    /// Müştərinin balansına əl ilə məbləğ əlavə edilməsi (Mədaxil / Avans).
    /// </summary>
    ManualDeposit,

    /// <summary>
    /// Müştərinin balansından əl ilə məbləğ çıxarılması (Məxaric / Qaytarma).
    /// </summary>
    ManualWithdrawal,

    /// <summary>
    /// Balansın birbaşa düzəlişi (Yeni balans təyin etmə).
    /// </summary>
    Adjustment,

    /// <summary>
    /// Valyutalar arası köçürmə (USD <-> RUB).
    /// </summary>
    Transfer
}
