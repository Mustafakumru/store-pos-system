import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

import { ReportService, localDayRange } from '../../core/services/report.service';
import { apiErrorMessage } from '../../core/utils/api.util';
import { UtcPipe } from '../../shared/pipes/utc.pipe';
import { XReport, ZReportListItem } from '../../shared/models/report.model';
import { downloadCsv } from '../../shared/utils/report-format';

@Component({
  selector: 'app-store-reports',
  standalone: true,
  imports: [
    CommonModule, FormsModule, TableModule, ButtonModule,
    CalendarModule, ToastModule, TooltipModule, UtcPipe
  ],
  providers: [MessageService],
  template: `
    <div class="space-y-6">

      <!-- ── Başlık ─────────────────────────────────────────── -->
      <div class="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h2 class="text-2xl font-bold text-gray-800">X / Z Raporları</h2>
          <p class="text-gray-400 text-sm mt-1">
            Açık dönem (X) ve kapatılmış gün sonu raporları (Z)
          </p>
        </div>
        <p class="text-xs text-gray-400 max-w-sm">
          <i class="pi pi-info-circle mr-1"></i>
          Z raporu (gün sonu kapanışı) kasadan alınır; bu ekran yalnızca görüntüler.
        </p>
      </div>

      <!-- ── X Raporu (açık dönem) ──────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
        <div class="flex items-center justify-between mb-4 flex-wrap gap-2">
          <div>
            <h3 class="font-bold text-gray-800 flex items-center gap-2">
              <i class="pi pi-clock text-blue-500"></i>
              X Raporu — Açık Dönem
            </h3>
            @if (x(); as xr) {
              <p class="text-xs text-gray-400 mt-1">
                @if (hasPreviousZ()) {
                  Son Z (#{{ xr.lastZReportNumber }}) sonrası:
                  {{ xr.fromDate | utc | date:'dd.MM.yyyy HH:mm' }} →
                  {{ xr.toDate | utc | date:'dd.MM.yyyy HH:mm' }}
                } @else {
                  Henüz Z raporu alınmamış — tüm satışlar dahil
                }
              </p>
            }
          </div>
          <p-button icon="pi pi-refresh" label="Yenile" size="small"
                    severity="secondary" [outlined]="true"
                    [loading]="xLoading()" (onClick)="loadX()" />
        </div>

        @if (openDays() >= 2) {
          <div class="bg-amber-50 border border-amber-200 rounded-xl px-4 py-2.5
                      text-sm text-amber-700 mb-4">
            <i class="pi pi-exclamation-triangle mr-1"></i>
            Son Z raporundan bu yana {{ openDays() }} gün geçmiş. Gün sonu kapanışı yapılmamış olabilir.
          </div>
        }

        @if (x(); as xr) {
          <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
            <div class="bg-gray-50 rounded-xl p-4">
              <p class="text-xs font-semibold text-gray-400 uppercase">Net Tutar</p>
              <p class="text-2xl font-bold text-gray-800 mt-1">
                {{ xr.netAmount | currency:'TRY':'symbol':'1.2-2' }}
              </p>
              <p class="text-xs text-gray-400 mt-0.5">
                Satış {{ xr.totalSalesAmount | currency:'TRY':'symbol':'1.2-2' }}
                − İade {{ xr.totalRefundsAmount | currency:'TRY':'symbol':'1.2-2' }}
              </p>
            </div>
            <div class="bg-gray-50 rounded-xl p-4">
              <p class="text-xs font-semibold text-gray-400 uppercase">İşlem</p>
              <p class="text-2xl font-bold text-gray-800 mt-1">{{ xr.transactionCount }}</p>
              <p class="text-xs text-gray-400 mt-0.5">{{ xr.refundCount }} iade işlemi</p>
            </div>
            <div class="bg-gray-50 rounded-xl p-4">
              <p class="text-xs font-semibold text-gray-400 uppercase">Nakit</p>
              <p class="text-2xl font-bold text-gray-800 mt-1">
                {{ xr.cashTotal | currency:'TRY':'symbol':'1.2-2' }}
              </p>
              @if (xr.giftCardSoldCash) {
                <p class="text-xs text-gray-400 mt-0.5">
                  H.kart satışı nakit +{{ xr.giftCardSoldCash | currency:'TRY':'symbol':'1.2-2' }} (emanet)
                </p>
              }
              @if (xr.cashRefundTotal || xr.giftCardSoldCash) {
                <p class="text-xs text-gray-400 mt-0.5">
                  Nakit iade −{{ (xr.cashRefundTotal ?? 0) | currency:'TRY':'symbol':'1.2-2' }} ·
                  kasada net <b class="text-gray-600">{{ (xr.netCash ?? (xr.cashTotal - (xr.cashRefundTotal ?? 0) + (xr.giftCardSoldCash ?? 0))) | currency:'TRY':'symbol':'1.2-2' }}</b>
                </p>
              }
            </div>
            <div class="bg-gray-50 rounded-xl p-4">
              <p class="text-xs font-semibold text-gray-400 uppercase">Kart / Hediye Kartı</p>
              <p class="text-2xl font-bold text-gray-800 mt-1">
                {{ xr.cardTotal | currency:'TRY':'symbol':'1.2-2' }}
              </p>
              <p class="text-xs text-gray-400 mt-0.5">
                Hediye kartı {{ xr.giftCardTotal | currency:'TRY':'symbol':'1.2-2' }}
              </p>
            </div>
          </div>
        } @else if (!xLoading()) {
          <p class="text-sm text-gray-400 text-center py-6">X raporu yüklenemedi.</p>
        }
      </div>

      <!-- ── Z Raporları ────────────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        <div class="p-4 flex items-center gap-3 flex-wrap border-b border-gray-100">
          <h3 class="font-bold text-gray-800 flex items-center gap-2 mr-auto">
            <i class="pi pi-lock text-gray-500"></i>
            Z Raporları
          </h3>
          <p-calendar [(ngModel)]="dateRange" selectionMode="range"
                      [readonlyInput]="true" dateFormat="dd.mm.yy"
                      placeholder="Tarih aralığı" (onSelect)="onDateSelect()" />
          <p-button icon="pi pi-download" label="CSV" size="small"
                    severity="secondary" [outlined]="true"
                    [disabled]="zReports().length === 0" (onClick)="exportCsv()" />
        </div>

        <!-- Dönem toplamı -->
        @if (zReports().length > 0) {
          <div class="grid grid-cols-2 lg:grid-cols-4 gap-4 px-5 py-4 bg-gray-50 text-sm">
            <div><span class="text-gray-400">Z sayısı:</span>
              <b class="text-gray-800 ml-1">{{ zReports().length }}</b></div>
            <div><span class="text-gray-400">Net toplam:</span>
              <b class="text-gray-800 ml-1">{{ totals().net | currency:'TRY':'symbol':'1.2-2' }}</b></div>
            <div><span class="text-gray-400">Nakit:</span>
              <b class="text-gray-800 ml-1">{{ totals().cash | currency:'TRY':'symbol':'1.2-2' }}</b></div>
            <div><span class="text-gray-400">Kart:</span>
              <b class="text-gray-800 ml-1">{{ totals().card | currency:'TRY':'symbol':'1.2-2' }}</b></div>
          </div>
        }

        <p-table [value]="zReports()" [loading]="zLoading()"
                 [paginator]="zReports().length > 15" [rows]="15"
                 styleClass="p-datatable-sm" [rowHover]="true">
          <ng-template pTemplate="header">
            <tr>
              <th class="text-xs font-semibold text-gray-500 uppercase">Z No</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Dönem</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Alan</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">İşlem</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-right">Satış</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-right">İade</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-right">Nakit</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-right">Kart</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-right">Net</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-z>
            <tr>
              <td>
                <span class="font-mono text-xs bg-gray-50 border border-gray-200
                             px-2.5 py-1 rounded-lg text-gray-700 font-semibold">
                  #{{ z.reportNumber }}
                </span>
              </td>
              <td class="text-xs text-gray-500">
                @if (isOpenStart(z.fromDate)) { Başlangıç } @else {
                  {{ z.fromDate | utc | date:'dd.MM.yy HH:mm' }}
                }
                → <span class="text-gray-700 font-medium">{{ z.toDate | utc | date:'dd.MM.yy HH:mm' }}</span>
              </td>
              <td class="text-sm text-gray-600">{{ z.cashierName || '—' }}</td>
              <td class="text-center text-sm">
                {{ z.transactionCount }}
                @if (z.refundCount > 0) {
                  <span class="text-xs text-gray-400">/ {{ z.refundCount }} iade</span>
                }
              </td>
              <td class="text-right text-sm">{{ z.totalSalesAmount | currency:'TRY':'symbol':'1.2-2' }}</td>
              <td class="text-right text-sm text-gray-500">
                {{ z.totalRefundsAmount > 0 ? '−' : '' }}{{ z.totalRefundsAmount | currency:'TRY':'symbol':'1.2-2' }}
              </td>
              <td class="text-right text-sm">
                {{ z.cashTotal | currency:'TRY':'symbol':'1.2-2' }}
                @if (z.cashRefundTotal) {
                  <p class="text-xs text-gray-400">iade −{{ z.cashRefundTotal | currency:'TRY':'symbol':'1.2-2' }}</p>
                }
              </td>
              <td class="text-right text-sm">{{ z.cardTotal | currency:'TRY':'symbol':'1.2-2' }}</td>
              <td class="text-right font-bold text-gray-800">{{ z.netAmount | currency:'TRY':'symbol':'1.2-2' }}</td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="9">
                <div class="text-center py-12 text-gray-300">
                  <i class="pi pi-lock text-4xl mb-3 block"></i>
                  <p>Bu dönemde Z raporu yok</p>
                </div>
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    </div>
    <p-toast position="top-right" />
  `
})
export class StoreReportsComponent implements OnInit {
  x         = signal<XReport | null>(null);
  xLoading  = signal(false);
  zReports  = signal<ZReportListItem[]>([]);
  zLoading  = signal(false);
  dateRange: Date[] = [];

