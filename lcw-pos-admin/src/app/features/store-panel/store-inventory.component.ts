import { Component, OnInit, signal, computed } from '@angular/core';
import { assetUrl, apiErrorMessage } from '../../core/utils/api.util';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { StoreService } from '../../core/services/store.service';
import { AuthService } from '../../core/services/auth.service';
import { environment } from '../../environments/environment';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageService } from 'primeng/api';

interface InventoryItem {
  variantId:      number;
  productId:      number;
  productName:    string;
  barcode:        string;
  color?:         string;
  size?:          string;
  imagePath?:     string;
  quantity:       number;
  minStockAlert:  number;
  alertLevel:     string;
  alertLevelText: string;
  currentPrice:   number;
}

interface ProductGroup {
  productId:      number;
  productName:    string;
  imagePath?:     string;
  totalQuantity:  number;
  variantCount:   number;
  worstAlert:     string;
  worstAlertText: string;
  variants:       InventoryItem[];
  expanded:       boolean;
}

@Component({
  selector: 'app-store-inventory',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    TableModule, TagModule, ToastModule,
    ButtonModule, TooltipModule, DialogModule, InputNumberModule
  ],
  providers: [MessageService],
  template: `
    <div class="space-y-6">

      <!-- ── Başlık ─────────────────────────────────────────── -->
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-2xl font-bold text-gray-800">Stok Durumu</h2>
          <p class="text-gray-400 text-sm mt-1">
            Mağazanızdaki güncel stok bilgileri
          </p>
        </div>

        <!-- Görünüm Switcher -->
        <div class="bg-white rounded-xl border border-gray-200 p-1 flex gap-1">
          <button
            (click)="viewMode = 'product'"
            [class]="viewMode === 'product'
              ? 'bg-red-500 text-white'
              : 'text-gray-500 hover:bg-gray-100'"
            class="px-4 py-2 rounded-lg text-sm font-medium transition-colors
                   flex items-center gap-2">
            <i class="pi pi-box"></i>
            Ürün Bazlı
          </button>
          <button
            (click)="viewMode = 'variant'"
            [class]="viewMode === 'variant'
              ? 'bg-red-500 text-white'
              : 'text-gray-500 hover:bg-gray-100'"
            class="px-4 py-2 rounded-lg text-sm font-medium transition-colors
                   flex items-center gap-2">
            <i class="pi pi-tags"></i>
            Varyant Bazlı
          </button>
        </div>
      </div>

      <!-- ── Özet Kartlar ───────────────────────────────────── -->
      <div class="grid grid-cols-4 gap-4">
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">
            Toplam Ürün
          </p>
          <p class="text-3xl font-bold text-gray-800 mt-1">
            {{ summary().totalProducts }}
          </p>
        </div>
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">
            🔴 Negatif
          </p>
          <p class="text-3xl font-bold text-red-500 mt-1">
            {{ summary().criticalCount }}
          </p>
        </div>
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">
            ⛔ Tükenen
          </p>
          <p class="text-3xl font-bold text-orange-500 mt-1">
            {{ summary().outOfStockCount }}
          </p>
        </div>
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">
            🟡 Düşük
          </p>
          <p class="text-3xl font-bold text-yellow-500 mt-1">
            {{ summary().lowStockCount }}
          </p>
        </div>
      </div>

      <!-- ── Filtreler ──────────────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
        <div class="flex items-center gap-3 flex-wrap">
          <span class="text-sm font-semibold text-gray-600">Filtrele:</span>
          @for (f of filters; track f.value) {
            <button
              (click)="activeFilter = f.value; applyFilter()"
              [class]="activeFilter === f.value
                ? 'bg-red-500 text-white'
                : 'bg-gray-100 text-gray-600 hover:bg-gray-200'"
              class="px-4 py-1.5 rounded-xl text-sm font-medium transition-colors">
              {{ f.label }}
            </button>
          }
          <div class="ml-auto relative">
            <i class="pi pi-search absolute left-3 top-1/2 -translate-y-1/2
                       text-gray-400 text-sm"></i>
            <input type="text" [(ngModel)]="searchTerm"
                   (input)="applyFilter()"
                   placeholder="Ürün veya barkod ara..."
                   class="pl-9 pr-4 py-2 text-sm border border-gray-200
                          rounded-xl focus:outline-none focus:ring-2
                          focus:ring-red-300 w-64" />
          </div>
        </div>
      </div>

      <!-- ── ÜRÜN BAZLI GÖRÜNÜM ─────────────────────────────── -->
      @if (viewMode === 'product') {
        <div class="bg-white rounded-2xl shadow-sm border border-gray-100
                    overflow-hidden">

          @if (isLoading) {
            <div class="flex items-center justify-center py-16 text-gray-300">
              <i class="pi pi-spin pi-spinner text-3xl mr-3"></i>
              Yükleniyor...
            </div>
          } @else if (filteredGroups().length === 0) {
            <div class="text-center py-16 text-gray-300">
              <i class="pi pi-box text-5xl mb-4 block"></i>
              <p>Stok bilgisi bulunamadı</p>
            </div>
          } @else {
            <div class="divide-y divide-gray-50">
              @for (group of filteredGroups(); track group.productId) {
                <!-- Ürün Satırı -->
                <div>
                  <div class="flex items-center gap-4 px-5 py-4 hover:bg-gray-50
                              cursor-pointer transition-colors"
                       (click)="toggleGroup(group.productId)">

                    <!-- Expand Icon -->
                    <i [class]="group.expanded
                       ? 'pi pi-chevron-down text-gray-400'
                       : 'pi pi-chevron-right text-gray-400'"
                       class="text-sm w-4 flex-shrink-0"></i>

                    <!-- Görsel -->
                    @if (group.imagePath) {
                      <img [src]="getImageUrl(group.imagePath)"
     style="width: 48px; height: 48px; object-fit: cover; flex-shrink: 0;"
     class="rounded-xl border border-gray-200"
     (error)="onImageError($event)" />
                    } @else {
                      <div style="width: 48px; height: 48px; flex-shrink: 0;"
     class="bg-gray-100 rounded-xl flex items-center justify-center">
  <i class="pi pi-image text-gray-300"></i>
</div>
                    }

                    <!-- Ürün Adı -->
                    <div class="flex-1 min-w-0">
                      <p class="font-bold text-gray-800 text-sm">
                        {{ group.productName }}
                      </p>
                      <p class="text-xs text-gray-400 mt-0.5">
                        {{ group.variantCount }} varyant
                      </p>
                    </div>

                    <!-- Toplam Stok -->
                    <div class="text-center w-24">
                      <p class="text-xs text-gray-400 mb-0.5">Toplam Stok</p>
                      <p class="font-bold text-lg"
                         [class.text-red-600]="group.totalQuantity < 0"
                         [class.text-orange-500]="group.totalQuantity === 0"
                         [class.text-gray-800]="group.totalQuantity > 0">
                        {{ group.totalQuantity }}
                      </p>
                    </div>

                    <!-- Durum -->
                    <div class="w-32 text-right">
                      <span class="text-sm">{{ group.worstAlertText }}</span>
                    </div>

                  </div>

                  <!-- Varyant Detayları (Expandable) -->
                  @if (group.expanded) {
                    <div class="bg-gray-50 px-5 pb-4">
                      <div class="rounded-xl overflow-hidden border border-gray-200">
                        <table class="w-full text-sm">
                          <thead class="bg-gray-100">
                            <tr>
                              <th class="px-4 py-2 text-left text-xs font-semibold
                                         text-gray-500 uppercase">Barkod</th>
                              <th class="px-4 py-2 text-left text-xs font-semibold
                                         text-gray-500 uppercase">Renk</th>
                              <th class="px-4 py-2 text-left text-xs font-semibold
                                         text-gray-500 uppercase">Beden</th>
                              <th class="px-4 py-2 text-center text-xs font-semibold
                                         text-gray-500 uppercase">Stok</th>
                              <th class="px-4 py-2 text-center text-xs font-semibold
                                         text-gray-500 uppercase">Min.</th>
                              <th class="px-4 py-2 text-center text-xs font-semibold
                                         text-gray-500 uppercase">Durum</th>
                              <th class="px-4 py-2 text-right text-xs font-semibold
                                         text-gray-500 uppercase">Fiyat</th>
                            </tr>
                          </thead>
                          <tbody class="divide-y divide-gray-100 bg-white">
                            @for (v of group.variants; track v.variantId) {
                              <tr [class.bg-red-50]="v.quantity < 0"
                                  [class.bg-orange-50]="v.quantity === 0">
                                <td class="px-4 py-2">
                                  <span class="font-mono text-xs text-gray-500
                                               bg-gray-50 border border-gray-200
                                               px-2 py-0.5 rounded">
                                    {{ v.barcode }}
                                  </span>
                                </td>
                                <td class="px-4 py-2">
                                  @if (v.color) {
                                    <span class="text-xs bg-blue-100 text-blue-700
                                                 px-2 py-0.5 rounded-full">
                                      {{ v.color }}
                                    </span>
                                  }
                                </td>
                                <td class="px-4 py-2">
                                  @if (v.size) {
                                    <span class="text-xs bg-gray-100 text-gray-700
                                                 px-2 py-0.5 rounded-full font-bold">
                                      {{ v.size }}
                                    </span>
                                  }
                                </td>
                                <td class="px-4 py-2 text-center font-bold"
                                    [class.text-red-600]="v.quantity < 0"
                                    [class.text-orange-500]="v.quantity === 0"
                                    [class.text-gray-800]="v.quantity > 0">
                                  {{ v.quantity }}
                                </td>
                                <td class="px-4 py-2 text-center">
                                  <button (click)="openMinStock(v)"
                                          class="text-sm text-gray-600 hover:text-blue-600 inline-flex items-center gap-1"
                                          pTooltip="Minimum stoğu düzenle" tooltipPosition="top">
                                    {{ v.minStockAlert }} <i class="pi pi-pencil text-xs"></i>
                                  </button>
                                </td>
                                <td class="px-4 py-2 text-center text-sm">
                                  {{ v.alertLevelText }}
                                </td>
                                <td class="px-4 py-2 text-right font-semibold
                                           text-gray-700 text-sm">
                                  {{ v.currentPrice | currency:'TRY':'symbol':'1.2-2' }}
                                </td>
                              </tr>
                            }
                          </tbody>
                        </table>
                      </div>
                    </div>
                  }
                </div>
              }
            </div>
          }
        </div>
      }

      <!-- ── VARİYANT BAZLI GÖRÜNÜM ─────────────────────────── -->
      @if (viewMode === 'variant') {
        <div class="bg-white rounded-2xl shadow-sm border border-gray-100
                    overflow-hidden">
          <p-table
            [value]="filteredItems()"
            [loading]="isLoading"
            [paginator]="true"
            [rows]="20"
            styleClass="p-datatable-sm"
            [rowHover]="true">

            <ng-template pTemplate="header">
              <tr>
                <th class="text-xs font-semibold text-gray-500 uppercase">
                  Barkod
                </th>
                <th class="text-xs font-semibold text-gray-500 uppercase">
                  Ürün
                </th>
                <th class="text-xs font-semibold text-gray-500 uppercase">
                  Renk
                </th>
                <th class="text-xs font-semibold text-gray-500 uppercase">
                  Beden
                </th>
                <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                  Stok
                </th>
                <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                  Min.
                </th>
                <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                  Durum
                </th>
                <th class="text-xs font-semibold text-gray-500 uppercase text-right">
                  Fiyat
                </th>
              </tr>
            </ng-template>

            <ng-template pTemplate="body" let-item>
              <tr [class.bg-red-50]="item.quantity < 0"
                  [class.bg-orange-50]="item.quantity === 0"
                  [class.bg-yellow-50]="item.quantity > 0
                    && item.alertLevel === 'Low'">
                <td>
                  <span class="font-mono text-xs text-gray-500 bg-gray-50
                               border border-gray-200 px-2 py-0.5 rounded">
                    {{ item.barcode }}
                  </span>
                </td>
                <td class="font-semibold text-gray-800 text-sm">
                  {{ item.productName }}
                </td>
                <td>
                  @if (item.color) {
                    <span class="text-xs bg-blue-100 text-blue-700
                                 px-2 py-0.5 rounded-full">
                      {{ item.color }}
                    </span>
                  }
                </td>
                <td>
                  @if (item.size) {
                    <span class="text-xs bg-gray-100 text-gray-700
                                 px-2 py-0.5 rounded-full font-bold">
                      {{ item.size }}
                    </span>
                  }
                </td>
                <td class="text-center font-bold text-lg"
                    [class.text-red-600]="item.quantity < 0"
                    [class.text-orange-500]="item.quantity === 0"
                    [class.text-gray-800]="item.quantity > 0">
                  {{ item.quantity }}
                </td>
                <td class="text-center">
                  <button (click)="openMinStock(item)"
                          class="text-sm text-gray-600 hover:text-blue-600 inline-flex items-center gap-1"
                          pTooltip="Minimum stoğu düzenle" tooltipPosition="top">
                    {{ item.minStockAlert }} <i class="pi pi-pencil text-xs"></i>
                  </button>
                </td>
                <td class="text-center text-sm">
                  {{ item.alertLevelText }}
                </td>
                <td class="text-right font-semibold text-gray-700 text-sm">
                  {{ item.currentPrice | currency:'TRY':'symbol':'1.2-2' }}
                </td>
              </tr>
            </ng-template>

            <ng-template pTemplate="emptymessage">
              <tr>
                <td colspan="8">
                  <div class="text-center py-12 text-gray-300">
                    <i class="pi pi-box text-4xl mb-3 block"></i>
                    <p>Stok bilgisi bulunamadı</p>
                  </div>
                </td>
              </tr>
            </ng-template>

          </p-table>
        </div>
      }

    </div>

    <!-- ── Minimum Stok Dialog ───────────────────────────── -->
    <p-dialog [visible]="minDialogVisible"
              (visibleChange)="minDialogVisible = $event"
              [modal]="true" [draggable]="false"
              header="Minimum Stok Seviyesi" [style]="{ width: '420px' }">
      @if (minTarget) {
        <div class="space-y-4 pt-1">
          <div>
            <p class="font-semibold text-gray-800 text-sm">{{ minTarget.productName }}</p>
            <p class="text-xs text-gray-400 mt-0.5">
              {{ minTarget.barcode }}
              @if (minTarget.color) { · {{ minTarget.color }} }
              @if (minTarget.size) { · {{ minTarget.size }} }
              · Mevcut stok: <b>{{ minTarget.quantity }}</b>
            </p>
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Bu seviyenin altına düşünce "Düşük Stok" uyarısı verilsin
            </label>
            <p-inputNumber [(ngModel)]="minValue" [min]="0" [max]="10000"
                           [showButtons]="true" buttonLayout="horizontal"
                           incrementButtonIcon="pi pi-plus" decrementButtonIcon="pi pi-minus"
                           inputStyleClass="w-20 text-center font-bold" />
          </div>

          @if (minTargetVariantCount > 1) {
            <label class="flex items-center gap-2 text-sm text-gray-600 cursor-pointer">
              <input type="checkbox" [(ngModel)]="minApplyAll" />
              Bu ürünün tüm varyantlarına uygula ({{ minTargetVariantCount }} varyant)
            </label>
          }
        </div>
      }
      <ng-template pTemplate="footer">
        <p-button label="İptal" severity="secondary" [outlined]="true"
                  (onClick)="minDialogVisible = false" />
        <p-button label="Kaydet" icon="pi pi-check" [loading]="minSaving"
                  [disabled]="minValue == null || minSaving" (onClick)="saveMinStock()" />
      </ng-template>
    </p-dialog>

    <p-toast position="top-right" />
  `
})
export class StoreInventoryComponent implements OnInit {
  // ✅ Signal'a çevrildi
  allItems      = signal<InventoryItem[]>([]);
  filteredItems = signal<InventoryItem[]>([]);
  expandedIds   = signal<Set<number>>(new Set()); // hangi ürünler açık

