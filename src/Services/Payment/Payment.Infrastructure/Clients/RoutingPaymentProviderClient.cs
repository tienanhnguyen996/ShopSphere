using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Infrastructure.Configuration;

namespace ShopSphere.Services.Payment.Infrastructure.Clients;

public class RoutingPaymentProviderClient : IPaymentProviderClient
{
    private readonly StripePaymentProviderClient _stripeClient;
    private readonly MoMoPaymentProviderClient _momoClient;
    private readonly VnPayPaymentProviderClient _vnPayClient;
    private readonly MockPaymentProviderClient _mockClient;
    private readonly PaymentSettings _settings;
    private readonly ILogger<RoutingPaymentProviderClient> _logger;

    public RoutingPaymentProviderClient(
        StripePaymentProviderClient stripeClient,
        MoMoPaymentProviderClient momoClient,
        VnPayPaymentProviderClient vnPayClient,
        MockPaymentProviderClient mockClient,
        IOptions<PaymentSettings> settings,
        ILogger<RoutingPaymentProviderClient> logger)
    {
        _stripeClient = stripeClient ?? throw new ArgumentNullException(nameof(stripeClient));
        _momoClient = momoClient ?? throw new ArgumentNullException(nameof(momoClient));
        _vnPayClient = vnPayClient ?? throw new ArgumentNullException(nameof(vnPayClient));
        _mockClient = mockClient ?? throw new ArgumentNullException(nameof(mockClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var method = request.PaymentMethod.Trim().ToLowerInvariant();

        if (method is "stripe" or "credit_card" or "card")
        {
            if (_settings.Stripe.IsConfigured)
            {
                _logger.LogInformation("Routing charge to Stripe provider for Attempt {AttemptId}", request.PaymentAttemptId);
                return _stripeClient.ChargeAsync(request, cancellationToken);
            }

            if (_settings.FallbackToMockOnMissingCredentials)
            {
                _logger.LogWarning("Stripe credentials not configured. Falling back to MockPaymentProviderClient for Attempt {AttemptId}", request.PaymentAttemptId);
                return _mockClient.ChargeAsync(request, cancellationToken);
            }

            return Task.FromResult(new PaymentChargeResult(
                PaymentProviderStatus.Failed,
                null,
                "Stripe provider is not configured and fallback is disabled."));
        }

        if (method is "momo" or "digital_wallet" or "ewallet")
        {
            if (_settings.MoMo.IsConfigured)
            {
                _logger.LogInformation("Routing charge to MoMo provider for Attempt {AttemptId}", request.PaymentAttemptId);
                return _momoClient.ChargeAsync(request, cancellationToken);
            }

            if (_settings.FallbackToMockOnMissingCredentials)
            {
                _logger.LogWarning("MoMo credentials not configured. Falling back to MockPaymentProviderClient for Attempt {AttemptId}", request.PaymentAttemptId);
                return _mockClient.ChargeAsync(request, cancellationToken);
            }

            return Task.FromResult(new PaymentChargeResult(
                PaymentProviderStatus.Failed,
                null,
                "MoMo provider is not configured and fallback is disabled."));
        }

        if (method is "vnpay" or "bank_transfer" or "banking")
        {
            if (_settings.VnPay.IsConfigured)
            {
                _logger.LogInformation("Routing charge to VNPay provider for Attempt {AttemptId}", request.PaymentAttemptId);
                return _vnPayClient.ChargeAsync(request, cancellationToken);
            }

            if (_settings.FallbackToMockOnMissingCredentials)
            {
                _logger.LogWarning("VNPay credentials not configured. Falling back to MockPaymentProviderClient for Attempt {AttemptId}", request.PaymentAttemptId);
                return _mockClient.ChargeAsync(request, cancellationToken);
            }

            return Task.FromResult(new PaymentChargeResult(
                PaymentProviderStatus.Failed,
                null,
                "VNPay provider is not configured and fallback is disabled."));
        }

        _logger.LogInformation("Routing charge for method '{Method}' to MockPaymentProviderClient", request.PaymentMethod);
        return _mockClient.ChargeAsync(request, cancellationToken);
    }

    public Task<PaymentStatusQueryResult> GetStatusAsync(PaymentStatusQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var txn = request.ProviderTransactionId ?? string.Empty;

        if (txn.StartsWith("pi_", StringComparison.OrdinalIgnoreCase))
        {
            return _settings.Stripe.IsConfigured
                ? _stripeClient.GetStatusAsync(request, cancellationToken)
                : _mockClient.GetStatusAsync(request, cancellationToken);
        }

        if (txn.StartsWith("momo_", StringComparison.OrdinalIgnoreCase))
        {
            return _settings.MoMo.IsConfigured
                ? _momoClient.GetStatusAsync(request, cancellationToken)
                : _mockClient.GetStatusAsync(request, cancellationToken);
        }

        if (txn.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
        {
            return _settings.VnPay.IsConfigured
                ? _vnPayClient.GetStatusAsync(request, cancellationToken)
                : _mockClient.GetStatusAsync(request, cancellationToken);
        }

        return _mockClient.GetStatusAsync(request, cancellationToken);
    }

    public Task<PaymentRefundResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var txn = request.ProviderTransactionId;

        if (txn.StartsWith("pi_", StringComparison.OrdinalIgnoreCase))
        {
            return _settings.Stripe.IsConfigured
                ? _stripeClient.RefundAsync(request, cancellationToken)
                : _mockClient.RefundAsync(request, cancellationToken);
        }

        if (txn.StartsWith("momo_", StringComparison.OrdinalIgnoreCase))
        {
            return _settings.MoMo.IsConfigured
                ? _momoClient.RefundAsync(request, cancellationToken)
                : _mockClient.RefundAsync(request, cancellationToken);
        }

        if (txn.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
        {
            return _settings.VnPay.IsConfigured
                ? _vnPayClient.RefundAsync(request, cancellationToken)
                : _mockClient.RefundAsync(request, cancellationToken);
        }

        return _mockClient.RefundAsync(request, cancellationToken);
    }
}
