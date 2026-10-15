namespace ShopSphere.Services.Payment.Domain;

/// <summary>
/// Status states of a payment attempt.
/// Timeout = PaymentUnknown, not Failure!
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// Initial attempt created, awaiting charge response.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Provider timed out or returned ambiguous state.
    /// Must query provider status before retrying; do not assume failure.
    /// </summary>
    PaymentUnknown = 1,

    /// <summary>
    /// Provider confirmed payment succeeded. Terminal charge state.
    /// </summary>
    Succeeded = 2,

    /// <summary>
    /// Provider confirmed payment declined or failed. Terminal charge state.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Confirmed refund completed for a previously succeeded payment.
    /// </summary>
    Refunded = 4
}
