import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { StoreService } from '../../core/services/store.service';
import { StockTransferService } from '../../core/services/stock-transfer.service';
import { AuthService } from '../../core/services/auth.service';
import { UtcPipe } from '../../shared/pipes/utc.pipe';
import { apiErrorMessage } from '../../core/utils/api.util';
import { ReportService, localDayRange } from '../../core/services/report.service';
import { BarChartComponent, BarDatum } from '../../shared/components/bar-chart.component';
import { EMPTY_SALES_SUMMARY, SalesSummary } from '../../shared/models/report.model';
import { formatMoney, hourlyToBars } from '../../shared/utils/report-format';

@Component({
  selector: 'app-store-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, UtcPipe, BarChartComponent],
  template: `
    <div class="space-y-6">

      <!-- ── Başlık ─────────────────────────────────────────── -->
      <div>
        <h2 class="text-2xl font-bold text-gray-800">
          Hoş Geldiniz 👋
        </h2>
        <p class="text-gray-400 text-sm mt-1">
          Mağazanızın günlük özeti
        </p>
      </div>

      @if (!hasStore) {
        <div class="bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-sm text-red-700">
          <i class="pi pi-exclamation-triangle mr-1"></i>
          Hesabınıza bir mağaza atanmamış. Lütfen sistem yöneticisiyle iletişime geçin.
        </div>
      }
      @if (loadError()) {
        <div class="bg-amber-50 border border-amber-200 rounded-xl px-4 py-3 text-sm text-amber-700
                    flex items-center justify-between">
          <span><i class="pi pi-info-circle mr-1"></i>{{ loadError() }}</span>
          <button (click)="reload()" class="font-medium hover:underline">Tekrar dene</button>
        </div>
      }

      <!-- ── Bugünkü Satış ──────────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
        <div class="flex items-center justify-between mb-4">
          <h3 class="font-bold text-gray-800 flex items-center gap-2">
            <i class="pi pi-chart-line text-blue-500"></i>
            Bugünkü Satış
          </h3>
          <a routerLink="/store/sales"
             class="text-xs text-blue-500 hover:text-blue-600 font-medium">
            Satış Raporu →
          </a>
        </div>

        <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <div>
            <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">Net Ciro</p>
            <p class="text-2xl font-bold text-gray-800 mt-1">
              {{ today().netRevenue | currency:'TRY':'symbol':'1.0-0' }}
            </p>
            @if (today().refundTotal > 0) {
              <p class="text-xs text-gray-400 mt-0.5">
                İade −{{ today().refundTotal | currency:'TRY':'symbol':'1.0-0' }}
              </p>
            }
          </div>
          <div>
            <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">Satış Adedi</p>
            <p class="text-2xl font-bold text-gray-800 mt-1">{{ today().totalSales }}</p>
            @if (today().totalSales > 0) {
              <p class="text-xs text-gray-400 mt-0.5">
                Ort. sepet {{ (today().totalRevenue / today().totalSales) | currency:'TRY':'symbol':'1.0-0' }}
              </p>
            }
          </div>
          <div>
            <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">Satılan Ürün</p>
            <p class="text-2xl font-bold text-gray-800 mt-1">{{ today().totalItems }}</p>
          </div>
          <div>
            <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">İade İşlemi</p>
            <p class="text-2xl font-bold text-gray-800 mt-1">{{ today().refundCount }}</p>
          </div>
        </div>

        <div class="mt-5">
          <p class="text-xs text-gray-400 mb-1">Saatlik satış tutarı</p>
          <app-bar-chart [data]="hourlyBars()" [height]="120"
                         ariaLabel="Bugünün saatlik satış tutarı"
                         emptyText="Bugün henüz satış yok"
                         [formatter]="formatMoney" />
        </div>
      </div>

      <!-- ── Özet Kartlar ───────────────────────────────────── -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">

        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                Toplam Ürün
              </p>
              <p class="text-3xl font-bold text-gray-800 mt-1">
                {{ inventorySummary().totalProducts }}
              </p>
            </div>
            <div class="w-12 h-12 bg-blue-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-box text-blue-500 text-xl"></i>
            </div>
          </div>
        </div>

        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                Kritik Stok
              </p>
              <p class="text-3xl font-bold mt-1"
                 [class]="inventorySummary().criticalCount > 0
                   ? 'text-red-500' : 'text-gray-800'">
                {{ inventorySummary().criticalCount }}
              </p>
            </div>
            <div class="w-12 h-12 bg-red-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-exclamation-triangle text-red-500 text-xl"></i>
            </div>
          </div>
        </div>

        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                Bekleyen Sevkiyat
              </p>
              <p class="text-3xl font-bold mt-1"
                 [class]="pendingTransfers() > 0
                   ? 'text-blue-600' : 'text-gray-800'">
                {{ pendingTransfers() }}
              </p>
            </div>
            <div class="w-12 h-12 bg-blue-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-truck text-blue-500 text-xl"></i>
            </div>
          </div>
        </div>

        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                Tükenen Ürün
              </p>
              <p class="text-3xl font-bold mt-1"
                 [class]="inventorySummary().outOfStockCount > 0
                   ? 'text-orange-500' : 'text-gray-800'">
                {{ inventorySummary().outOfStockCount }}
              </p>
            </div>
            <div class="w-12 h-12 bg-orange-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-ban text-orange-500 text-xl"></i>
            </div>
          </div>
        </div>

      </div>

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">

        <!-- ── Stok Uyarıları ─────────────────────────────── -->
        <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
          <div class="flex items-center justify-between mb-4">
            <h3 class="font-bold text-gray-800 flex items-center gap-2">
              <i class="pi pi-exclamation-circle text-red-500"></i>
              Stok Uyarıları
            </h3>
            <a routerLink="/store/inventory"
               class="text-xs text-red-500 hover:text-red-600 font-medium">
              Tümünü Gör →
            </a>
          </div>

          @if (stockAlerts().length === 0) {
            <div class="text-center py-8 text-gray-300">
              <i class="pi pi-check-circle text-4xl mb-2 block text-emerald-400"></i>
              <p class="text-sm text-emerald-500 font-medium">
                Tüm stoklar normal
              </p>
            </div>
          } @else {
            <div class="space-y-2 max-h-64 overflow-y-auto">
              @for (alert of stockAlerts(); track alert.variantId) {
                <div class="flex items-center gap-3 p-3 rounded-xl"
                     [class]="alert.quantity < 0
                       ? 'bg-red-50'
                       : alert.quantity === 0
                         ? 'bg-orange-50'
                         : 'bg-yellow-50'">
                  <span class="text-lg">
                    {{ alert.quantity < 0 ? '🔴' : alert.quantity === 0 ? '⛔' : '🟡' }}
                  </span>
                  <div class="flex-1 min-w-0">
                    <p class="text-sm font-semibold text-gray-800 truncate">
                      {{ alert.productName }}
                    </p>
                    <div class="flex items-center gap-1.5 mt-0.5">
                      @if (alert.color) {
                        <span class="text-xs text-gray-500">{{ alert.color }}</span>
                      }
                      @if (alert.size) {
                        <span class="text-xs font-bold text-gray-500">
                          {{ alert.size }}
                        </span>
                      }
                    </div>
                  </div>
                  <span class="font-bold text-sm"
                        [class]="alert.quantity < 0 ? 'text-red-600'
                          : alert.quantity === 0 ? 'text-orange-500'
                          : 'text-yellow-600'">
                    {{ alert.quantity }}
                  </span>
                </div>
              }
            </div>
          }
        </div>

        <!-- ── Bekleyen Sevkiyatlar ───────────────────────── -->
        <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
          <div class="flex items-center justify-between mb-4">
            <h3 class="font-bold text-gray-800 flex items-center gap-2">
              <i class="pi pi-truck text-blue-500"></i>
              Teslim Alınacak Sevkiyatlar
            </h3>
            <a routerLink="/store/transfers"
               class="text-xs text-blue-500 hover:text-blue-600 font-medium">
              Tümünü Gör →
            </a>
          </div>

          @if (shippedTransfers().length === 0) {
            <div class="text-center py-8 text-gray-300">
              <i class="pi pi-truck text-4xl mb-2 block"></i>
              <p class="text-sm">Bekleyen sevkiyat yok</p>
            </div>
          } @else {
            <div class="space-y-2">
              @for (t of shippedTransfers(); track t.id) {
                <div class="flex items-center gap-3 p-3 bg-blue-50 rounded-xl">
                  <i class="pi pi-truck text-blue-500"></i>
                  <div class="flex-1">
                    <p class="text-sm font-semibold text-gray-800">
                      {{ t.transferNumber }}
                    </p>
                    <p class="text-xs text-gray-400">
                      {{ t.totalProducts }} ürün · {{ t.totalQuantity }} adet —
                      {{ (t.shippedAt ?? t.createdAt) | utc | date:'dd.MM.yyyy' }}
                    </p>
                  </div>
                  <a routerLink="/store/transfers"
                     class="text-xs bg-blue-500 text-white px-3 py-1
                            rounded-lg font-medium hover:bg-blue-600">
                    Teslim Al
                  </a>
                </div>
              }
            </div>
          }
        </div>

      </div>

      <!-- ── Hızlı Erişim ───────────────────────────────────── -->
      <div class="grid grid-cols-2 md:grid-cols-5 gap-4">
        @for (action of quickActions; track action.label) {
          <a [routerLink]="action.route"
             class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100
                    hover:border-red-200 hover:shadow-md transition-all
                    flex flex-col items-center gap-3 text-center group">
            <div class="w-12 h-12 rounded-xl flex items-center justify-center
                        transition-colors"
                 [class]="action.bgColor">
              <i [class]="'pi ' + action.icon + ' text-xl ' + action.iconColor"></i>
            </div>
            <span class="text-sm font-semibold text-gray-700
                         group-hover:text-red-600 transition-colors">
              {{ action.label }}
            </span>
          </a>
        }
      </div>

    </div>
  `
})
export class StoreDashboardComponent implements OnInit {
  inventorySummary = signal({
    totalProducts:  0,
    criticalCount:  0,
    outOfStockCount: 0,
    lowStockCount:  0
  });
  stockAlerts      = signal<any[]>([]);
  shippedTransfers = signal<any[]>([]);
  pendingTransfers = signal(0);
  loadError        = signal<string | null>(null);
  hasStore         = true;
  today            = signal<SalesSummary>({ ...EMPTY_SALES_SUMMARY });
  hourlyBars       = signal<BarDatum[]>([]);
  readonly formatMoney = formatMoney;