  /** .NET DateTime.MinValue ("0001-01-01") = hiç Z alınmamış. */
  isOpenStart(value?: string | null): boolean {
    return !value || value.startsWith('0001-');
  }

  readonly hasPreviousZ = computed(() => {
    const xr = this.x();
    return !!xr && xr.lastZReportNumber != null && !this.isOpenStart(xr.fromDate);
  });

  /** Açık dönemin kaç takvim günüdür sürdüğü (son Z'den bu yana). */
  readonly openDays = computed(() => {
    const xr = this.x();
    if (!xr || !this.hasPreviousZ()) return 0;
    const from = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(xr.fromDate) ? xr.fromDate : xr.fromDate + 'Z');
    return Math.floor((Date.now() - from.getTime()) / 86_400_000);
  });

  readonly totals = computed(() => this.zReports().reduce(
    (t, z) => ({ net: t.net + z.netAmount, cash: t.cash + z.cashTotal, card: t.card + z.cardTotal }),
    { net: 0, cash: 0, card: 0 }));

  constructor(
    private reportService:  ReportService,
    private messageService: MessageService
  ) {}

  ngOnInit(): void {
    // Varsayılan: son 30 gün
    const end = new Date();
    const start = new Date();
    start.setDate(start.getDate() - 29);
    this.dateRange = [start, end];
    this.loadX();
    this.loadZ();
  }

  loadX(): void {
    this.xLoading.set(true);
    this.reportService.getXReport().subscribe({
      next: r => { this.x.set(r); this.xLoading.set(false); },
      error: err => {
        this.xLoading.set(false);
        this.toastError(err, 'X raporu yüklenemedi.');
      }
    });
  }

  onDateSelect(): void {
    if (this.dateRange?.length === 2 && this.dateRange[1]) this.loadZ();
  }

  loadZ(): void {
    const [start, end] = this.dateRange ?? [];
    const { from, to } = localDayRange(start ?? new Date(), end ?? start ?? new Date());
    this.zLoading.set(true);
    this.reportService.getZReports(from, to).subscribe({
      next: list => { this.zReports.set(list); this.zLoading.set(false); },
      error: err => {
        this.zLoading.set(false);
        this.toastError(err, 'Z raporları yüklenemedi.');
      }
    });
  }

  exportCsv(): void {
    const utc = (v: string) => new Date(/[zZ]$/.test(v) ? v : v + 'Z');
    const fmt = (v: string) => this.isOpenStart(v) ? 'Başlangıç' : utc(v).toLocaleString('tr-TR');
    downloadCsv(
      `z-raporlari-${new Date().toISOString().slice(0, 10)}.csv`,
      ['Z No', 'Başlangıç', 'Bitiş', 'Alan', 'İşlem', 'İade Adedi',
       'Satış', 'İade', 'Nakit', 'Nakit İade', 'Kart', 'Karta İade', 'Hediye Kartı', 'Net'],
      this.zReports().map(z => [
        z.reportNumber, fmt(z.fromDate), fmt(z.toDate), z.cashierName ?? '',
        z.transactionCount, z.refundCount, z.totalSalesAmount, z.totalRefundsAmount,
        z.cashTotal, z.cashRefundTotal ?? 0, z.cardTotal, z.cardRefundTotal ?? 0,
        z.giftCardTotal, z.netAmount
      ])
    );
  }

  private toastError(err: unknown, fallback: string): void {
    this.messageService.add({
      severity: 'error', summary: 'Hata', detail: apiErrorMessage(err, fallback)
    });
  }
}
