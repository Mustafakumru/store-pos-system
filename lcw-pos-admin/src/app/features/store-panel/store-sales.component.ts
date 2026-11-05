import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { apiErrorMessage } from '../../core/utils/api.util';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { CalendarModule } from 'primeng/calendar';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { UtcPipe } from '../../shared/pipes/utc.pipe';
import { BarChartComponent, BarDatum } from '../../shared/components/bar-chart.component';
import { ReportService } from '../../core/services/report.service';
import { CashierBreakdown, SalesSummary, EMPTY_SALES_SUMMARY as EMPTY_SUMMARY } from '../../shared/models/report.model';
import { formatMoney, hourlyToBars, dailyToBars, downloadCsv } from '../../shared/utils/report-format';

@Component({
  selector: 'app-store-sales',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    TableModule, ButtonModule, DialogModule,
    TagModule, ToastModule, CalendarModule, TooltipModule, UtcPipe,
    BarChartComponent
  ],
  providers: [MessageService],
  template: `
    <div class="space-y-6">

      <!-- ── Başlık ─────────────────────────────────────────── -->
      <div>
        <h2 class="text-2xl font-bold text-gray-800">Satış Geçmişi</h2>
        <p class="text-gray-400 text-sm mt-1">
          Mağazanızın satış kayıtları · {{ periodLabel }}
        </p>
      </div>

      <!-- ── Günlük Özet ────────────────────────────────────── -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">Satış Adedi</p>
          <p class="text-3xl font-bold text-gray-800 mt-1">
            {{ summary.totalSales }}
          </p>
          @if (summary.refundCount > 0) {
            <p class="text-xs text-orange-500 mt-1">{{ summary.refundCount }} iade işlemi</p>
          }
        </div>
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">Net Ciro</p>
          <p class="text-2xl font-bold text-emerald-600 mt-1">
            {{ summary.netRevenue | currency:'TRY':'symbol':'1.0-0' }}
          </p>
          @if (summary.refundTotal > 0) {
            <p class="text-xs text-gray-400 mt-1">
              Brüt {{ summary.totalRevenue | currency:'TRY':'symbol':'1.0-0' }} −
              iade {{ summary.refundTotal | currency:'TRY':'symbol':'1.0-0' }}
            </p>
          }
        </div>
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">Nakit</p>
          <p class="text-2xl font-bold text-blue-600 mt-1">
            {{ summary.cashTotal | currency:'TRY':'symbol':'1.0-0' }}
          </p>
          @if (summary.cashRefundTotal) {
            <p class="text-xs text-gray-400 mt-1">
              Nakit iade −{{ summary.cashRefundTotal | currency:'TRY':'symbol':'1.0-0' }} ·
              net {{ summary.cashTotal - (summary.cashRefundTotal ?? 0) | currency:'TRY':'symbol':'1.0-0' }}
            </p>
          }
        </div>
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">Kart</p>
          <p class="text-2xl font-bold text-purple-600 mt-1">
            {{ summary.cardTotal | currency:'TRY':'symbol':'1.0-0' }}
          </p>
          @if (summary.cardRefundTotal) {
            <p class="text-xs text-gray-400 mt-1">
              Karta iade −{{ summary.cardRefundTotal | currency:'TRY':'symbol':'1.0-0' }}
            </p>
          }
          @if (summary.giftCardTotal > 0) {
            <p class="text-xs text-gray-400 mt-1">
              Hediye kartı {{ summary.giftCardTotal | currency:'TRY':'symbol':'1.0-0' }}
            </p>
          }
        </div>
      </div>

      <!-- ── Grafik + Kasiyer Performansı ───────────────────── -->
      <div class="grid grid-cols-1 lg:grid-cols-5 gap-6">
        <div class="lg:col-span-3 bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
          <h3 class="font-bold text-gray-800 text-sm">
            {{ isSingleDay ? 'Saatlik satış tutarı' : 'Günlük satış tutarı' }}
          </h3>
          <p class="text-xs text-gray-400 mb-3">Brüt satış (iadeler düşülmeden)</p>
          <app-bar-chart [data]="chartBars" [height]="180"
                         [ariaLabel]="isSingleDay ? 'Saatlik satış tutarı' : 'Günlük satış tutarı'"
                         [formatter]="formatMoney" />
        </div>

        <div class="lg:col-span-2 bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
          <h3 class="font-bold text-gray-800 text-sm mb-3">Kasiyer Performansı</h3>
          @if (cashiers.length === 0) {
            <p class="text-sm text-gray-400 text-center py-10">Bu dönemde satış yok</p>
          } @else {
            <div class="max-h-60 overflow-y-auto -mx-1">
              <table class="w-full text-sm">
                <thead class="text-xs text-gray-400 uppercase">
                  <tr>
                    <th class="text-left font-semibold px-1 pb-2">Kasiyer</th>
                    <th class="text-right font-semibold px-1 pb-2">Satış</th>
                    <th class="text-right font-semibold px-1 pb-2">Ort. Sepet</th>
                    <th class="text-right font-semibold px-1 pb-2">Tutar</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-50">
                  @for (c of cashiers; track c.cashierId) {
                    <tr class="hover:bg-gray-50 cursor-pointer"
                        pTooltip="Listeyi bu kasiyere göre filtrele" tooltipPosition="left"
                        (click)="filterByCashier(c.cashierName)">
                      <td class="px-1 py-2 font-medium text-gray-700 truncate max-w-[140px]">
                        {{ c.cashierName || '—' }}
                      </td>
                      <td class="px-1 py-2 text-right text-gray-600">
                        {{ c.saleCount }}
                        <span class="text-xs text-gray-400">/ {{ c.itemCount }} ürün</span>
                      </td>
                      <td class="px-1 py-2 text-right text-gray-600">
                        {{ c.averageBasket | currency:'TRY':'symbol':'1.0-0' }}
                      </td>
                      <td class="px-1 py-2 text-right font-semibold text-gray-800">
                        {{ c.revenue | currency:'TRY':'symbol':'1.0-0' }}
                        <div class="h-1 bg-gray-100 rounded-full mt-1">
                          <div class="h-1 rounded-full bg-blue-500"
                               [style.width.%]="cashierShare(c.revenue)"></div>
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>

      <!-- ── Filtreler ──────────────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
        <div class="flex items-center gap-4 flex-wrap">
          <div class="relative">
            <i class="pi pi-search absolute left-3 top-1/2 -translate-y-1/2
                       text-gray-400 text-sm"></i>
            <input type="text" [(ngModel)]="searchTerm"
                   (ngModelChange)="applyFilter()"
                   placeholder="Fiş no veya kasiyer ara..."
                   class="pl-9 pr-4 py-2 text-sm border border-gray-200
                          rounded-xl focus:outline-none focus:ring-2
                          focus:ring-red-300 w-64" />
          </div>

          <div class="flex items-center gap-2">
            <span class="text-sm text-gray-500">Tarih:</span>
            <p-calendar
              [(ngModel)]="dateRange"
              selectionMode="range"
              [readonlyInput]="true"
              dateFormat="dd.mm.yy"
              placeholder="Tarih aralığı"
              styleClass="text-sm"
              (onSelect)="onDateSelect()" />
          </div>

          <button (click)="clearFilters()"
            class="text-sm text-gray-500 hover:text-gray-700
                   px-3 py-2 rounded-xl hover:bg-gray-100">
            <i class="pi pi-times mr-1"></i>Temizle
          </button>

          <button (click)="exportCsv()" [disabled]="filteredSales.length === 0"
            class="ml-auto text-sm text-gray-600 hover:text-gray-800 border border-gray-200
                   px-3 py-2 rounded-xl hover:bg-gray-50 disabled:opacity-40">
            <i class="pi pi-download mr-1"></i>CSV İndir
          </button>
        </div>
      </div>

      @if (summary.giftCardSoldTotal) {
        <div class="bg-white rounded-2xl shadow-sm border border-gray-100 px-5 py-3 flex flex-wrap items-center gap-x-6 gap-y-1 text-sm">
          <span class="font-semibold text-gray-700"><i class="pi pi-gift mr-1 text-pink-500"></i>Hediye kartı satışı (emanet)</span>
          <span class="text-gray-800 font-bold">{{ summary.giftCardSoldTotal | currency:'TRY':'symbol':'1.0-0' }}</span>
          <span class="text-gray-500">nakit {{ (summary.giftCardSoldCash ?? 0) | currency:'TRY':'symbol':'1.0-0' }} ·
            kart {{ (summary.giftCardSoldCard ?? 0) | currency:'TRY':'symbol':'1.0-0' }}</span>
          <span class="text-xs text-gray-400">Ciroya dahil değildir; kart harcandığında satış olarak görünür.</span>
        </div>
      }

      @if (totalCount > sales.length) {
        <div class="bg-amber-50 border border-amber-200 rounded-xl px-4 py-2.5 text-sm text-amber-700">
          <i class="pi pi-info-circle mr-1"></i>
          Bu dönemde {{ totalCount }} satış var; listede en yeni {{ sales.length }} tanesi gösteriliyor.
          Özet kartları dönemin tamamını kapsar. Daha kısa bir tarih aralığı seçebilirsiniz.
        </div>
      }

      <!-- ── Tablo ──────────────────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        <p-table
          [value]="filteredSales"
          [loading]="isLoading"
          [paginator]="true"
          [rows]="15"
          styleClass="p-datatable-sm"
          [rowHover]="true">

          <ng-template pTemplate="header">
            <tr>
              <th class="text-xs font-semibold text-gray-500 uppercase">Fiş No</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Tarih</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Kasiyer</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                Ödeme
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                Durum
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-right">
                Tutar
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                Detay
              </th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-sale>
            <tr class="border-b border-gray-50">
              <td>
                <span class="font-mono text-xs bg-gray-50 border border-gray-200
                             px-2.5 py-1 rounded-lg text-gray-700 font-semibold">
                  {{ sale.receiptNumber }}
                </span>
              </td>
              <td class="text-gray-500 text-xs">
                {{ sale.saleDate | utc | date:'dd.MM.yyyy HH:mm' }}
              </td>
              <td class="text-gray-700 text-sm font-medium">
                {{ sale.cashierName || '—' }}
              </td>
              <td class="text-center">
                <span [class]="getPaymentClass(sale.paymentType)"
                      class="text-xs px-2.5 py-1 rounded-full font-medium">
                  {{ getPaymentLabel(sale.paymentType) }}
                </span>
              </td>
              <td class="text-center">
                <span [class]="getSaleStatusClass(sale.status)"
                      class="text-xs px-2.5 py-1 rounded-full font-medium">
                  {{ getSaleStatusLabel(sale.status) }}
                </span>
              </td>
              <td class="text-right font-bold text-gray-800">
                {{ sale.finalAmount | currency:'TRY':'symbol':'1.2-2' }}
              </td>
              <td class="text-center">
                <p-button icon="pi pi-eye" size="small"
                  severity="secondary" [rounded]="true"
                  pTooltip="Detay" tooltipPosition="top"
                  (onClick)="openDetail(sale)" />
              </td>
            </tr>
          </ng-template>

          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="7">
                <div class="text-center py-12 text-gray-300">
                  <i class="pi pi-receipt text-4xl mb-3 block"></i>
                  <p>Satış bulunamadı</p>
                </div>
              </td>
            </tr>
          </ng-template>

        </p-table>
      </div>

    </div>

    <!-- ── Fiş Detayı Dialog ──────────────────────────────── -->
    <p-dialog
      [visible]="detailVisible"
      (visibleChange)="detailVisible = $event"
      [modal]="true" [draggable]="false"
      [header]="'Fiş: ' + (selectedSale?.receiptNumber ?? '')"
      [style]="{ width: '560px' }">

      @if (selectedSale) {
        <div class="space-y-4">

          <!-- Özet -->
          <div class="grid grid-cols-2 gap-3">
            <div class="bg-gray-50 rounded-xl p-3">
              <p class="text-xs text-gray-400">Tarih</p>
              <p class="font-semibold text-gray-800 text-sm">
                {{ selectedSale.saleDate | utc | date:'dd.MM.yyyy HH:mm' }}
              </p>
            </div>
            <div class="bg-gray-50 rounded-xl p-3">
              <p class="text-xs text-gray-400">Ödeme</p>
              <p class="font-semibold text-gray-800 text-sm">
                {{ getPaymentLabel(selectedSale.paymentType) }}
              </p>
            </div>
          </div>

          <!-- Kalemler -->
          <div class="border border-gray-100 rounded-xl overflow-hidden">
            <div class="bg-gray-50 px-4 py-2.5 flex justify-between
                        text-xs font-semibold text-gray-500 uppercase">
              <span>Ürün</span>
              <span>Adet × Fiyat = Toplam</span>
            </div>
            <div class="divide-y divide-gray-50">
              @for (item of selectedSale.items; track $index) {
                <div class="px-4 py-3 flex items-center justify-between">
                  <div>
                    <p class="text-sm font-semibold text-gray-800">
                      {{ item.productName }}
                    </p>
                    <div class="flex items-center gap-1.5 mt-0.5">
                      <span class="font-mono text-xs text-gray-400">
                        {{ item.barcode }}
                      </span>
                      @if (item.color) {
                        <span class="text-xs text-blue-600">{{ item.color }}</span>
                      }
                      @if (item.size) {
                        <span class="text-xs font-bold text-gray-500">
                          {{ item.size }}
                        </span>
                      }
                    </div>
                  </div>
                  <div class="text-right">
                    <p class="text-sm font-bold text-gray-800">
                      {{ item.lineTotal | currency:'TRY':'symbol':'1.2-2' }}
                    </p>
                    <p class="text-xs text-gray-400">
                      {{ item.quantity }} ×
                      {{ item.priceAtSale | currency:'TRY':'symbol':'1.2-2' }}
                    </p>
                  </div>
                </div>
              }
            </div>
          </div>

          <!-- Toplam -->
          <div class="bg-gray-50 rounded-xl p-4 space-y-2">
            @if (selectedSale.discountAmount > 0) {
              <div class="flex justify-between text-sm text-gray-500">
                <span>Ara Toplam</span>
                <span>{{ selectedSale.totalAmount | currency:'TRY':'symbol':'1.2-2' }}</span>
              </div>
              <div class="flex justify-between text-sm text-red-500">
                <span>İndirim</span>
                <span>-{{ selectedSale.discountAmount | currency:'TRY':'symbol':'1.2-2' }}</span>
              </div>
            }
            <div class="flex justify-between font-bold text-gray-800 text-base
                        pt-2 border-t border-gray-200">
              <span>Genel Toplam</span>
              <span class="text-emerald-600">
                {{ selectedSale.finalAmount | currency:'TRY':'symbol':'1.2-2' }}
              </span>
            </div>
          </div>

          @if (selectedSale.hasNegativeStockItem) {
            <div class="bg-red-50 rounded-xl px-4 py-3 flex items-center gap-2">
              <i class="pi pi-exclamation-triangle text-red-500"></i>
              <p class="text-sm text-red-600">
                Bu satışta negatif stoka düşen ürün var.
              </p>
            </div>
          }

        </div>
      }

      <ng-template pTemplate="footer">
        <p-button label="Kapat" severity="secondary"
          (onClick)="detailVisible = false" />
      </ng-template>
    </p-dialog>

    <p-toast position="top-right" />
  `
})
export class StoreSalesComponent implements OnInit {
  private static readonly PAGE_SIZE = 500;

