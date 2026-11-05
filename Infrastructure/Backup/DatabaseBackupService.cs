using System.Data;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Backup;

/// <summary>Yapılandırma: "Backup" bölümü.</summary>
public class BackupOptions
{
    /// <summary>Otomatik günlük yedek açık mı (elle yedek her zaman kullanılabilir).</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Yedek klasörü — SQL SERVER'IN gördüğü yol (BACKUP komutunu sunucu çalıştırır).
    /// Boşsa SQL Server'ın varsayılan yedek klasörü kullanılır.
    /// Eski yedeklerin silinmesi için API de bu klasöre erişebilmelidir.
    /// </summary>
    public string? Directory { get; set; }

    /// <summary>Günlük yedek saati (sunucu yerel saati, "HH:mm").</summary>
    public string DailyAt { get; set; } = "02:30";

    /// <summary>Bu kadar günden eski yedekler silinir (en yeni <see cref="MinKeep"/> adet hariç).</summary>
    public int RetentionDays { get; set; } = 30;
    public int MinKeep { get; set; } = 7;

    /// <summary>BACKUP komutu zaman aşımı (dk).</summary>
    public int TimeoutMinutes { get; set; } = 60;

    public static BackupOptions From(IConfiguration config)
        => config.GetSection("Backup").Get<BackupOptions>() ?? new BackupOptions();

    public TimeSpan DailyTime => TimeSpan.TryParse(DailyAt, out var t) ? t : new TimeSpan(2, 30, 0);

    /// <summary>Verilen andan sonraki ilk planlı yedek zamanı (yerel saat).</summary>
    public DateTime NextRunAfter(DateTime localNow)
    {
        var today = localNow.Date + DailyTime;
        return today > localNow ? today : today.AddDays(1);
    }
}

public record BackupStatus(
    bool Enabled, string? Directory, string DailyAt, int RetentionDays,
    DateTime? LastSuccessAt, DateTime? NextRunAt, bool IsOverdue,
    List<BackupRecord> History);

/// <summary>
/// SQL Server tam yedeği: BACKUP DATABASE ... WITH CHECKSUM, ardından
/// RESTORE VERIFYONLY (yedek okunabilir mi), sonra saklama süresi temizliği.
/// Aynı anda tek yedek (sp_getapplock) — birden çok API örneğinde de güvenli.
/// Her deneme BackupRecords tablosuna yazılır.
/// </summary>
public class DatabaseBackupService
{
    private const string LockName = "LCW_POS_DATABASE_BACKUP";
    private readonly IServiceScopeFactory _scopes;
    private readonly BackupOptions _options;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(IServiceScopeFactory scopes, IConfiguration config, ILogger<DatabaseBackupService> logger)
    {
        _scopes = scopes;
        _options = BackupOptions.From(config);
        _logger = logger;
    }

    public BackupOptions Options => _options;

