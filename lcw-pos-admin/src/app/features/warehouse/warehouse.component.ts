import { Component, OnInit } from '@angular/core';
import { apiErrorMessage } from '../../core/utils/api.util';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { WarehouseService } from '../../core/services/warehouse.service';
import { WarehouseStock, ProductForReceiving } from '../../shared/models/warehouse.model';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

interface ReceiveItem extends ProductForReceiving {
  enteredQuantity: number;
  selected:        boolean;
}

@Component({
  selector: 'app-warehouse',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    TableModule, ButtonModule, DialogModule,
    InputNumberModule, InputTextModule,
    TagModule, ToastModule, TooltipModule
  ],
  providers: [MessageService],
  template: `
    <div class="space-y-6">

      <!-- ── Başlık ─────────────────────────────────────────── -->
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-2xl font-bold text-gray-800">Depo Yönetimi</h2>
          <p class="text-gray-400 text-sm mt-1">
            Merkezi depo stok durumu ve mal girişi
          </p>
        </div>
        <button
          (click)="openReceiveDialog()"
          class="inline-flex items-center gap-2 bg-red-500 hover:bg-red-600
                 text-white px-5 py-2.5 rounded-xl transition-colors
                 font-medium text-sm shadow-sm shadow-red-200">
          <i class="pi pi-download"></i>
          Mal Girişi Yap
        </button>
      </div>

      <!-- ── İstatistik Kartları ────────────────────────────── -->
      <div class="grid grid-cols-3 gap-4">

        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                Varyant Çeşidi
              </p>
              <p class="text-3xl font-bold text-gray-800 mt-1">
                {{ stocks.length }}
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
                Toplam Stok
              </p>
              <p class="text-3xl font-bold text-emerald-600 mt-1">
                {{ totalStock }}
              </p>
            </div>
            <div class="w-12 h-12 bg-emerald-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-warehouse text-emerald-500 text-xl"></i>
            </div>
          </div>
        </div>

        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                Rezerve
              </p>
              <p class="text-3xl font-bold text-orange-500 mt-1">
                {{ totalReserved }}
              </p>
            </div>
            <div class="w-12 h-12 bg-orange-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-lock text-orange-500 text-xl"></i>
            </div>
          </div>
        </div>

      </div>

      <!-- ── Stok Tablosu ───────────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">

        <div class="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
          <span class="text-sm font-semibold text-gray-600">Depo Stok Listesi</span>
          <div class="relative">
            <i class="pi pi-search absolute left-3 top-1/2 -translate-y-1/2
                       text-gray-400 text-sm"></i>
            <input
              type="text"
              [(ngModel)]="searchTerm"
              placeholder="Ürün veya barkod ara..."
              class="pl-9 pr-4 py-2 text-sm border border-gray-200 rounded-xl
                     focus:outline-none focus:ring-2 focus:ring-red-300 w-64" />
          </div>
        </div>

        <p-table
          [value]="filteredStocks"
          [loading]="isLoading"
          [paginator]="true"
          [rows]="15"
          styleClass="p-datatable-sm"
          [rowHover]="true">

          <ng-template pTemplate="header">
            <tr>
              <th class="text-xs font-semibold text-gray-500 uppercase">Barkod</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Ürün Adı</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Renk / Beden</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                Toplam Stok
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                Rezerve
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                Kullanılabilir
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                Durum
              </th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-stock>
            <tr class="border-b border-gray-50">

              <td>
                <span class="font-mono text-xs bg-gray-50 border border-gray-200
                             px-2.5 py-1 rounded-lg text-gray-600">
                  {{ stock.barcode }}
                </span>
              </td>

              <td class="font-semibold text-gray-800 text-sm">
                {{ stock.productName }}
              </td>

              <!-- ✅ Renk / Beden -->
              <td>
                <div class="flex items-center gap-1.5">
                  @if (stock.color) {
                    <span class="text-xs bg-blue-50 text-blue-600
                                 px-2 py-0.5 rounded-full font-medium">
                      {{ stock.color }}
                    </span>
                  }
                  @if (stock.size) {
                    <span class="text-xs bg-gray-100 text-gray-700
                                 px-2 py-0.5 rounded-full font-semibold">
                      {{ stock.size }}
                    </span>
                  }
                  @if (!stock.color && !stock.size) {
                    <span class="text-gray-300 text-xs">—</span>
                  }
                </div>
              </td>

              <td class="text-center">
                <span class="font-bold text-gray-800">{{ stock.quantity }}</span>
              </td>

              <td class="text-center">
                @if (stock.reservedQuantity > 0) {
                  <span class="text-orange-500 font-semibold">
                    {{ stock.reservedQuantity }}
                  </span>
                } @else {
                  <span class="text-gray-300">0</span>
                }
              </td>

              <td class="text-center">
                <span [class]="stock.availableQuantity > 0
                  ? 'font-bold text-emerald-600'
                  : 'font-bold text-red-500'">
                  {{ stock.availableQuantity }}
                </span>
              </td>

              <td class="text-center">
                @if (stock.availableQuantity <= 0) {
                  <span class="text-xs bg-red-50 text-red-600 px-2.5 py-1
                               rounded-full font-medium">⛔ Tükendi</span>
                } @else if (stock.availableQuantity <= 10) {
                  <span class="text-xs bg-orange-50 text-orange-600 px-2.5 py-1
                               rounded-full font-medium">🟡 Azalıyor</span>
                } @else {
                  <span class="text-xs bg-emerald-50 text-emerald-600 px-2.5 py-1
                               rounded-full font-medium">🟢 Normal</span>
                }
              </td>

            </tr>
          </ng-template>

          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="7">
                <div class="flex flex-col items-center justify-center
                            py-16 text-gray-300">
                  <i class="pi pi-warehouse text-5xl mb-4"></i>
                  <p class="text-base font-medium">Depoda stok bulunmuyor</p>
                  <p class="text-sm mt-1">Mal girişi yaparak stok ekleyin</p>
                </div>
              </td>
            </tr>
          </ng-template>

        </p-table>
      </div>

    </div>

    <!-- ── Mal Girişi Dialog ──────────────────────────────── -->
    <p-dialog
      [visible]="receiveDialogVisible"
      (visibleChange)="receiveDialogVisible = $event"
      [modal]="true"
      [closable]="!isSubmitting"
      [draggable]="false"
      header="Mal Girişi Yap"
      [style]="{ width: '720px' }">

      @if (isLoadingProducts) {
        <div class="flex items-center justify-center py-12 text-gray-300">
          <i class="pi pi-spin pi-spinner text-3xl mr-3"></i>
          <span>Ürünler yükleniyor...</span>
        </div>
      } @else {
        <div class="space-y-4">

          <div class="bg-blue-50 rounded-xl px-4 py-3 flex items-start gap-3">
            <i class="pi pi-info-circle text-blue-500 mt-0.5"></i>
            <p class="text-sm text-blue-700">
              Sadece katalogdaki aktif varyantlara stok girişi yapabilirsiniz.
            </p>
          </div>

          <!-- Varyant Listesi -->
          <div class="border border-gray-100 rounded-xl overflow-hidden">
            <div class="bg-gray-50 px-4 py-2.5 grid grid-cols-12 gap-3
                        text-xs font-semibold text-gray-500 uppercase">
              <div class="col-span-5">Ürün / Varyant</div>
              <div class="col-span-3 text-center">Mevcut Stok</div>
              <div class="col-span-4 text-center">Giriş Miktarı</div>
            </div>

            <div class="divide-y divide-gray-50 max-h-80 overflow-y-auto">
              <!-- ✅ track item.variantId -->
              @for (item of receiveItems; track item.variantId) {
                <div class="px-4 py-3 grid grid-cols-12 gap-3 items-center
                            hover:bg-gray-50 transition-colors"
                     [class.bg-emerald-50]="item.enteredQuantity > 0">

                  <!-- Ürün Bilgi -->
                  <div class="col-span-5">
                    <p class="text-sm font-semibold text-gray-800 truncate">
                      {{ item.productName }}
                    </p>
                    <div class="flex items-center gap-1.5 mt-0.5">
                      <span class="text-xs font-mono text-gray-400">
                        {{ item.barcode }}
                      </span>
                      @if (item.color) {
                        <span class="text-xs bg-blue-100 text-blue-600
                                     px-1.5 py-0.5 rounded-full">
                          {{ item.color }}
                        </span>
                      }
                      @if (item.size) {
                        <span class="text-xs bg-gray-200 text-gray-600
                                     px-1.5 py-0.5 rounded-full font-semibold">
                          {{ item.size }}
                        </span>
                      }
                    </div>
                  </div>

                  <!-- Mevcut Stok -->
                  <div class="col-span-3 text-center">
                    <span class="text-sm font-bold"
                          [class.text-emerald-600]="item.currentWarehouseStock > 0"
                          [class.text-gray-400]="item.currentWarehouseStock === 0">
                      {{ item.currentWarehouseStock }}
                    </span>
                  </div>

                  <!-- Giriş Miktarı -->
                  <div class="col-span-4 flex justify-center">
                    <p-inputNumber
                      [(ngModel)]="item.enteredQuantity"
                      [min]="0"
                      [max]="9999"
                      [showButtons]="true"
                      buttonLayout="horizontal"
                      decrementButtonClass="p-button-secondary p-button-sm"
                      incrementButtonClass="p-button-secondary p-button-sm"
                      incrementButtonIcon="pi pi-plus"
                      decrementButtonIcon="pi pi-minus"
                      inputStyleClass="w-16 text-center text-sm font-bold"
                      styleClass="w-full" />
                  </div>

                </div>
              }
            </div>
          </div>

          @if (selectedItemCount > 0) {
            <div class="bg-emerald-50 rounded-xl px-4 py-3 flex items-center
                        justify-between">
              <span class="text-sm text-emerald-700 font-medium">
                <i class="pi pi-check-circle mr-1"></i>
                {{ selectedItemCount }} varyant seçildi
              </span>
              <span class="text-sm font-bold text-emerald-700">
                Toplam: {{ totalEnteredQuantity }} adet
              </span>
            </div>
          }

          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Not (Opsiyonel)
            </label>
            <input
              type="text"
              [(ngModel)]="receiveNotes"
              placeholder="Tedarikçi adı, fatura no vb."
              class="w-full px-3 py-2 text-sm border border-gray-200 rounded-xl
                     focus:outline-none focus:ring-2 focus:ring-red-300" />
          </div>

        </div>
      }

      <ng-template pTemplate="footer">
        <div class="flex justify-end gap-2">
          <p-button label="İptal" severity="secondary" [outlined]="true"
            [disabled]="isSubmitting"
            (onClick)="receiveDialogVisible = false" />
          <p-button
            label="Stok Girişini Kaydet"
            icon="pi pi-save"
            [loading]="isSubmitting"
            [disabled]="selectedItemCount === 0 || isSubmitting"
            (onClick)="onReceiveSubmit()" />
        </div>
      </ng-template>

    </p-dialog>

    <p-toast position="top-right" />
  `
})
export class WarehouseComponent implements OnInit {
  stocks:              WarehouseStock[] = [];
  receiveItems:        ReceiveItem[]    = [];
  isLoading            = false;
  isLoadingProducts    = false;
  isSubmitting         = false;
  receiveDialogVisible = false;
  receiveNotes         = '';
  searchTerm           = '';

