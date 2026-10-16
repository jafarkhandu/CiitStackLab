namespace CIITStackLab.Infrastructure.Payments;

public sealed class PhonePeOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public int ClientVersion { get; set; }

    public string Environment { get; set; } = "SANDBOX";

    public string CallbackUsername { get; set; } = string.Empty;

    public string CallbackPassword { get; set; } = string.Empty;
}
