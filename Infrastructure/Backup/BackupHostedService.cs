using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Backup;

/// <summary>
/// Günlük otomatik yedek (Backup:Enabled). Sunucu planlı saatte kapalıysa yedek
/// kaçmasın diye açılışta son başarılı yedek 24 saatten eskiyse birkaç dakika
/// sonra telafi yedeği alır. Sonuç işlem kaydına da yazılır.
/// </summary>
public class BackupHostedService : BackgroundService
{
    private static readonly TimeSpan CatchUpDelay = TimeSpan.FromMinutes(2);
    private readonly DatabaseBackupService _backup;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BackupHostedService> _logger;

    public BackupHostedService(DatabaseBackupService backup, IServiceScopeFactory scopes, ILogger<BackupHostedService> logger)
    {
        _backup = backup;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_backup.Options.Enabled)
        {
            _logger.LogInformation("Otomatik yedek kapalı (Backup:Enabled=false).");
            return;
        }

        try
        {
            if (await NeedsCatchUpAsync(stoppingToken))
            {
                _logger.LogWarning("Son 24 saatte başarılı yedek yok: {Delay} sonra telafi yedeği alınacak.", CatchUpDelay);
                await Task.Delay(CatchUpDelay, stoppingToken);
                await RunAndAuditAsync("Açılış (gecikmiş yedek)", stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var next = _backup.Options.NextRunAfter(DateTime.Now);
                _logger.LogInformation("Sonraki otomatik yedek: {Next:yyyy-MM-dd HH:mm}", next);
                await Task.Delay(next - DateTime.Now, stoppingToken);
                await RunAndAuditAsync("Otomatik", stoppingToken);
            }
        }
        catch (OperationCanceledException) { /* kapanış */ }
    }

    private async Task<bool> NeedsCatchUpAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var last = await db.BackupRecords.Where(b => b.Succeeded).MaxAsync(b => (DateTime?)b.FinishedAt, ct);
        return last is null || DateTime.UtcNow - last > TimeSpan.FromHours(24);
    }

    private async Task RunAndAuditAsync(string trigger, CancellationToken ct)
    {
        var record = await _backup.RunAsync(trigger, "sistem", ct);
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AuditLogs.Add(new AuditLog
            {
                OccurredAt = DateTime.UtcNow,
                Category = AuditCategory.Request,
                Action = "System.Backup",
                Succeeded = record.Succeeded,
                UserId = "system",
                UserName = "Sistem",
                EntityType = "Backup",
                EntityId = record.Id.ToString(),
                Summary = record.Succeeded ? $"{trigger}: {record.FilePath}" : $"{trigger} BAŞARISIZ: {record.Error}"
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yedek sonucu işlem kaydına yazılamadı");
        }
    }
}
