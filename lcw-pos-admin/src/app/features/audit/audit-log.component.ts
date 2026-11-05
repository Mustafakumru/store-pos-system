import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuditService } from '../../core/services/audit.service';
import { AuthService } from '../../core/services/auth.service';
import { StoreService } from '../../core/services/store.service';
import { apiErrorMessage } from '../../core/utils/api.util';
import { AuditActionOption, AuditLog, AuditLogFilter } from '../../shared/models/audit.model';
import { Store } from '../../shared/models/store.model';

import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { CalendarModule } from 'primeng/calendar';
import { DropdownModule } from 'primeng/dropdown';
import { TooltipModule } from 'primeng/tooltip';

/**
 * İşlem kaydı: kim, hangi mağazada, ne zaman, ne yaptı, sonuç.
 * Admin tüm mağazaları (mağaza filtresiyle), mağaza müdürü sadece kendi
 * mağazasını görür (sunucu zorlar). Satıra tıklayınca ayrıntı açılır.
 */
@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [CommonModule, FormsModule, TableModule, CalendarModule, DropdownModule, TooltipModule],
  template: `
    <div class="space-y-6">
      <div>
        <h2 class="text-2xl font-bold text-gray-800">İşlem Kaydı</h2>
        <p class="text-gray-400 text-sm mt-1">
          Satış, iade, onay, giriş denemesi ve ana veri değişiklikleri — kim, ne zaman, ne yaptı.
          Kayıtlar değiştirilemez ve silinemez. PIN, şifre ve kart numaraları kayda yazılmaz.
        </p>
      </div>

      <!-- ── Filtreler ─────────────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
        <div class="grid grid-cols-1 md:grid-cols-3 xl:grid-cols-6 gap-3 items-end">
          <label class="text-xs font-semibold text-gray-500">Başlangıç
            <p-calendar [(ngModel)]="filter.from" dateFormat="dd.mm.yy" [showIcon]="true"
                        styleClass="w-full mt-1" inputStyleClass="text-sm" placeholder="gg.aa.yyyy" />
          </label>
          <label class="text-xs font-semibold text-gray-500">Bitiş
            <p-calendar [(ngModel)]="filter.to" dateFormat="dd.mm.yy" [showIcon]="true"
                        styleClass="w-full mt-1" inputStyleClass="text-sm" placeholder="gg.aa.yyyy" />
          </label>
          <label class="text-xs font-semibold text-gray-500">İşlem
            <p-dropdown [options]="actions" [(ngModel)]="filter.action" optionLabel="label" optionValue="key"
                        [showClear]="true" [filter]="true" placeholder="Tümü" styleClass="w-full mt-1 text-sm" />
          </label>
          @if (isAdmin) {
            <label class="text-xs font-semibold text-gray-500">Mağaza
              <p-dropdown [options]="stores" [(ngModel)]="filter.storeId" optionLabel="name" optionValue="id"
                          [showClear]="true" [filter]="true" placeholder="Tümü" styleClass="w-full mt-1 text-sm" />
            </label>
          }
          <label class="text-xs font-semibold text-gray-500">Personel no / ad
            <input [(ngModel)]="filter.user" (keyup.enter)="search()"
                   class="w-full mt-1 px-3 py-2 text-sm border border-gray-200 rounded-lg" />
          </label>
          <label class="text-xs font-semibold text-gray-500">Fiş no / kayıt no
            <input [(ngModel)]="filter.entityId" (keyup.enter)="search()"
                   class="w-full mt-1 px-3 py-2 text-sm border border-gray-200 rounded-lg" />
          </label>
        </div>
        <div class="flex items-center gap-2 mt-4 flex-wrap">
          @for (r of resultFilters; track r.label) {
            <button (click)="filter.succeeded = r.value; search()"
                    [class]="filter.succeeded === r.value ? 'bg-red-500 text-white' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'"
                    class="px-3 py-1.5 rounded-xl text-xs font-medium transition-colors">{{ r.label }}</button>
          }
          <span class="flex-1"></span>
          <button (click)="clear()" class="px-4 py-2 rounded-xl text-sm text-gray-600 bg-gray-100 hover:bg-gray-200">Temizle</button>
          <button (click)="search()" class="px-5 py-2 rounded-xl text-sm text-white bg-red-500 hover:bg-red-600 font-medium">
            <i class="pi pi-search mr-1"></i> Ara
          </button>
        </div>
        @if (error) { <p class="text-sm text-red-600 mt-3">{{ error }}</p> }
      </div>

      <!-- ── Liste ─────────────────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        <p-table [value]="rows" [lazy]="true" (onLazyLoad)="load($event)" [loading]="loading"
                 [paginator]="true" [rows]="filter.pageSize" [totalRecords]="total" [rowsPerPageOptions]="[25, 50, 100]"
                 dataKey="id" [expandedRowKeys]="expanded" styleClass="p-datatable-sm" [rowHover]="true">
          <ng-template pTemplate="header">
            <tr>
              <th class="text-xs font-semibold text-gray-500 uppercase w-40">Zaman</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">İşlem</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Kim</th>
              @if (isAdmin) { <th class="text-xs font-semibold text-gray-500 uppercase">Mağaza</th> }
              <th class="text-xs font-semibold text-gray-500 uppercase">Sonuç</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Özet</th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-log let-isExpanded="expanded">
            <tr class="border-b border-gray-50 cursor-pointer" (click)="toggle(log)">
              <td class="text-xs text-gray-600 whitespace-nowrap">
                <i [class]="'pi text-gray-300 mr-1 ' + (isExpanded ? 'pi-chevron-down' : 'pi-chevron-right')"></i>
                {{ log.occurredAt | date:'dd.MM.yyyy HH:mm:ss' }}
              </td>
              <td>
                <span class="font-medium text-gray-800 text-sm">{{ log.actionLabel }}</span>
                @if (log.category === 'Değişiklik') {
                  <span class="ml-1 text-[10px] bg-blue-50 text-blue-600 px-1.5 py-0.5 rounded">alan değişikliği</span>
                }
              </td>
              <td class="text-sm">
                <span class="text-gray-800">{{ log.userName || '—' }}</span>
                @if (log.personelId) { <span class="text-xs text-gray-400 ml-1">#{{ log.personelId }}</span> }
              </td>
              @if (isAdmin) { <td class="text-sm text-gray-600">{{ log.storeName || '—' }}</td> }
              <td>
                @if (log.succeeded) {
                  <span class="text-xs font-semibold text-green-700 bg-green-50 px-2 py-0.5 rounded-lg">Başarılı</span>
                } @else {
                  <span class="text-xs font-semibold text-red-700 bg-red-50 px-2 py-0.5 rounded-lg">
                    Reddedildi{{ log.statusCode ? ' (' + log.statusCode + ')' : '' }}
                  </span>
                }
              </td>
              <td class="text-sm text-gray-600 max-w-md truncate" [pTooltip]="log.summary" tooltipPosition="top">
                {{ log.summary || '—' }}
              </td>
            </tr>
          </ng-template>

          <ng-template pTemplate="rowexpansion" let-log>
            <tr>
              <td [attr.colspan]="isAdmin ? 6 : 5" class="bg-gray-50">
                <div class="grid md:grid-cols-3 gap-4 text-xs p-2">
                  <div class="space-y-1 text-gray-600">
                    <p><b>Anahtar:</b> <span class="font-mono">{{ log.action }}</span></p>
                    @if (log.entityId) { <p><b>Kayıt:</b> {{ log.entityType }} {{ log.entityId }}</p> }
                    @if (log.roles) { <p><b>Rol:</b> {{ log.roles }}</p> }
                    @if (log.path) { <p><b>İstek:</b> <span class="font-mono">{{ log.httpMethod }} {{ log.path }}</span></p> }
                    @if (log.ipAddress) { <p><b>IP:</b> {{ log.ipAddress }}</p> }
                    @if (log.durationMs !== null && log.durationMs !== undefined) { <p><b>Süre:</b> {{ log.durationMs }} ms</p> }
                    @if (log.correlationId) {
                      <p><b>İlgili kayıtlar:</b>
                        <button class="text-red-600 underline bg-transparent border-0 cursor-pointer p-0"
                                (click)="$event.stopPropagation(); showRelated(log)">aynı isteğin değişikliklerini göster</button>
                      </p>
                    }
                  </div>
                  <pre class="md:col-span-2 bg-white border border-gray-200 rounded-lg p-3 overflow-auto max-h-72 whitespace-pre-wrap">{{ pretty(log.details) }}</pre>
                </div>
              </td>
            </tr>
          </ng-template>

          <ng-template pTemplate="emptymessage">
            <tr><td [attr.colspan]="isAdmin ? 6 : 5" class="text-center text-gray-400 py-10">Kayıt bulunamadı</td></tr>
          </ng-template>
        </p-table>
      </div>
    </div>
  `
})
export class AuditLogComponent implements OnInit {
  private readonly audit = inject(AuditService);
  private readonly storesApi = inject(StoreService);
  readonly isAdmin = inject(AuthService).isAdmin();

