using System.Text.Json.Serialization;

namespace discord_payment_bot.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeactivationType
{
    RemoveRole,
    Kick,
    Ban
}