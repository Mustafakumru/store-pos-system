import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

import { ReportService, localDayRange } from '../../core/services/report.service';
import { apiErrorMessage } from '../../core/utils/api.util';
import { UtcPipe } from '../../shared/pipes/utc.pipe';
import { CashSessionListItem } from '../../shared/models/report.model';
import { downloadCsv } from '../../shared/utils/report-format';

/** Bu tutarın altındaki farklar "tutarlı" sayılır (bozuk para yuvarlaması). */
const TOLERANCE = 1;

@Component({
  selector: 'app-store-cash-sessions',
  standalone: true,
  imports: [CommonModule, FormsModule, TableModule, ButtonModule,
            CalendarModule, ToastModule, UtcPipe],
  providers: [MessageService],
  template: `
    <div class="space-y-6">

      <div>
        <h2 class="text-2xl font-bold text-gray-800">Kasa Oturumları</h2>
        <p class="text-gray-400 text-sm mt-1">
          Kasiyer avans / teslim kayıtları ve kasa farkları
        </p>
      </div>

      <!-- ── Özet ───────────────────────────────────────────── -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">Açık Kasa</p>
          <p class="text-3xl font-bold text-gray-800 mt-1">{{ openCount() }}</p>
        </div>
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">Kapanan Oturum</p>
          <p class="text-3xl font-bold text-gray-800 mt-1">{{ closedCount() }}</p>
        </div>
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">Farklı Kapanan</p>
          <p class="text-3xl font-bold mt-1"
             [class.text-red-600]="mismatchCount() > 0"
             [class.text-gray-800]="mismatchCount() === 0">
            {{ mismatchCount() }}
          </p>
        </div>
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">Net Kasa Farkı</p>
          <p class="text-2xl font-bold mt-1"
             [class.text-red-600]="totalDiff() < -tolerance"
             [class.text-gray-800]="totalDiff() >= -tolerance">
            {{ totalDiff() > 0 ? '+' : '' }}{{ totalDiff() | currency:'TRY':'symbol':'1.2-2' }}
          </p>
        </div>
      </div>

      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        <div class="p-4 flex items-center gap-3 flex-wrap border-b border-gray-100">
          <div class="flex gap-1 mr-auto">
            @for (f of filters; track f.value) {
              <button (click)="filter.set(f.value)"
                [class]="filter() === f.value
                  ? 'bg-red-500 text-white' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'"
                class="px-3 py-1.5 rounded-xl text-sm font-medium">
                {{ f.label }}
              </button>
            }
          </div>
          <p-calendar [(ngModel)]="dateRange" selectionMode="range"
                      [readonlyInput]="true" dateFormat="dd.mm.yy"
                      placeholder="Tarih aralığı" (onSelect)="onDateSelect()" />
          <p-button icon="pi pi-download" label="CSV" size="small"
                    severity="secondary" [outlined]="true"
                    [disabled]="filtered().length === 0" (onClick)="exportCsv()" />
        </div>

        <p-table [value]="filtered()" [loading]="loading()"
                 [paginator]="filtered().length > 20" [rows]="20"
                 styleClass="p-datatable-sm" [rowHover]="true">
          <ng-template pTemplate="header">
            <tr>
              <th class="text-xs font-semibold text-gray-500 uppercase">Kasiyer</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Açılış → Kapanış</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-right">Avans</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-right">Nakit Satış</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-right">Nakit İade</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-right">Beklenen</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-right">Teslim</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-right">Fark</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Onaylayan</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-s>
            <tr [class.bg-red-50]="isShort(s)">
              <td>
                <p class="font-semibold text-gray-800 text-sm">{{ s.cashierName || '—' }}</p>
                <p class="text-xs text-gray-400">{{ s.transactionCount }} işlem</p>
              </td>
              <td class="text-xs text-gray-500">
                {{ s.openedAt | utc | date:'dd.MM.yy HH:mm' }} →
                @if (s.status === 'Open') {
                  <span class="inline-flex items-center gap-1 text-emerald-700 font-medium">
                    <i class="pi pi-circle-fill" style="font-size: 6px"></i> Açık
                  </span>
                } @else {
                  {{ s.closedAt | utc | date:'dd.MM.yy HH:mm' }}
                }
              </td>
              <td class="text-right text-sm">{{ s.openingFloat | currency:'TRY':'symbol':'1.2-2' }}</td>
              <td class="text-right text-sm">{{ s.cashSalesTotal | currency:'TRY':'symbol':'1.2-2' }}</td>
              <td class="text-right text-sm text-gray-500">
                {{ s.cashRefundTotal ? '−' : '' }}{{ (s.cashRefundTotal ?? 0) | currency:'TRY':'symbol':'1.2-2' }}
              </td>
              <td class="text-right text-sm font-medium">{{ s.expectedCashAmount | currency:'TRY':'symbol':'1.2-2' }}</td>
              <td class="text-right text-sm">
                @if (s.closingAmount != null) {
                  {{ s.closingAmount | currency:'TRY':'symbol':'1.2-2' }}
                } @else { <span class="text-gray-300">—</span> }
              </td>
              <td class="text-right text-sm font-bold">
                @if (s.difference != null) {
                  @if (isShort(s)) {
                    <span class="text-red-600"><i class="pi pi-arrow-down text-xs"></i>
                      {{ s.difference | currency:'TRY':'symbol':'1.2-2' }} açık</span>
                  } @else if (s.difference > tolerance) {
                    <span class="text-amber-600"><i class="pi pi-arrow-up text-xs"></i>
                      +{{ s.difference | currency:'TRY':'symbol':'1.2-2' }} fazla</span>
                  } @else {
                    <span class="text-emerald-600"><i class="pi pi-check text-xs"></i> Tutarlı</span>
                  }
                } @else { <span class="text-gray-300">—</span> }
              </td>
              <td class="text-xs text-gray-500">
                <p>Açılış: {{ s.openingApprovedByName || '—' }}</p>
                @if (s.status === 'Closed') {
                  <p>Kapanış: {{ s.closingApprovedByName || '—' }}</p>
                }
              </td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr><td colspan="9">
              <div class="text-center py-12 text-gray-300">
                <i class="pi pi-wallet text-4xl mb-3 block"></i>
                <p>Bu dönemde kasa oturumu yok</p>
              </div>
            </td></tr>
          </ng-template>
        </p-table>

        <p class="px-4 py-3 text-xs text-gray-400 border-t border-gray-100">
          <i class="pi pi-info-circle mr-1"></i>
          Beklenen = avans + oturumda alınan nakit (para üstü düşülmüş) + hediye kartı satışı nakdi − kasiyerin verdiği nakit iadeler.
          Bu düzenlemeden önceki oturumlarda para üstü ve nakit iade izlenmediği için fark hatalı görünebilir.
        </p>
      </div>
    </div>
    <p-toast position="top-right" />
  `
})
export class StoreCashSessionsComponent implements OnInit {
  readonly tolerance = TOLERANCE;

