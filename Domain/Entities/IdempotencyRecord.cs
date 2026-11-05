namespace Domain.Entities;

/// <summary>
/// Tekrar gonderim korumasi: istemci para hareketi yapan her istege benzersiz
/// bir Idempotency-Key ekler. Ayni anahtarla ikinci istek (ag kopup yeniden
/// deneme, cift tiklama) islem tekrar edilmeden ILK yaniti alir.
/// BaseEntity'den turemez: soft-delete/denetim alanlarina gerek yok.
/// </summary>
public class IdempotencyRecord
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ResponseContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
