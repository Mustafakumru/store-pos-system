using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebAPI.Common;

/// <summary>
/// Tüm DateTime değerlerini JSON'da UTC olarak ("...Z") yazar.
///
/// Neden: Veritabanı anları UTC saklıyor ama EF Core okurken Kind=Unspecified
/// veriyor; varsayılan serileştirici bunları saat dilimi eki OLMADAN yazıyordu
/// ("2026-11-03T09:15:00"). Tarayıcı ve WPF bunu yerel saat sandığı için
/// panelde ve fişte saatler 3 saat geri görünüyordu.
///
/// Okuma: ekli ("Z"/"+03:00") gelen değer UTC'ye çevrilir; eksiz gelen değer
/// olduğu gibi bırakılır (sadece tarih gönderen formlar bozulmasın).
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        writer.WriteStringValue(utc);   // ISO 8601, "Z" ekli
    }
}