  quickActions = [
    { label: 'Stok Durumu',   icon: 'pi-chart-bar', route: '/store/inventory',
      bgColor: 'bg-blue-50',    iconColor: 'text-blue-500' },
    { label: 'Sevkiyat Talep', icon: 'pi-truck',    route: '/store/transfers',
      bgColor: 'bg-purple-50',  iconColor: 'text-purple-500' },
    { label: 'Satışlar',      icon: 'pi-receipt',   route: '/store/sales',
      bgColor: 'bg-emerald-50', iconColor: 'text-emerald-500' },
    { label: 'X / Z Raporları', icon: 'pi-file',    route: '/store/reports',
      bgColor: 'bg-gray-100',   iconColor: 'text-gray-600' },
    { label: 'Kasiyerler',    icon: 'pi-users',     route: '/store/cashiers',
      bgColor: 'bg-orange-50',  iconColor: 'text-orange-500' },
  ];

  constructor(
    private storeService:    StoreService,
    private transferService: StockTransferService,
    private authService:     AuthService,
    private reportService:   ReportService
  ) {}

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loadError.set(null);
    this.hasStore = !!this.authService.storeId();
    if (!this.hasStore) return;
    this.loadInventorySummary();
    this.loadTransfers();
    this.loadTodaySales();
  }

  loadTodaySales(): void {
    const { from, to } = localDayRange();
    this.reportService.getSalesSummary(from, to).subscribe({
      next: s => this.today.set(s),
      error: err => this.showError(err, 'Bugünkü satış özeti yüklenemedi.')
    });
    this.reportService.getSalesBreakdown(from, to).subscribe({
      next: b => this.hourlyBars.set(hourlyToBars(b.hourly)),
      error: () => this.hourlyBars.set([])
    });
  }

  private showError(err: unknown, fallback: string): void {
    this.loadError.set(apiErrorMessage(err, fallback));
  }

  loadInventorySummary(): void {
    const storeId = this.authService.storeId();
    if (!storeId) return;

    this.storeService.getInventory(+storeId).subscribe({
      next: data => {
        this.inventorySummary.set({
          totalProducts:   data.totalProducts,
          criticalCount:   data.criticalCount,
          outOfStockCount: data.outOfStockCount,
          lowStockCount:   data.lowStockCount
        });
        // Sadece uyarılı olanları göster (max 5) - en kötü durum önce
        const priority: Record<string, number> = { Critical: 3, OutOfStock: 2, Low: 1, Normal: 0 };
        this.stockAlerts.set(
          data.items
            .filter(i => i.alertLevel !== 'Normal')
            .sort((a, b) => (priority[b.alertLevel] ?? 0) - (priority[a.alertLevel] ?? 0))
            .slice(0, 5)
        );
      },
      error: err => this.showError(err, 'Stok özeti yüklenemedi.')
    });
  }

  loadTransfers(): void {
    this.transferService.getAll().subscribe({
      next: transfers => {
        const shipped = transfers.filter(t => t.status === 'Shipped');
        this.shippedTransfers.set(shipped.slice(0, 3));
        this.pendingTransfers.set(shipped.length);
      },
      error: err => this.showError(err, 'Sevkiyatlar yüklenemedi.')
    });
  }
}