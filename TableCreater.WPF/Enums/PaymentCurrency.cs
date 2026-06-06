namespace TableCreater.WPF.Enums;

/// <summary>
/// Currency used for payment.
/// When PaidCurrency == Rub, the paid amount must be converted to USD via historicalExchangeRate.
/// </summary>
public enum PaymentCurrency
{
    Usd,
    Rub
}