  sales:         any[] = [];
  filteredSales: any[] = [];
  selectedSale:  any   = null;
  isLoading      = false;
  detailVisible  = false;
  searchTerm     = '';
  dateRange:     Date[] = [];
  totalCount     = 0;
  summary: SalesSummary = { ...EMPTY_SUMMARY };
  chartBars: BarDatum[] = [];
  cashiers:  CashierBreakdown[] = [];
  readonly formatMoney = formatMoney;

  private readonly apiUrl = `${environment.apiUrl}/pos/sales`;

  constructor(
    private http:           HttpClient,
    private messageService: MessageService,
    private reportService:  ReportService
  ) {}

  /** Seçili dönem tek bir gün mü (grafik saatlik), yoksa çok gün mü (günlük). */
  get isSingleDay(): boolean {
    const { from, to } = this.period;
    return to.getTime() - from.getTime() <= 86_400_000 + 3_600_000; // DST payı
  }

  private loadBreakdown(): void {
    const { from, to } = this.period;
    const single = this.isSingleDay;
    this.reportService.getSalesBreakdown(from, to).subscribe({
      next: b => {
        this.chartBars = single ? hourlyToBars(b.hourly) : dailyToBars(b.daily, from, to);
        this.cashiers  = b.cashiers;
      },
      error: () => { this.chartBars = []; this.cashiers = []; }
    });
  }

