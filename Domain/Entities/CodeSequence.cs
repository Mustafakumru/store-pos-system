using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Genel amacli atomik sayac (urun kodu ve barkod uretimi).
/// Scope ornekleri: "PC:W6PNZ8" (urun kodu kovasi), "BC868402" (kategori barkodu).
/// Deger MERGE ile atomik artirilir (ReceiptSequence ile ayni yontem).
/// </summary>
public class CodeSequence : BaseEntity
{
    public string Scope { get; set; } = string.Empty;
    public int CurrentValue { get; set; }
}
