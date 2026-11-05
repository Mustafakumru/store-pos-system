using System.Text.Json;
using System.Text.Json.Serialization;

namespace LCWaikiki.POS.WpfClient.Core.Http;

/// <summary>
/// API'den gelen tarihleri YEREL saate çevirir.
/// Backend tüm anları UTC ("...Z") yazar; eki olmayan eski formatlı değerler
/// de UTC kabul edilir (veritabanı anları her zaman UTC tutar).
/// Yazarken değer UTC'ye çevrilip "Z" ekli gönderilir.
/// </summary>
public sealed class LocalDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return value.Kind switch
        {
            DateTimeKind.Utc => value.ToLocalTime(),
            DateTimeKind.Local => value,
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Kind == DateTimeKind.Unspecified
            ? value
            : value.ToUniversalTime());
}
