import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuditService } from '../../core/services/audit.service';
import { apiErrorMessage } from '../../core/utils/api.util';
import { BackupStatus } from '../../shared/models/audit.model';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';

/** Veritabanı yedekleri: durum, geçmiş ve "Şimdi yedekle" (yalnızca Admin). */
@Component({
  selector: 'app-backups',
  standalone: true,
  imports: [CommonModule, TableModule, TooltipModule],
  template: `
    <div class="space-y-6">
      <div class="flex items-center justify-between gap-4 flex-wrap">
        <div>
          <h2 class="text-2xl font-bold text-gray-800">Veritabanı Yedekleri</h2>
          <p class="text-gray-400 text-sm mt-1">
            Her yedek alındıktan sonra okunabilirliği SQL Server'a doğrulatılır. Eski yedekler saklama süresine göre silinir.
          </p>
        </div>
        <button (click)="run()" [disabled]="running"
                class="inline-flex items-center gap-2 bg-red-500 hover:bg-red-600 disabled:opacity-60
                       text-white px-5 py-2.5 rounded-xl font-medium text-sm shadow-sm">
          <i [class]="'pi ' + (running ? 'pi-spin pi-spinner' : 'pi-database')"></i>
          {{ running ? 'Yedekleniyor...' : 'Şimdi yedekle' }}
        </button>
      </div>

      @if (message) {
        <div [class]="messageOk ? 'bg-green-50 text-green-800 border-green-200' : 'bg-red-50 text-red-800 border-red-200'"
             class="border rounded-xl px-4 py-3 text-sm">{{ message }}</div>
      }

      @if (status) {
        @if (status.isOverdue) {
          <div class="bg-red-50 border border-red-200 text-red-800 rounded-xl px-4 py-3 text-sm font-medium">
            <i class="pi pi-exclamation-triangle mr-1"></i>
            Son 26 saatte başarılı yedek yok. Yedek klasörünü ve sunucu loglarını kontrol edin.
          </div>
        }
        @if (!status.enabled) {
          <div class="bg-amber-50 border border-amber-200 text-amber-800 rounded-xl px-4 py-3 text-sm">
            Otomatik günlük yedek kapalı (Backup:Enabled). Elle yedek alınabilir; canlı ortamda açılması önerilir.
          </div>
        }

        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
            <p class="text-xs font-semibold text-gray-400 uppercase">Son başarılı yedek</p>
            <p class="text-lg font-bold mt-1" [class]="status.isOverdue ? 'text-red-600' : 'text-gray-800'">
              {{ status.lastSuccessAt ? (status.lastSuccessAt | date:'dd.MM.yyyy HH:mm') : 'Hiç yok' }}
            </p>
          </div>
          <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
            <p class="text-xs font-semibold text-gray-400 uppercase">Sonraki otomatik yedek</p>
            <p class="text-lg font-bold mt-1 text-gray-800">
              {{ status.nextRunAt ? (status.nextRunAt | date:'dd.MM.yyyy HH:mm') : 'Kapalı' }}
            </p>
          </div>
          <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
            <p class="text-xs font-semibold text-gray-400 uppercase">Saklama süresi</p>
            <p class="text-lg font-bold mt-1 text-gray-800">{{ status.retentionDays }} gün</p>
          </div>
          <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
            <p class="text-xs font-semibold text-gray-400 uppercase">Klasör</p>
            <p class="text-sm font-mono mt-2 text-gray-700 break-all">{{ status.directory || 'SQL Server varsayılanı' }}</p>
          </div>
        </div>

        <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
          <p-table [value]="status.history" styleClass="p-datatable-sm" [rowHover]="true">
            <ng-template pTemplate="header">
              <tr>
                <th class="text-xs font-semibold text-gray-500 uppercase">Başlangıç</th>
                <th class="text-xs font-semibold text-gray-500 uppercase">Tür</th>
                <th class="text-xs font-semibold text-gray-500 uppercase">Sonuç</th>
                <th class="text-xs font-semibold text-gray-500 uppercase">Boyut</th>
                <th class="text-xs font-semibold text-gray-500 uppercase">Süre</th>
                <th class="text-xs font-semibold text-gray-500 uppercase">Dosya</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-b>
              <tr class="border-b border-gray-50">
                <td class="text-xs whitespace-nowrap">{{ b.startedAt | date:'dd.MM.yyyy HH:mm:ss' }}</td>
                <td class="text-sm">{{ b.trigger }}@if (b.requestedBy && b.requestedBy !== 'sistem') { <span class="text-xs text-gray-400"> · {{ b.requestedBy }}</span> }</td>
                <td>
                  @if (b.succeeded) {
                    <span class="text-xs font-semibold text-green-700 bg-green-50 px-2 py-0.5 rounded-lg">
                      Başarılı{{ b.verified ? ' · doğrulandı' : '' }}
                    </span>
                  } @else {
                    <span class="text-xs font-semibold text-red-700 bg-red-50 px-2 py-0.5 rounded-lg" [pTooltip]="b.error">Başarısız</span>
                  }
                </td>
                <td class="text-sm text-gray-600">{{ size(b.sizeBytes) }}</td>
                <td class="text-sm text-gray-600">{{ duration(b) }}</td>
                <td class="text-xs font-mono text-gray-500 break-all">{{ b.filePath }}</td>
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr><td colspan="6" class="text-center text-gray-400 py-10">Henüz yedek alınmadı</td></tr>
            </ng-template>
          </p-table>
        </div>
      }
    </div>
  `
})
export class BackupComponent implements OnInit {
  private readonly audit = inject(AuditService);
  status: BackupStatus | null = null;
  running = false;
  message = '';
  messageOk = true;

  ngOnInit(): void { this.refresh(); }

  refresh(): void {
    this.audit.getBackupStatus().subscribe({
      next: s => this.status = s,
      error: err => { this.messageOk = false; this.message = apiErrorMessage(err, 'Yedek durumu alınamadı.'); }
    });
  }

  run(): void {
    this.running = true;
    this.message = '';
    this.audit.runBackup().subscribe({
      next: b => {
        this.running = false; this.messageOk = true;
        this.message = `Yedek alındı ve doğrulandı: ${b.filePath} (${this.size(b.sizeBytes)})`;
        this.refresh();
      },
      error: err => {
        this.running = false; this.messageOk = false;
        this.message = apiErrorMessage(err, 'Yedek alınamadı.');
        this.refresh();
      }
    });
  }

  size(bytes?: number): string {
    if (!bytes) return '—';
    const mb = bytes / 1024 / 1024;
    return mb >= 1 ? `${mb.toLocaleString('tr-TR', { maximumFractionDigits: 1 })} MB` : `${Math.round(bytes / 1024)} KB`;
  }

  duration(b: { startedAt: string; finishedAt?: string }): string {
    if (!b.finishedAt) return '—';
    const s = (new Date(b.finishedAt).getTime() - new Date(b.startedAt).getTime()) / 1000;
    return s < 60 ? `${s.toFixed(1)} sn` : `${Math.round(s / 60)} dk`;
  }
}
