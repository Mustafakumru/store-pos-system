using System.Text.Json;
using System.Text.Json.Nodes;

namespace Application.Common.Auditing;

/// <summary>
/// İşlem kaydına yazılan veriden gizli/kişisel bilgiyi ayıklar:
///  • PIN, şifre, token, onay token'ı  → "***" (asla yazılmaz)
///  • Kart / hediye kartı no            → son 4 hane
///  • Telefon                           → son 2 hane, e-posta → ilk harf + alan adı
///  • T.C. kimlik no                    → "***"
/// Uzun diziler ve metinler kısaltılır; sonuç en fazla <see cref="MaxLength"/> karakter.
/// </summary>
public static class AuditSanitizer
{
    public const int MaxLength = 4000;
    private const int MaxArrayItems = 30;
    private const int MaxStringLength = 200;

    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "pin", "oldPin", "newPin", "currentPin", "managerPin",
        "password", "currentPassword", "newPassword", "confirmPassword", "passwordHash", "pinHash",
        "token", "accessToken", "refreshToken", "approvalToken", "returnApprovalToken",
        "secret", "secretKey", "securityStamp", "concurrencyStamp", "tckn", "identityNumber", "nationalId"
    };

    private static readonly HashSet<string> CardKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "cardNumber", "giftCardNumber", "owedGiftCardNumber", "pan"
    };

    private static readonly HashSet<string> PhoneKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "phone", "phoneNumber", "mobile", "customerPhone"
    };

    private static readonly HashSet<string> EmailKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "email", "customerEmail", "normalizedEmail", "userName", "normalizedUserName"
    };

    public static bool IsSecret(string key) => SecretKeys.Contains(key);

    /// <summary>Bir alanın değerini anahtar adına göre maskeler (alan değişikliği kayıtları için).</summary>
    public static string? MaskValue(string key, object? value)
    {
        if (value is null) return null;
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        if (SecretKeys.Contains(key)) return "***";
        if (CardKeys.Contains(key)) return MaskCard(text);
        if (PhoneKeys.Contains(key)) return MaskPhone(text);
        if (EmailKeys.Contains(key) && text.Contains('@')) return MaskEmail(text);
        return Truncate(text, MaxStringLength);
    }

    /// <summary>JSON gövdeyi maskeler; JSON değilse null döner.</summary>
    public static string? SanitizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        JsonNode? node;
        try { node = JsonNode.Parse(json); }
        catch (JsonException) { return null; }
        if (node is null) return null;
        var cleaned = Clean(node, parentKey: null);
        return Truncate(cleaned?.ToJsonString() ?? "null", MaxLength);
    }

    public static string Serialize(object value)
        => Truncate(JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }), MaxLength);

    private static JsonNode? Clean(JsonNode? node, string? parentKey)
    {
        switch (node)
        {
            case JsonObject obj:
                var result = new JsonObject();
                foreach (var (key, child) in obj)
                {
                    if (SecretKeys.Contains(key)) { result[key] = child is null ? null : "***"; continue; }
                    result[key] = Clean(child, key);
                }
                return result;

            case JsonArray arr:
                var list = new JsonArray();
                foreach (var item in arr.Take(MaxArrayItems)) list.Add(Clean(item, parentKey));
                if (arr.Count > MaxArrayItems) list.Add($"... (+{arr.Count - MaxArrayItems} kalem)");
                return list;

            case JsonValue val when val.TryGetValue<string>(out var s):
                return parentKey is null ? Truncate(s, MaxStringLength) : MaskValue(parentKey, s);

            default:
                return node?.DeepClone();
        }
    }

    public static string MaskCard(string v)
    {
        var t = v.Trim();
        return t.Length <= 4 ? "****" : new string('*', Math.Min(t.Length - 4, 8)) + t[^4..];
    }

    public static string MaskPhone(string v)
    {
        var digits = new string(v.Where(char.IsDigit).ToArray());
        return digits.Length <= 2 ? "**" : "*******" + digits[^2..];
    }

    public static string MaskEmail(string v)
    {
        var at = v.IndexOf('@');
        return at <= 0 ? "***" : v[0] + "***" + v[at..];
    }

    public static string Truncate(string v, int max) => v.Length <= max ? v : v[..(max - 1)] + "…";
}