  get filteredStocks(): WarehouseStock[] {
    if (!this.searchTerm) return this.stocks;
    const term = this.searchTerm.toLowerCase();
    return this.stocks.filter(s =>
      s.productName.toLowerCase().includes(term) ||
      s.barcode.toLowerCase().includes(term) ||
      s.color?.toLowerCase().includes(term) ||
      s.size?.toLowerCase().includes(term)
    );
  }

  get totalStock():    number { return this.stocks.reduce((a, s) => a + s.quantity, 0); }
  get totalReserved(): number { return this.stocks.reduce((a, s) => a + s.reservedQuantity, 0); }

  get selectedItemCount(): number {
    return this.receiveItems.filter(i => i.enteredQuantity > 0).length;
  }

  get totalEnteredQuantity(): number {
    return this.receiveItems.reduce((a, i) => a + (i.enteredQuantity || 0), 0);
  }

  constructor(
    private warehouseService: WarehouseService,
    private messageService:   MessageService
  ) {}

  ngOnInit(): void { this.loadStocks(); }

  loadStocks(): void {
    this.isLoading = true;
    this.warehouseService.getStock().subscribe({
      next: data => { this.stocks = data; this.isLoading = false; },
      error: (err) => {
        this.isLoading = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Stok bilgileri yüklenirken hata oluştu.')
        });
      }
    });
  }

  openReceiveDialog(): void {
    this.receiveNotes         = '';
    this.receiveDialogVisible = true;
    this.isLoadingProducts    = true;

    this.warehouseService.getProductsForReceiving().subscribe({
      next: products => {
        this.receiveItems = products.map(p => ({
          ...p,
          enteredQuantity: 0,
          selected:        false
        }));
        this.isLoadingProducts = false;
      },
      error: (err) => {
        this.isLoadingProducts = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Ürünler yüklenemedi.')
        });
      }
    });
  }

  onReceiveSubmit(): void {
    // ✅ variantId kullan
    const items = this.receiveItems
      .filter(i => i.enteredQuantity > 0)
      .map(i => ({
        variantId: i.variantId,
        quantity:  i.enteredQuantity
      }));

    if (!items.length) return;

    this.isSubmitting = true;
    this.warehouseService.receiveStock({ items, notes: this.receiveNotes })
      .subscribe({
        next: result => {
          this.isSubmitting         = false;
          this.receiveDialogVisible = false;
          this.loadStocks();
          this.messageService.add({
            severity: 'success',
            summary:  'Stok Girişi Başarılı',
            detail:   `${result.totalProducts} varyant, toplam ${result.totalQuantity} adet eklendi.`,
            life: 4000
          });
        },
        error: (err) => {
          this.isSubmitting = false;
          this.messageService.add({
            severity: 'error', summary: 'Hata',
            detail: apiErrorMessage(err, 'Stok girişi sırasında hata oluştu.')
          });
        }
      });
  }
}