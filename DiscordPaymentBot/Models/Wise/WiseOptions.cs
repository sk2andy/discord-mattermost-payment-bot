namespace discord_payment_bot.Models.Wise;

public class WiseOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.transferwise.com/v3";
    public string WiseCommunicationPin { get; set; } = string.Empty;
    public string DeviceFingerPrint { get; set; } = string.Empty;
}
