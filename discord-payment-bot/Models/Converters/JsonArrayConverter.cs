using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace discord_payment_bot.Models.Converters;

public class JsonArrayConverter<T> : ValueConverter<T[], string>
{
    public JsonArrayConverter(ConverterMappingHints? hints = null)
        : base(
            arr => JsonSerializer.Serialize(arr, (JsonSerializerOptions?)null),
            str => JsonSerializer.Deserialize<T[]>(str, (JsonSerializerOptions?)null)!,
            hints)
    { }
}