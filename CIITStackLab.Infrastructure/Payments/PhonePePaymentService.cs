using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using pg_sdk_dotnet;
using pg_sdk_dotnet.Common.Models.Response;
using pg_sdk_dotnet.Payments.v2;
using pg_sdk_dotnet.Payments.v2.Models.Request;

namespace CIITStackLab.Infrastructure.Payments;

public sealed class PhonePePaymentService
{
    private readonly PhonePeOptions _options;
    private readonly ILogger<PhonePePaymentService> _logger;
    private readonly Lazy<StandardCheckoutClient> _client;

    public PhonePePaymentService(
        IOptions<PhonePeOptions> options,
        ILogger<PhonePePaymentService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new Lazy<StandardCheckoutClient>(CreateClient);
    }

    public async Task<string> CreateCheckoutAsync(
        string merchantOrderId,
        long amountInPaise,
        string redirectUrl)
    {
        if (string.IsNullOrWhiteSpace(merchantOrderId))
        {
            throw new ArgumentException("Merchant order ID is required.", nameof(merchantOrderId));
        }

        if (amountInPaise <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountInPaise));
        }

        if (string.IsNullOrWhiteSpace(redirectUrl))
        {
            throw new ArgumentException("Redirect URL is required.", nameof(redirectUrl));
        }

        var request = StandardCheckoutPayRequest.Builder()
            .SetMerchantOrderId(merchantOrderId)
            .SetAmount(amountInPaise)
            .SetRedirectUrl(redirectUrl)
            .Build();

        try
        {
            var response = await _client.Value
                .Pay(request)
                .WaitAsync(GetTimeout());

            if (string.IsNullOrWhiteSpace(response.RedirectUrl))
            {
                throw new InvalidOperationException(
                    "PhonePe could not create the checkout order because no redirect URL was returned.");
            }

            _logger.LogInformation(
                "PhonePe checkout created for merchant order {MerchantOrderId}.",
                merchantOrderId);

            return response.RedirectUrl;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "PhonePe checkout creation timed out for merchant order {MerchantOrderId}.",
                merchantOrderId);
            throw;
        }
    }

    public async Task<string> GetOrderStateAsync(string merchantOrderId)
    {
        if (string.IsNullOrWhiteSpace(merchantOrderId))
        {
            throw new ArgumentException("Merchant order ID is required.", nameof(merchantOrderId));
        }

        try
        {
            var response = await _client.Value
                .GetOrderStatus(merchantOrderId)
                .WaitAsync(GetTimeout());

            if (response is null)
            {
                _logger.LogWarning(
                    "PhonePe returned no order status response for merchant order {MerchantOrderId}. The payment will remain pending until it can be verified.",
                    merchantOrderId);
                return "PENDING";
            }

            if (string.IsNullOrWhiteSpace(response.State))
            {
                _logger.LogWarning(
                    "PhonePe returned an empty order state for merchant order {MerchantOrderId}. The payment will remain pending until it can be verified.",
                    merchantOrderId);
                return "PENDING";
            }

            return response.State;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "PhonePe order status check timed out for merchant order {MerchantOrderId}.",
                merchantOrderId);
            throw;
        }
    }

    public CallbackResponse ValidateCallback(
        string authorizationHeader,
        string requestBody)
    {
        if (string.IsNullOrWhiteSpace(_options.CallbackUsername)
            || string.IsNullOrWhiteSpace(_options.CallbackPassword))
        {
            throw new InvalidOperationException(
                "PhonePe callback credentials are not configured.");
        }

        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            throw new InvalidOperationException("PhonePe callback authorization is missing.");
        }

        return _client.Value.ValidateCallback(
            _options.CallbackUsername,
            _options.CallbackPassword,
            authorizationHeader,
            requestBody);
    }

    private TimeSpan GetTimeout()
    {
        return TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 120));
    }

    private StandardCheckoutClient CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ClientSecret)
            || _options.ClientVersion <= 0)
        {
            throw new InvalidOperationException(
                "PhonePe credentials are not configured. Configure PhonePe:ClientId, ClientSecret and ClientVersion.");
        }

        var environment = _options.Environment.Equals(
            "PRODUCTION",
            StringComparison.OrdinalIgnoreCase)
            ? Env.PRODUCTION
            : Env.SANDBOX;

        return StandardCheckoutClient.GetInstance(
            _options.ClientId,
            _options.ClientSecret,
            _options.ClientVersion,
            environment,
            null);
    }
}