  cashierShare(revenue: number): number {
    const max = Math.max(0, ...this.cashiers.map(c => c.revenue));
    return max > 0 ? Math.max(0, revenue) / max * 100 : 0;
  }

  filterByCashier(name?: string | null): void {
    if (!name) return;
    this.searchTerm = this.searchTerm === name ? '' : name;
    this.applyFilter();
  }

  exportCsv(): void {
    const { from } = this.period;
    const toLocal = (v: string) =>
      new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(v) ? v : v + 'Z').toLocaleString('tr-TR');
    downloadCsv(
      `satislar-${from.toISOString().slice(0, 10)}.csv`,
      ['Fiş No', 'Tarih', 'Kasiyer', 'Ödeme', 'Durum', 'Ürün Adedi', 'Tutar'],
      this.filteredSales.map(s => [
        s.receiptNumber, toLocal(s.saleDate), s.cashierName ?? '',
        this.getPaymentLabel(s.paymentType).replace(/^\S+\s/, ''),
        this.getSaleStatusLabel(s.status).replace(/^\S+\s/, ''),
        s.itemCount, s.finalAmount
      ])
    );
  }

  ngOnInit(): void { this.loadSales(); }

  /** Seçili aralık (yoksa bugün) - yerel gün başı ve bitiş gününü kapsayan son. */
  private get period(): { from: Date; to: Date } {
    const [start, end] = this.dateRange?.length ? this.dateRange : [];
    const from = new Date(start ?? new Date());
    from.setHours(0, 0, 0, 0);
    const to = new Date(end ?? start ?? new Date());
    to.setHours(0, 0, 0, 0);
    to.setDate(to.getDate() + 1); // bitiş günü dahil
    return { from, to };
  }

  get periodLabel(): string {
    const { from, to } = this.period;
    const last = new Date(to.getTime() - 1);
    const fmt = (d: Date) => d.toLocaleDateString('tr-TR');
    if (from.toDateString() === last.toDateString())
      return from.toDateString() === new Date().toDateString() ? 'Bugün' : fmt(from);
    return `${fmt(from)} – ${fmt(last)}`;
  }

  onDateSelect(): void {
    // Aralık seçiminde ilk tıklamada bitiş günü henüz yok - ikinciyi bekle
    if (this.dateRange?.length === 2 && this.dateRange[1]) this.loadSales();
  }

  loadSales(): void {
    const { from, to } = this.period;
    const params = new HttpParams()
      .set('from', from.toISOString())   // UTC an: backend yerel günü doğru keser
      .set('to', to.toISOString())
      .set('pageSize', StoreSalesComponent.PAGE_SIZE);

    this.isLoading = true;
    this.loadBreakdown();
    this.http.get<any>(this.apiUrl, { params }).subscribe({
      next: data => {
        // Backend { totalCount, summary, items } döner. Eskiden olmayan
        // `data.sales` okunuyordu ve liste hiç dolmuyordu.
        this.sales      = data?.items ?? [];
        this.totalCount = data?.totalCount ?? this.sales.length;
        this.summary    = { ...EMPTY_SUMMARY, ...(data?.summary ?? {}) };
        this.applyFilter();
        this.isLoading = false;
      },
      error: (err) => {
        this.isLoading = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Satışlar yüklenemedi.')
        });
      }
    });
  }

  applyFilter(): void {
    const term = this.searchTerm.trim().toLowerCase();
    this.filteredSales = term
      ? this.sales.filter(s =>
          s.receiptNumber?.toLowerCase().includes(term) ||
          s.cashierName?.toLowerCase().includes(term))
      : [...this.sales];
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.dateRange  = [];
    this.loadSales();
  }

  openDetail(sale: any): void {
    this.http.get<any>(
      `${environment.apiUrl}/pos/sales/${encodeURIComponent(sale.receiptNumber)}`
    ).subscribe({
      next: data => {
        this.selectedSale = data;
        this.detailVisible = true;
      },
      error: (err) => this.messageService.add({
        severity: 'error', summary: 'Hata',
        detail: apiErrorMessage(err, 'Fiş detayı yüklenemedi.')
      })
    });
  }

  getPaymentLabel(type: string): string {
    const map: Record<string, string> = {
      Cash: '💵 Nakit', Card: '💳 Kart', Mixed: '🔀 Karma', GiftCard: '🎁 Hediye Kartı',
      ExchangeCredit: '🔁 İade Kredisi'
    };
    return map[type] ?? type;
  }

  getPaymentClass(type: string): string {
    const map: Record<string, string> = {
      Cash:     'bg-green-100 text-green-700',
      Card:     'bg-blue-100 text-blue-700',
      Mixed:    'bg-purple-100 text-purple-700',
      GiftCard: 'bg-pink-100 text-pink-700',
      ExchangeCredit: 'bg-amber-100 text-amber-700'
    };
    return map[type] ?? 'bg-gray-100 text-gray-600';
  }

  getSaleStatusLabel(status: string): string {
    const map: Record<string, string> = {
      Completed: '✅ Tamamlandı',
      Refunded:  '↩️ İade',
      Partial:   '⚠️ Kısmi İade'
    };
    return map[status] ?? status;
  }

  getSaleStatusClass(status: string): string {
    const map: Record<string, string> = {
      Completed: 'bg-emerald-100 text-emerald-700',
      Refunded:  'bg-orange-100 text-orange-700',
      Partial:   'bg-yellow-100 text-yellow-700'
    };
    return map[status] ?? 'bg-gray-100 text-gray-600';
  }
}