  isLoading    = false;
  viewMode:    'product' | 'variant' = 'product';
  activeFilter = 'all';
  searchTerm   = '';

  summary = signal({
    totalProducts: 0, criticalCount: 0,
    outOfStockCount: 0, lowStockCount: 0
  });

  filters = [
    { label: 'Tümü',           value: 'all'       },
    { label: '🔴 Negatif',     value: 'Critical'  },
    { label: '⛔ Tükendi',     value: 'OutOfStock' },
    { label: '🟡 Düşük Stok', value: 'Low'       },
    { label: '🟢 Normal',      value: 'Normal'    },
  ];
onImageError(event: Event): void {
  (event.target as HTMLImageElement).style.display = 'none';
}
  // ✅ computed → filteredItems() signal'ından okur
  readonly filteredGroups = computed(() => {
    const items    = this.filteredItems(); // ✅ signal çağrısı
    const expanded = this.expandedIds();

    const groupMap = new Map<number, ProductGroup>();

    for (const item of items) {
      if (!groupMap.has(item.productId)) {
        groupMap.set(item.productId, {
          productId:      item.productId,
          productName:    item.productName,
          imagePath:      item.imagePath,
          totalQuantity:  0,
          variantCount:   0,
          worstAlert:     'Normal',
          worstAlertText: '🟢 Normal',
          variants:       [],
          expanded:       expanded.has(item.productId) // ✅ signal'dan oku
        });
      }

      const group = groupMap.get(item.productId)!;
      group.totalQuantity += item.quantity;
      group.variantCount  += 1;
      group.variants.push(item);

      const alertPriority: Record<string, number> = {
        Critical: 4, OutOfStock: 3, Low: 2, Normal: 1
      };
      if ((alertPriority[item.alertLevel] ?? 0) >
          (alertPriority[group.worstAlert] ?? 0)) {
        group.worstAlert     = item.alertLevel;
        group.worstAlertText = item.alertLevelText;
      }
    }

    // En kötü durumdaki ürünler üstte
    const rank: Record<string, number> = { Critical: 4, OutOfStock: 3, Low: 2, Normal: 1 };
    return Array.from(groupMap.values())
      .sort((a, b) => (rank[b.worstAlert] ?? 0) - (rank[a.worstAlert] ?? 0)
                   || a.productName.localeCompare(b.productName, 'tr'));
  });

