namespace Application.Common;

/// <summary>
/// Mağaza iş günü saati. Veritabanında tüm anlar UTC tutulur; "bugün",
/// fiş numarasındaki tarih ve saatlik kırılım gibi İŞ GÜNÜ kavramları
/// mağazanın yerel saatine (Türkiye) göre hesaplanmalıdır. Aksi halde
/// yerel 00:00–03:00 arası yapılan işlemler bir önceki güne yazılır.
/// </summary>
public static class BusinessClock
{
    public static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        // Türkiye 2016'dan beri sabit UTC+3 (yaz saati yok)
        return TimeZoneInfo.CreateCustomTimeZone("TR", TimeSpan.FromHours(3), "Türkiye", "Türkiye");
    }

    /// <summary>UTC anı yerel (iş) saatine çevirir.</summary>
    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);

    /// <summary>Şu anki yerel iş günü (saat 00:00, Kind=Unspecified).</summary>
    public static DateTime Today => ToLocal(DateTime.UtcNow).Date;

    /// <summary>Yerel iş gününün [başı, sonu) aralığını UTC olarak döner.</summary>
    public static (DateTime FromUtc, DateTime ToUtc) DayRangeUtc(DateTime localDate)
    {
        var start = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(start, TimeZone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(start.AddDays(1), TimeZone);
        return (fromUtc, toUtc);
    }
}
