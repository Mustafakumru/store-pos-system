namespace Domain.Entities;

/// <summary>Veritabanı yedeği geçmişi (otomatik günlük ya da elle "Şimdi yedekle").</summary>
public class BackupRecord
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    /// <summary>"Otomatik" / "Elle" / "Açılış (gecikmiş yedek)".</summary>
    public string Trigger { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }

    public string FilePath { get; set; } = string.Empty;
    public long? SizeBytes { get; set; }
    public bool Succeeded { get; set; }

    /// <summary>RESTORE VERIFYONLY ... WITH CHECKSUM geçti mi (yedek okunabilir ve sağlam).</summary>
    public bool Verified { get; set; }
    public string? Error { get; set; }
    public int DeletedOldFiles { get; set; }
}
