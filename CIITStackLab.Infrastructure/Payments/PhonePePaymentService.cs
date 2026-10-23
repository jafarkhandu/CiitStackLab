using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CIITStackLab.Infrastructure.Payments;

// Compatibility type retained for existing controller constructor signatures during migration.
// The implementation is Razorpay-only; no PhonePe SDK or API is used.
public sealed class PhonePePaymentService
{
    private const string OrdersEndpoint = "https://api.razorpay.com/v1/orders";
    private readonly RazorpayOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PhonePePaymentService> _logger;

    public PhonePePaymentService(
        IOptions<RazorpayOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<PhonePePaymentService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string KeyId => RequireOptions().KeyId;

    public async Task<string> CreateCheckoutAsync(
        string merchantOrderId,
        long amountInPaise,
        string redirectUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(merchantOrderId))
            throw new ArgumentException("Merchant order ID is required.", nameof(merchantOrderId));
        if (amountInPaise <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountInPaise));
        if (string.IsNullOrWhiteSpace(redirectUrl))
            throw new ArgumentException("Redirect URL is required.", nameof(redirectUrl));

        var options = RequireOptions();
        var client = CreateClient(options);
        var payload = JsonSerializer.Serialize(new
        {
            amount = amountInPaise,
            currency = "INR",
            receipt = merchantOrderId,
            partial_payment = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, OrdersEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = CreateBasicAuthentication(options);

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Razorpay order creation failed for merchant order {MerchantOrderId}. Status: {StatusCode}.",
                merchantOrderId,
                response.StatusCode);
            throw new InvalidOperationException("Razorpay could not create the payment order.");
        }

        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("id", out var idElement)
            || string.IsNullOrWhiteSpace(idElement.GetString()))
        {
            throw new InvalidOperationException("Razorpay did not return a payment order ID.");
        }

        var razorpayOrderId = idElement.GetString()!;
        var baseUri = new Uri(redirectUrl).GetLeftPart(UriPartial.Authority);
        var checkoutUrl = $"{baseUri}/Payments/RazorpayCheckout"
            + $"?merchantOrderId={Uri.EscapeDataString(merchantOrderId)}"
            + $"&razorpayOrderId={Uri.EscapeDataString(razorpayOrderId)}"
            + $"&keyId={Uri.EscapeDataString(options.KeyId)}";

        _logger.LogInformation(
            "Razorpay order created for merchant order {MerchantOrderId}.",
            merchantOrderId);

        return checkoutUrl;
    }

    public async Task<string> GetOrderStateAsync(string merchantOrderId)
    {
        if (string.IsNullOrWhiteSpace(merchantOrderId))
            throw new ArgumentException("Merchant order ID is required.", nameof(merchantOrderId));

        return "PENDING";
    }

    public async Task<RazorpayOrderDetails> GetOrderAsync(
        string razorpayOrderId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(razorpayOrderId))
            throw new ArgumentException("Razorpay order ID is required.", nameof(razorpayOrderId));

        var options = RequireOptions();
        var client = CreateClient(options);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{OrdersEndpoint}/{Uri.EscapeDataString(razorpayOrderId)}");
        request.Headers.Authorization = CreateBasicAuthentication(options);

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Razorpay could not verify the payment order.");

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        return new RazorpayOrderDetails(
            root.GetProperty("id").GetString() ?? string.Empty,
            root.TryGetProperty("receipt", out var receipt) ? receipt.GetString() : null,
            root.GetProperty("amount").GetInt64(),
            root.TryGetProperty("amount_paid", out var amountPaid) ? amountPaid.GetInt64() : 0,
            root.TryGetProperty("status", out var status) ? status.GetString() ?? string.Empty : string.Empty);
    }

    public bool VerifyPaymentSignature(
        string razorpayOrderId,
        string razorpayPaymentId,
        string razorpaySignature)
    {
        if (string.IsNullOrWhiteSpace(razorpayOrderId)
            || string.IsNullOrWhiteSpace(razorpayPaymentId)
            || string.IsNullOrWhiteSpace(razorpaySignature))
        {
            return false;
        }

        var options = RequireOptions();
        var payload = $"{razorpayOrderId}|{razorpayPaymentId}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.KeySecret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(razorpaySignature));
    }

    private RazorpayOptions RequireOptions()
    {
        var keySecret = string.IsNullOrWhiteSpace(_options.KeySecret)
            ? Environment.GetEnvironmentVariable("Razorpay__KeySecret")
            : _options.KeySecret;

        if (string.IsNullOrWhiteSpace(_options.KeyId) || string.IsNullOrWhiteSpace(keySecret))
        {
            throw new InvalidOperationException(
                "Razorpay credentials are not configured. Configure Razorpay:KeyId and Razorpay:KeySecret or Razorpay__KeySecret.");
        }

        if (!string.Equals(_options.KeySecret, keySecret, StringComparison.Ordinal))
        {
            _options.KeySecret = keySecret;
        }

        return _options;
    }

    private HttpClient CreateClient(RazorpayOptions options)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 120));
        return client;
    }

    private static AuthenticationHeaderValue CreateBasicAuthentication(RazorpayOptions options)
    {
        var token = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.KeyId}:{options.KeySecret}"));
        return new AuthenticationHeaderValue("Basic", token);
    }
}

public sealed record RazorpayOrderDetails(
    string Id,
    string? Receipt,
    long Amount,
    long AmountPaid,
    string Status);