  constructor(
    private storeService:   StoreService,
    private authService:    AuthService,
    private messageService: MessageService
  ) {}

  ngOnInit(): void { this.loadInventory(); }

  loadInventory(): void {
    const storeId = this.authService.storeId();
    if (!storeId) {
      this.messageService.add({
        severity: 'warn', summary: 'Mağaza yok',
        detail: 'Hesabınıza mağaza atanmamış.'
      });
      return;
    }
    this.isLoading = true;

    this.storeService.getInventory(+storeId).subscribe({
      next: (data: any) => {
        this.allItems.set(data.items ?? []);
        this.summary.set({
          totalProducts:   data.totalProducts,
          criticalCount:   data.criticalCount,
          outOfStockCount: data.outOfStockCount,
          lowStockCount:   data.lowStockCount
        });
        this.applyFilter();
        this.isLoading = false;
      },
      error: (err) => {
        this.isLoading = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Stok bilgileri yüklenemedi.')
        });
      }
    });
  }

  applyFilter(): void {
    let items = [...this.allItems()]; // ✅ signal'dan oku

    if (this.activeFilter !== 'all')
      items = items.filter(i => i.alertLevel === this.activeFilter);

    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      items = items.filter(i =>
        i.productName?.toLowerCase().includes(term) ||
        i.barcode?.toLowerCase().includes(term) ||
        i.color?.toLowerCase().includes(term) ||
        i.size?.toLowerCase().includes(term)
      );
    }

    this.filteredItems.set(items); // ✅ signal'a yaz
  }

  // ✅ expand/collapse — signal günceller, computed tetiklenir
  toggleGroup(productId: number): void {
    const current = new Set(this.expandedIds());
    if (current.has(productId)) current.delete(productId);
    else current.add(productId);
    this.expandedIds.set(current);
  }

  // ── Minimum stok düzenleme ──────────────────────────────────
  minDialogVisible = false;
  minTarget:  InventoryItem | null = null;
  minValue:   number | null = null;
  minApplyAll = false;
  minSaving   = false;

  get minTargetVariantCount(): number {
    const t = this.minTarget;
    return t ? this.allItems().filter(i => i.productId === t.productId).length : 0;
  }

  openMinStock(item: InventoryItem): void {
    this.minTarget   = item;
    this.minValue    = item.minStockAlert;
    this.minApplyAll = false;
    this.minDialogVisible = true;
  }

  saveMinStock(): void {
    const target  = this.minTarget;
    const storeId = this.authService.storeId();
    if (!target || storeId == null || this.minValue == null) return;

    const value = Math.max(0, Math.round(this.minValue));
    const variants = this.minApplyAll
      ? this.allItems().filter(i => i.productId === target.productId)
      : [target];

    this.minSaving = true;
    this.storeService.updateMinStock(+storeId,
      variants.map(v => ({ variantId: v.variantId, minStockAlert: value }))
    ).subscribe({
      next: res => {
        this.minSaving = false;
        this.minDialogVisible = false;
        this.messageService.add({
          severity: 'success', summary: 'Güncellendi', detail: res.message
        });
        // Uyarı seviyeleri backend'de yeniden hesaplanır - listeyi tazele
        this.loadInventory();
      },
      error: err => {
        this.minSaving = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Minimum stok güncellenemedi.')
        });
      }
    });
  }

  getImageUrl(path?: string | null): string {
    return assetUrl(path);
  }
}