    public async Task<BackupRecord> RunAsync(string trigger, string? requestedBy, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var record = new BackupRecord { StartedAt = DateTime.UtcNow, Trigger = trigger, RequestedBy = requestedBy };

        var connection = (SqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        try
        {
            // Aynı anda ikinci yedek başlamasın (oturum kilidi bağlantı kapanınca düşer)
            var lockResult = await ScalarAsync<int>(connection,
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource=@n, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; SELECT @r;",
                ct, ("@n", LockName));
            if (lockResult < 0)
                throw new InvalidOperationException("Başka bir yedekleme şu anda sürüyor. Bitince tekrar deneyin.");

            var dbName = connection.Database;
            // Ayar yoksa SQL Server'ın varsayılan yedek klasörü; o da yoksa (LocalDB/Express)
            // veri klasörü. Canlıda Backup:Directory ile AYRI bir diske/klasöre yazılmalı.
            var directory = string.IsNullOrWhiteSpace(_options.Directory)
                ? await ScalarAsync<string>(connection,
                    "SELECT CAST(COALESCE(SERVERPROPERTY('InstanceDefaultBackupPath'), SERVERPROPERTY('InstanceDefaultDataPath')) AS nvarchar(400))", ct)
                : _options.Directory!.Trim();
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("Yedek klasörü belirlenemedi: Backup:Directory ayarını verin.");
            TryCreateLocalDirectory(directory);

            var sep = directory.Contains('/') && !directory.Contains('\\') ? '/' : '\\';
            record.FilePath = $"{directory.TrimEnd('/', '\\')}{sep}{dbName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";

            db.BackupRecords.Add(record);
            await db.SaveChangesAsync(ct);

            await BackupAsync(connection, dbName, record.FilePath, ct);
            await ExecAsync(connection, "RESTORE VERIFYONLY FROM DISK = @p WITH CHECKSUM", ct, ("@p", record.FilePath));
            record.Verified = true;
            record.SizeBytes = await ScalarAsync<long?>(connection, """
                SELECT TOP 1 CAST(COALESCE(bs.compressed_backup_size, bs.backup_size) AS bigint)
                FROM msdb.dbo.backupset bs
                JOIN msdb.dbo.backupmediafamily mf ON mf.media_set_id = bs.media_set_id
                WHERE mf.physical_device_name = @p ORDER BY bs.backup_finish_date DESC
                """, ct, ("@p", record.FilePath));
            record.Succeeded = true;
            record.DeletedOldFiles = DeleteOldFiles(directory, dbName, record.FilePath);
            _logger.LogInformation("Yedek alındı ve doğrulandı: {Path} ({Size} bayt)", record.FilePath, record.SizeBytes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            record.Succeeded = false;
            record.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            _logger.LogError(ex, "Yedekleme başarısız ({Trigger})", trigger);
        }
        finally
        {
            record.FinishedAt = DateTime.UtcNow;
            if (record.Id == 0)
            {
                // Kilit/klasör hatası: kayıt henüz eklenmedi
                if (string.IsNullOrEmpty(record.FilePath)) record.FilePath = "-";
                db.BackupRecords.Add(record);
            }
            await db.SaveChangesAsync(CancellationToken.None);
            await connection.CloseAsync();
        }
        return record;
    }

    public async Task<BackupStatus> GetStatusAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var history = await db.BackupRecords.AsNoTracking()
            .OrderByDescending(b => b.StartedAt).Take(30).ToListAsync(ct);
        var lastSuccess = await db.BackupRecords.AsNoTracking()
            .Where(b => b.Succeeded).MaxAsync(b => (DateTime?)b.FinishedAt, ct);

        DateTime? next = _options.Enabled ? _options.NextRunAfter(DateTime.Now).ToUniversalTime() : null;
        // Gecikmiş: otomatik yedek açık ve son başarılı yedek 26 saatten eski (ya da hiç yok)
        var overdue = _options.Enabled && (lastSuccess is null || DateTime.UtcNow - lastSuccess > TimeSpan.FromHours(26));
        return new BackupStatus(_options.Enabled, _options.Directory, _options.DailyAt, _options.RetentionDays,
            lastSuccess, next, overdue, history);
    }

    private async Task BackupAsync(SqlConnection connection, string dbName, string path, CancellationToken ct)
    {
        var name = $"{dbName} tam yedek {DateTime.Now:yyyy-MM-dd HH:mm}";
        var sql = $"BACKUP DATABASE [{dbName.Replace("]", "]]")}] TO DISK = @p WITH INIT, CHECKSUM, NAME = @name{{0}}";
        try
        {
            await ExecAsync(connection, string.Format(sql, ", COMPRESSION"), ct, ("@p", path), ("@name", name));
        }
        catch (SqlException ex) when (ex.Number == 1844)
        {
            // Express / LocalDB sıkıştırmayı desteklemez
            await ExecAsync(connection, string.Format(sql, ""), ct, ("@p", path), ("@name", name));
        }
    }

    /// <summary>
    /// Saklama süresini aşan yedekleri siler. Klasör API'den görünmüyorsa (SQL Server başka
    /// makinede) dokunmaz. En yeni MinKeep dosya ve az önce alınan yedek asla silinmez.
    /// </summary>
    private int DeleteOldFiles(string directory, string dbName, string justCreated)
    {
        try
        {
            if (!System.IO.Directory.Exists(directory)) return 0;
            var files = new DirectoryInfo(directory).GetFiles($"{dbName}_*.bak")
                .OrderByDescending(f => f.LastWriteTimeUtc).ToList();
            var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, _options.RetentionDays));
            var deleted = 0;
            foreach (var f in files.Skip(Math.Max(1, _options.MinKeep)))
            {
                if (f.LastWriteTimeUtc >= cutoff) continue;
                if (string.Equals(f.FullName, justCreated, StringComparison.OrdinalIgnoreCase)) continue;
                f.Delete();
                deleted++;
            }
            if (deleted > 0) _logger.LogInformation("{Count} eski yedek silindi ({Days} günden eski)", deleted, _options.RetentionDays);
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Eski yedekler temizlenemedi");
            return 0;
        }
    }

    /// <summary>
    /// SQL Server BACKUP klasör oluşturmaz. Klasör bu makinedeyse (aynı sunucu / paylaşım)
    /// yoksa oluşturulur; uzak sunucu yolu ise dokunulmaz (SQL Server tarafında olmalı).
    /// </summary>
    private void TryCreateLocalDirectory(string directory)
    {
        try
        {
            if (Path.IsPathRooted(directory) && !System.IO.Directory.Exists(directory)
                && (OperatingSystem.IsWindows() ? directory.Contains('\\') || directory.Contains(':') : directory.StartsWith('/')))
                System.IO.Directory.CreateDirectory(directory);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Yedek klasörü bu makinede oluşturulamadı: {Dir}", directory);
        }
    }

    private async Task ExecAsync(SqlConnection c, string sql, CancellationToken ct, params (string, object)[] ps)
    {
        await using var cmd = Command(c, sql, ps);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<T> ScalarAsync<T>(SqlConnection c, string sql, CancellationToken ct, params (string, object)[] ps)
    {
        await using var cmd = Command(c, sql, ps);
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is null or DBNull ? default! : (T)value;
    }

    private SqlCommand Command(SqlConnection c, string sql, (string Name, object Value)[] ps)
    {
        var cmd = new SqlCommand(sql, c) { CommandTimeout = Math.Max(1, _options.TimeoutMinutes) * 60 };
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
        return cmd;
    }
}
