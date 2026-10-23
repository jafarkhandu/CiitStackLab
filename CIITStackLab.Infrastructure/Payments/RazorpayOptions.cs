namespace CIITStackLab.Infrastructure.Payments;

public sealed class RazorpayOptions
{
    public string KeyId { get; set; } = "rzp_test_TrIz5e007DJ61x";
    public string KeySecret { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}