  rows: AuditLog[] = [];
  total = 0;
  loading = false;
  error = '';
  actions: AuditActionOption[] = [];
  stores: Store[] = [];
  expanded: Record<number, boolean> = {};

  filter: AuditLogFilter = this.emptyFilter();

  readonly resultFilters: { label: string; value: boolean | null }[] = [
    { label: 'Tümü', value: null }, { label: 'Başarılı', value: true }, { label: 'Reddedilen', value: false }
  ];

  /** İlişkili kayıt araması için (aynı istek). */
  private correlationRows: AuditLog[] | null = null;

  ngOnInit(): void {
    this.audit.getActions().subscribe({ next: a => this.actions = a });
    if (this.isAdmin) this.storesApi.getAll().subscribe({ next: s => this.stores = s });
  }

  load(event?: TableLazyLoadEvent): void {
    if (event) {
      this.filter.pageSize = event.rows ?? this.filter.pageSize;
      this.filter.page = Math.floor((event.first ?? 0) / this.filter.pageSize) + 1;
    }
    this.correlationRows = null;
    this.loading = true;
    this.error = '';
    this.audit.getLogs(this.filter).subscribe({
      next: page => { this.rows = page.items; this.total = page.totalCount; this.loading = false; },
      error: err => { this.error = apiErrorMessage(err, 'İşlem kaydı yüklenemedi.'); this.loading = false; }
    });
  }

  search(): void {
    this.filter.page = 1;
    this.expanded = {};
    this.load();
  }

  clear(): void {
    this.filter = this.emptyFilter();
    this.search();
  }

  toggle(log: AuditLog): void {
    this.expanded = this.expanded[log.id] ? {} : { [log.id]: true };
  }

  /** Aynı istekte yapılan istek + alan değişikliği kayıtlarını listeler. */
  showRelated(log: AuditLog): void {
    if (!log.correlationId) return;
    this.rows = this.rows.filter(r => r.correlationId === log.correlationId);
    this.total = this.rows.length;
  }

  pretty(details?: string): string {
    if (!details) return '(ayrıntı yok)';
    try { return JSON.stringify(JSON.parse(details), null, 2); } catch { return details; }
  }

  private emptyFilter(): AuditLogFilter {
    const from = new Date(); from.setDate(from.getDate() - 7);
    return { from, to: null, storeId: null, user: '', action: null, entityId: '', succeeded: null, page: 1, pageSize: 50 };
  }
}