  sessions  = signal<CashSessionListItem[]>([]);
  loading   = signal(false);
  filter    = signal<'all' | 'Open' | 'mismatch'>('all');
  dateRange: Date[] = [];

  filters: { label: string; value: 'all' | 'Open' | 'mismatch' }[] = [
    { label: 'Tümü',          value: 'all' },
    { label: 'Açık',          value: 'Open' },
    { label: 'Farklı Kapanan', value: 'mismatch' },
  ];

  readonly filtered = computed(() => {
    const f = this.filter();
    return this.sessions().filter(s =>
      f === 'all' ? true :
      f === 'Open' ? s.status === 'Open' :
      this.isMismatch(s));
  });

  readonly openCount     = computed(() => this.sessions().filter(s => s.status === 'Open').length);
  readonly closedCount   = computed(() => this.sessions().filter(s => s.status === 'Closed').length);
  readonly mismatchCount = computed(() => this.sessions().filter(s => this.isMismatch(s)).length);
  readonly totalDiff     = computed(() => this.sessions().reduce((t, s) => t + (s.difference ?? 0), 0));

  constructor(
    private reportService:  ReportService,
    private messageService: MessageService
  ) {}

  ngOnInit(): void {
    const start = new Date();
    start.setDate(start.getDate() - 6);   // varsayılan: son 7 gün
    this.dateRange = [start, new Date()];
    this.load();
  }

  isShort(s: CashSessionListItem): boolean {
    return s.difference != null && s.difference < -TOLERANCE;
  }

  isMismatch(s: CashSessionListItem): boolean {
    return s.difference != null && Math.abs(s.difference) > TOLERANCE;
  }

  onDateSelect(): void {
    if (this.dateRange?.length === 2 && this.dateRange[1]) this.load();
  }

  load(): void {
    const [start, end] = this.dateRange ?? [];
    const { from, to } = localDayRange(start ?? new Date(), end ?? start ?? new Date());
    this.loading.set(true);
    this.reportService.getCashSessions(from, to).subscribe({
      next: list => { this.sessions.set(list); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Kasa oturumları yüklenemedi.')
        });
      }
    });
  }

  exportCsv(): void {
    const local = (v?: string | null) =>
      v ? new Date(/[zZ]$/.test(v) ? v : v + 'Z').toLocaleString('tr-TR') : '';
    downloadCsv(
      `kasa-oturumlari-${new Date().toISOString().slice(0, 10)}.csv`,
      ['Kasiyer', 'Durum', 'Açılış', 'Kapanış', 'İşlem', 'Avans', 'Nakit Satış', 'Nakit İade',
       'Beklenen', 'Teslim', 'Fark', 'Açılış Onayı', 'Kapanış Onayı'],
      this.filtered().map(s => [
        s.cashierName ?? '', s.status === 'Open' ? 'Açık' : 'Kapalı',
        local(s.openedAt), local(s.closedAt), s.transactionCount,
        s.openingFloat, s.cashSalesTotal, s.cashRefundTotal ?? 0, s.expectedCashAmount,
        s.closingAmount ?? '', s.difference ?? '',
        s.openingApprovedByName ?? '', s.closingApprovedByName ?? ''
      ])
    );
  }
}
