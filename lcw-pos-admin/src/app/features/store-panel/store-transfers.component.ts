import { Component, OnInit } from '@angular/core';
import { apiErrorMessage } from '../../core/utils/api.util';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { StockTransferService } from '../../core/services/stock-transfer.service';
import { WarehouseService } from '../../core/services/warehouse.service';
import { AuthService } from '../../core/services/auth.service';
import { StockTransfer } from '../../shared/models/stock-transfer.model';
import { UtcPipe } from '../../shared/pipes/utc.pipe';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageService, ConfirmationService } from 'primeng/api';

interface RequestItem {
  variantId:         number;
  productName:       string;
  barcode:           string;
  color?:            string;
  size?:             string;
  availableQuantity: number;
  requestedQuantity: number;
}

@Component({
  selector: 'app-store-transfers',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    TableModule, ButtonModule, DialogModule,
    InputNumberModule, InputTextModule,
    TagModule, ToastModule, TooltipModule, ConfirmDialogModule, UtcPipe
  ],
  providers: [MessageService, ConfirmationService],
  template: `
    <div class="space-y-6">

      <!-- ── Başlık ─────────────────────────────────────────── -->
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-2xl font-bold text-gray-800">Sevkiyat Yönetimi</h2>
          <p class="text-gray-400 text-sm mt-1">
            Depodan ürün talep edin veya gelen sevkiyatları teslim alın
          </p>
        </div>
        <button (click)="openRequestDialog()"
          class="inline-flex items-center gap-2 bg-red-500 hover:bg-red-600
                 text-white px-5 py-2.5 rounded-xl transition-colors
                 font-medium text-sm shadow-sm shadow-red-200">
          <i class="pi pi-plus"></i>
          Ürün Talep Et
        </button>
      </div>

      <!-- ── Durum Filtreleri ───────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
        <div class="flex items-center gap-3 flex-wrap">
          <span class="text-sm font-semibold text-gray-600">Filtrele:</span>
          @for (f of statusFilters; track f.value) {
            <button
              (click)="activeFilter = f.value"
              [class]="activeFilter === f.value
                ? 'bg-red-500 text-white'
                : 'bg-gray-100 text-gray-600 hover:bg-gray-200'"
              class="px-4 py-1.5 rounded-xl text-sm font-medium transition-colors">
              {{ f.label }}
              <span class="ml-1 text-xs opacity-70">({{ getCount(f.value) }})</span>
            </button>
          }
        </div>
      </div>

      <!-- ── Tablo ──────────────────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        <p-table
          [value]="filteredTransfers"
          [loading]="isLoading"
          [paginator]="true"
          [rows]="10"
          styleClass="p-datatable-sm"
          [rowHover]="true"
          [expandedRowKeys]="expandedRows"
          dataKey="id">

          <ng-template pTemplate="header">
            <tr>
              <th style="width:3rem"></th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Belge No</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                Ürün
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                Miktar
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Durum</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Tarih</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                İşlem
              </th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-transfer let-expanded="expanded">
            <tr class="border-b border-gray-50">
              <td>
                <p-button
                  [icon]="expanded ? 'pi pi-chevron-down' : 'pi pi-chevron-right'"
                  size="small" severity="secondary" [text]="true"
                  (onClick)="toggleRow(transfer)" />
              </td>
              <td>
                <span class="font-mono text-xs bg-gray-50 border border-gray-200
                             px-2.5 py-1 rounded-lg text-gray-700 font-semibold">
                  {{ transfer.transferNumber }}
                </span>
              </td>
              <td class="text-center font-bold text-gray-700">
                {{ transfer.totalProducts }}
              </td>
              <td class="text-center font-bold text-gray-700">
                {{ transfer.totalQuantity }}
              </td>
              <td>
                <span [class]="getStatusClass(transfer.status)"
                      class="text-xs px-3 py-1 rounded-full font-semibold">
                  {{ getStatusLabel(transfer.status) }}
                </span>
              </td>
              <td class="text-gray-400 text-xs">
                {{ transfer.createdAt | utc | date:'dd.MM.yyyy HH:mm' }}
              </td>
              <td>
                <div class="flex items-center justify-center">
                  <!-- MAĞAZA: Sadece Teslim Al -->
                  @if (transfer.status === 'Shipped') {
                    <p-button
                      label="Teslim Al"
                      icon="pi pi-check"
                      size="small"
                      severity="success"
                      (onClick)="confirmReceive(transfer)" />
                  }
                  @if (transfer.status === 'Draft') {
                    <div class="flex items-center gap-2">
                      <span class="text-xs text-gray-400">
                        ⏳ Depo onayı bekleniyor
                      </span>
                      <p-button
                        icon="pi pi-times"
                        size="small"
                        severity="danger"
                        [text]="true"
                        [rounded]="true"
                        pTooltip="Talebi İptal Et"
                        tooltipPosition="top"
                        (onClick)="confirmCancel(transfer)" />
                    </div>
                  }
                  @if (transfer.status === 'Received') {
                    <span class="text-xs text-emerald-500 font-medium">
                      ✓ Teslim Alındı
                    </span>
                  }
                  @if (transfer.status === 'Cancelled') {
                    <span class="text-xs text-red-400">❌ İptal</span>
                  }
                </div>
              </td>
            </tr>
          </ng-template>

          <!-- Detay -->
          <ng-template pTemplate="rowexpansion" let-transfer>
            <tr>
              <td colspan="7" class="bg-gray-50 px-8 py-4">
                <div class="flex flex-wrap gap-x-6 gap-y-1 text-xs text-gray-500 mb-3">
                  <span>Oluşturma: {{ transfer.createdAt | utc | date:'dd.MM.yyyy HH:mm' }}</span>
                  @if (transfer.shippedAt) {
                    <span>Gönderim: {{ transfer.shippedAt | utc | date:'dd.MM.yyyy HH:mm' }}</span>
                  }
                  @if (transfer.receivedAt) {
                    <span>Teslim: {{ transfer.receivedAt | utc | date:'dd.MM.yyyy HH:mm' }}</span>
                  }
                  @if (transfer.notes) {
                    <span class="text-gray-700"><i class="pi pi-comment mr-1"></i>{{ transfer.notes }}</span>
                  }
                </div>
                <div class="rounded-xl overflow-hidden border border-gray-200">
                  <table class="w-full text-sm">
                    <thead class="bg-gray-100">
                      <tr>
                        <th class="px-4 py-2 text-left text-xs font-semibold
                                   text-gray-500 uppercase">Barkod</th>
                        <th class="px-4 py-2 text-left text-xs font-semibold
                                   text-gray-500 uppercase">Ürün</th>
                        <th class="px-4 py-2 text-left text-xs font-semibold
                                   text-gray-500 uppercase">Renk / Beden</th>
                        <th class="px-4 py-2 text-center text-xs font-semibold
                                   text-gray-500 uppercase">Talep</th>
                        <th class="px-4 py-2 text-center text-xs font-semibold
                                   text-gray-500 uppercase">Gönderilen</th>
                        <th class="px-4 py-2 text-center text-xs font-semibold
                                   text-gray-500 uppercase">Teslim</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-gray-100 bg-white">
                      @for (item of transfer.items; track item.variantId) {
                        <tr>
                          <td class="px-4 py-2">
                            <span class="font-mono text-xs text-gray-500">
                              {{ item.barcode }}
                            </span>
                          </td>
                          <td class="px-4 py-2 font-medium text-gray-800">
                            {{ item.productName }}
                          </td>
                          <td class="px-4 py-2">
                            <div class="flex gap-1.5">
                              @if (item.color) {
                                <span class="text-xs bg-blue-100 text-blue-700
                                             px-2 py-0.5 rounded-full">
                                  {{ item.color }}
                                </span>
                              }
                              @if (item.size) {
                                <span class="text-xs bg-gray-100 text-gray-700
                                             px-2 py-0.5 rounded-full font-bold">
                                  {{ item.size }}
                                </span>
                              }
                            </div>
                          </td>
                          <td class="px-4 py-2 text-center font-bold text-gray-600">
                            {{ item.requestedQuantity }}
                          </td>
                          <td class="px-4 py-2 text-center font-bold"
                              [class.text-blue-600]="item.shippedQuantity > 0"
                              [class.text-gray-300]="item.shippedQuantity === 0">
                            {{ item.shippedQuantity }}
                          </td>
                          <td class="px-4 py-2 text-center font-bold"
                              [class.text-emerald-600]="item.receivedQuantity > 0 && item.receivedQuantity >= item.shippedQuantity"
                              [class.text-amber-600]="transfer.status === 'Received' && item.receivedQuantity < item.shippedQuantity"
                              [class.text-gray-300]="item.receivedQuantity === 0 && transfer.status !== 'Received'">
                            {{ item.receivedQuantity }}
                            @if (transfer.status === 'Received' && item.receivedQuantity < item.shippedQuantity) {
                              <span class="text-xs font-semibold">(−{{ item.shippedQuantity - item.receivedQuantity }})</span>
                            }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </td>
            </tr>
          </ng-template>

          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="7">
                <div class="text-center py-16 text-gray-300">
                  <i class="pi pi-truck text-5xl mb-4 block"></i>
                  <p>Henüz sevkiyat talebi yok</p>
                </div>
              </td>
            </tr>
          </ng-template>

        </p-table>
      </div>

    </div>

    <!-- ── Talep Oluştur Dialog ───────────────────────────── -->
    <p-dialog
      [visible]="requestDialogVisible"
      (visibleChange)="requestDialogVisible = $event"
      [modal]="true" [draggable]="false"
      header="Depodan Ürün Talep Et"
      [style]="{ width: '720px' }">

      <div class="space-y-4 pt-2">

        <div class="bg-blue-50 rounded-xl px-4 py-3 flex items-start gap-3">
          <i class="pi pi-info-circle text-blue-500 mt-0.5"></i>
          <p class="text-sm text-blue-700">
            Talep ettiğiniz ürünler depo tarafından onaylanarak gönderilecektir.
          </p>
        </div>

        @if (isLoadingProducts) {
          <div class="flex items-center justify-center py-8 text-gray-300">
            <i class="pi pi-spin pi-spinner text-2xl mr-2"></i>
            Ürünler yükleniyor...
          </div>
        } @else {

          <!-- Arama -->
          <div class="flex items-center gap-3">
            <div class="relative flex-1">
              <i class="pi pi-search absolute left-3 top-1/2 -translate-y-1/2
                         text-gray-400 text-sm"></i>
              <input type="text" [(ngModel)]="productSearch"
                     (input)="filterProducts()"
                     placeholder="Ürün veya barkod ara..."
                     class="w-full pl-9 pr-4 py-2 text-sm border border-gray-200
                            rounded-xl focus:outline-none focus:ring-2
                            focus:ring-red-300" />
            </div>
            <label class="flex items-center gap-2 text-sm text-gray-600 whitespace-nowrap cursor-pointer">
              <input type="checkbox" [(ngModel)]="onlyAvailable"
                     (ngModelChange)="filterProducts()" />
              Sadece depoda olanlar
            </label>
          </div>

          <div class="border border-gray-100 rounded-xl overflow-hidden">
            <div class="bg-gray-50 px-4 py-2.5 grid grid-cols-12 gap-3
                        text-xs font-semibold text-gray-500 uppercase">
              <div class="col-span-5">Ürün</div>
              <div class="col-span-3 text-center">Depoda Mevcut</div>
              <div class="col-span-4 text-center">Talep Miktarı</div>
            </div>

            <div class="divide-y divide-gray-50 max-h-72 overflow-y-auto">
              @if (filteredRequestItems.length === 0) {
                <div class="text-center py-8 text-sm text-gray-400">
                  Eşleşen ürün yok
                </div>
              }
              @for (item of filteredRequestItems; track item.variantId) {
                <div class="px-4 py-3 grid grid-cols-12 gap-3 items-center
                            hover:bg-gray-50 transition-colors"
                     [class.bg-blue-50]="item.requestedQuantity > 0">
                  <div class="col-span-5">
                    <p class="text-sm font-semibold text-gray-800 truncate">
                      {{ item.productName }}
                    </p>
                    <div class="flex items-center gap-1.5 mt-0.5">
                      <span class="font-mono text-xs text-gray-400">
                        {{ item.barcode }}
                      </span>
                      @if (item.color) {
                        <span class="text-xs bg-blue-100 text-blue-600
                                     px-1.5 py-0.5 rounded-full">
                          {{ item.color }}
                        </span>
                      }
                      @if (item.size) {
                        <span class="text-xs bg-gray-100 text-gray-600
                                     px-1.5 py-0.5 rounded-full font-bold">
                          {{ item.size }}
                        </span>
                      }
                    </div>
                  </div>
                  <div class="col-span-3 text-center">
                    <span class="text-sm font-bold"
                          [class.text-emerald-600]="item.availableQuantity > 10"
                          [class.text-orange-500]="item.availableQuantity > 0 && item.availableQuantity <= 10"
                          [class.text-red-400]="item.availableQuantity <= 0">
                      {{ item.availableQuantity }}
                    </span>
                  </div>
                  <div class="col-span-4 flex justify-center">
                    <p-inputNumber
                      [(ngModel)]="item.requestedQuantity"
                      [min]="0" [max]="item.availableQuantity"
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

          @if (requestSelectedCount > 0) {
            <div class="bg-blue-50 rounded-xl px-4 py-3 flex justify-between">
              <span class="text-sm text-blue-700 font-medium">
                {{ requestSelectedCount }} ürün seçildi
              </span>
              <span class="text-sm font-bold text-blue-700">
                Toplam: {{ requestTotalQuantity }} adet
              </span>
            </div>
          }

          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Not (Opsiyonel)
            </label>
            <input type="text" [(ngModel)]="requestNotes"
                   placeholder="Acil, sezon sonu vb."
                   class="w-full px-3 py-2 text-sm border border-gray-200
                          rounded-xl focus:outline-none focus:ring-2
                          focus:ring-red-300" />
          </div>
        }
      </div>

      <ng-template pTemplate="footer">
        <div class="flex justify-end gap-2">
          <p-button label="İptal" severity="secondary" [outlined]="true"
            (onClick)="requestDialogVisible = false" />
          <p-button
            label="Talep Gönder"
            icon="pi pi-send"
            [loading]="isSubmitting"
            [disabled]="requestSelectedCount === 0 || isSubmitting"
            (onClick)="onRequestSubmit()" />
        </div>
      </ng-template>
    </p-dialog>

    <p-toast position="top-right" />
    <p-confirmDialog />

    <!-- ── Teslim Al Dialog ───────────────────────────────── -->
    <p-dialog [visible]="receiveDialogVisible"
              (visibleChange)="receiveDialogVisible = $event"
              [modal]="true" [draggable]="false"
              [header]="'Teslim Al — ' + (receiveTarget?.transferNumber ?? '')"
              [style]="{ width: '680px' }">
      <div class="space-y-4 pt-1">
        <div class="bg-blue-50 rounded-xl px-4 py-3 text-sm text-blue-700 flex gap-3">
          <i class="pi pi-info-circle mt-0.5"></i>
          <span>
            Gelen ürünleri sayın. Eksik veya hasarlı gelen varsa satılabilir adedi girin;
            sadece bu adet mağaza stoğuna eklenir.
          </span>
        </div>

        <div class="border border-gray-100 rounded-xl overflow-hidden">
          <div class="bg-gray-50 px-4 py-2.5 grid grid-cols-12 gap-3
                      text-xs font-semibold text-gray-500 uppercase">
            <div class="col-span-6">Ürün</div>
            <div class="col-span-2 text-center">Gönderilen</div>
            <div class="col-span-4 text-center">Teslim Alınan</div>
          </div>
          <div class="divide-y divide-gray-50 max-h-72 overflow-y-auto">
            @for (l of receiveLines; track l.variantId) {
              <div class="px-4 py-2.5 grid grid-cols-12 gap-3 items-center"
                   [class.bg-amber-50]="l.received < l.shipped">
                <div class="col-span-6 min-w-0">
                  <p class="text-sm font-semibold text-gray-800 truncate">{{ l.productName }}</p>
                  <p class="text-xs text-gray-400">
                    <span class="font-mono">{{ l.barcode }}</span>
                    @if (l.color) { · {{ l.color }} }
                    @if (l.size) { · {{ l.size }} }
                  </p>
                </div>
                <div class="col-span-2 text-center font-bold text-gray-700">{{ l.shipped }}</div>
                <div class="col-span-4 flex justify-center items-center gap-2">
                  <p-inputNumber [(ngModel)]="l.received" [min]="0" [max]="l.shipped"
                                 [showButtons]="true" buttonLayout="horizontal"
                                 incrementButtonIcon="pi pi-plus" decrementButtonIcon="pi pi-minus"
                                 inputStyleClass="w-14 text-center text-sm font-bold" />
                  @if (l.received < l.shipped) {
                    <span class="text-xs font-semibold text-amber-700 whitespace-nowrap">
                      −{{ l.shipped - l.received }}
                    </span>
                  }
                </div>
              </div>
            }
          </div>
        </div>

        @if (receiveShortage > 0) {
          <div class="flex items-center justify-between text-sm">
            <span class="text-amber-700 font-medium">
              <i class="pi pi-exclamation-triangle mr-1"></i>
              Toplam {{ receiveShortage }} adet eksik/hasarlı
            </span>
            <button (click)="setAllReceived()" class="text-blue-600 hover:underline text-xs">
              Tümü eksiksiz geldi
            </button>
          </div>
        }

        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Açıklama
            @if (receiveShortage > 0) { <span class="text-red-500">*</span> }
            @else { <span class="text-gray-400 font-normal">(opsiyonel)</span> }
          </label>
          <input type="text" [(ngModel)]="receiveNote" maxlength="200"
                 [placeholder]="receiveShortage > 0 ? 'Örn: 2 adet koli hasarlı, 1 adet eksik geldi' : 'Not'"
                 class="w-full px-3 py-2 text-sm border border-gray-200 rounded-xl
                        focus:outline-none focus:ring-2 focus:ring-red-300" />
        </div>
      </div>

      <ng-template pTemplate="footer">
        <p-button label="İptal" severity="secondary" [outlined]="true"
                  (onClick)="receiveDialogVisible = false" />
        <p-button [label]="receiveShortage > 0 ? 'Farkla Teslim Al' : 'Teslim Al'"
                  icon="pi pi-check" severity="success"
                  [loading]="isReceiving" [disabled]="!receiveValid || isReceiving"
                  (onClick)="submitReceive()" />
      </ng-template>
    </p-dialog>
  `
})
export class StoreTransfersComponent implements OnInit {
  transfers:            StockTransfer[] = [];
  requestItems:         RequestItem[]   = [];
  filteredRequestItems: RequestItem[]   = [];

  isLoading          = false;
  isLoadingProducts  = false;
  isSubmitting       = false;
  requestDialogVisible = false;
  activeFilter       = 'all';
  requestNotes       = '';
  productSearch      = '';
  onlyAvailable      = true;
  expandedRows:      { [key: number]: boolean } = {};

  statusFilters = [
    { label: 'Tümü',      value: 'all'       },
    { label: '📝 Taslak', value: 'Draft'     },
    { label: '🚚 Yolda',  value: 'Shipped'   },
    { label: '✅ Teslim', value: 'Received'  },
    { label: '❌ İptal',  value: 'Cancelled' },
  ];

  get filteredTransfers(): StockTransfer[] {
    if (this.activeFilter === 'all') return this.transfers;
    return this.transfers.filter(t => t.status === this.activeFilter);
  }

  get requestSelectedCount(): number {
    return this.requestItems.filter(i => i.requestedQuantity > 0).length;
  }

  get requestTotalQuantity(): number {
    return this.requestItems.reduce((a, i) => a + (i.requestedQuantity || 0), 0);
  }

  constructor(
    private transferService:     StockTransferService,
    private warehouseService:    WarehouseService,
    private authService:         AuthService,
    private messageService:      MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit(): void { this.loadTransfers(); }

  loadTransfers(): void {
    this.isLoading = true;
    this.transferService.getAll().subscribe({
      next: data => { this.transfers = data; this.isLoading = false; },
      error: (err) => {
        this.isLoading = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Sevkiyatlar yüklenemedi.')
        });
      }
    });
  }

  openRequestDialog(): void {
    this.requestNotes          = '';
    this.productSearch         = '';
    this.requestDialogVisible  = true;
    this.isLoadingProducts     = true;

    this.warehouseService.getProductsForReceiving().subscribe({
      next: products => {
        this.requestItems = products.map(p => ({
          variantId:         p.variantId,
          productName:       p.productName,
          barcode:           p.barcode,
          color:             p.color,
          size:              p.size,
          availableQuantity: p.availableQuantity,
          requestedQuantity: 0
        }));
        this.filterProducts();
        this.isLoadingProducts    = false;
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

  filterProducts(): void {
    const term = this.productSearch.trim().toLowerCase();
    this.filteredRequestItems = this.requestItems.filter(i =>
      // Seçilmiş kalem filtre yüzünden gözden kaybolmasın
      (i.requestedQuantity > 0 || !this.onlyAvailable || i.availableQuantity > 0) &&
      (!term ||
        i.productName?.toLowerCase().includes(term) ||
        i.barcode?.toLowerCase().includes(term) ||
        i.color?.toLowerCase().includes(term) ||
        i.size?.toLowerCase().includes(term))
    );
  }

  onRequestSubmit(): void {
    const storeId = this.authService.storeId();
    if (!storeId || this.requestSelectedCount === 0) return;

    this.isSubmitting = true;

    const items = this.requestItems
      .filter(i => i.requestedQuantity > 0)
      .map(i => ({
        variantId:         i.variantId,
        requestedQuantity: i.requestedQuantity
      }));

    this.transferService.create({
      toStoreId: +storeId,
      items,
      notes: this.requestNotes
    }).subscribe({
      next: result => {
        this.isSubmitting         = false;
        this.requestDialogVisible = false;
        this.loadTransfers();
        this.messageService.add({
          severity: 'success', summary: 'Talep Gönderildi',
          detail:   `${result.transferNumber} — Depo onayı bekleniyor`,
          life: 5000
        });
      },
      error: (err) => {
        this.isSubmitting = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Talep oluşturulamadı.')
        });
      }
    });
  }

  // ── Teslim Al (kalem bazında, eksik/hasarlı girişli) ────────
  receiveDialogVisible = false;
  receiveTarget: StockTransfer | null = null;
  receiveLines: { variantId: number; productName: string; barcode: string;
                  color?: string; size?: string; shipped: number; received: number }[] = [];
  receiveNote = '';
  isReceiving = false;

  get receiveShortage(): number {
    return this.receiveLines.reduce((t, l) => t + Math.max(0, l.shipped - (l.received ?? 0)), 0);
  }

  get receiveValid(): boolean {
    return this.receiveLines.every(l => l.received != null && l.received >= 0 && l.received <= l.shipped)
        && (this.receiveShortage === 0 || this.receiveNote.trim().length > 0)
        && this.receiveNote.trim().length <= 200;
  }

  confirmReceive(transfer: StockTransfer): void {
    this.receiveTarget = transfer;
    this.receiveNote   = '';
    this.receiveLines  = transfer.items
      .filter(i => i.shippedQuantity > 0)
      .map(i => ({
        variantId: i.variantId, productName: i.productName, barcode: i.barcode,
        color: i.color, size: i.size,
        shipped: i.shippedQuantity, received: i.shippedQuantity
      }));
    this.receiveDialogVisible = true;
  }

  setAllReceived(): void {
    this.receiveLines.forEach(l => l.received = l.shipped);
  }

  submitReceive(): void {
    const t = this.receiveTarget;
    if (!t || !this.receiveValid) return;

    const hasDiff = this.receiveShortage > 0;
    this.isReceiving = true;
    this.transferService.receive(t.id, {
      // Fark yoksa kalem listesi göndermeye gerek yok (tam teslim)
      items: hasDiff
        ? this.receiveLines.map(l => ({ variantId: l.variantId, receivedQuantity: l.received }))
        : undefined,
      note: this.receiveNote.trim() || undefined
    }).subscribe({
      next: () => {
        this.isReceiving = false;
        this.receiveDialogVisible = false;
        this.loadTransfers();
        this.messageService.add({
          severity: hasDiff ? 'warn' : 'success',
          summary: 'Teslim Alındı',
          detail: hasDiff
            ? `Stoklar güncellendi. ${this.receiveShortage} adet eksik/hasarlı olarak kaydedildi.`
            : 'Stoklar güncellendi.',
          life: 6000
        });
      },
      error: (err) => {
        this.isReceiving = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'İşlem sırasında hata oluştu.')
        });
      }
    });
  }

  confirmCancel(transfer: StockTransfer): void {
    this.confirmationService.confirm({
      message: `"${transfer.transferNumber}" talebi iptal edilecek. Emin misiniz?`,
      header:      'Talebi İptal Et',
      icon:        'pi pi-exclamation-triangle',
      acceptLabel: 'Evet, İptal Et',
      rejectLabel: 'Vazgeç',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.transferService.cancel(transfer.id, 'Mağaza tarafından iptal edildi').subscribe({
          next: () => {
            this.loadTransfers();
            this.messageService.add({
              severity: 'info', summary: 'İptal Edildi',
              detail: `${transfer.transferNumber} iptal edildi.`
            });
          },
          error: (err) => this.messageService.add({
            severity: 'error', summary: 'Hata',
            detail: apiErrorMessage(err, 'Talep iptal edilemedi.')
          })
        });
      }
    });
  }

  toggleRow(transfer: StockTransfer): void {
    if (this.expandedRows[transfer.id])
      delete this.expandedRows[transfer.id];
    else
      this.expandedRows[transfer.id] = true;
    this.expandedRows = { ...this.expandedRows };
  }

  getCount(status: string): number {
    if (status === 'all') return this.transfers.length;
    return this.transfers.filter(t => t.status === status).length;
  }

  getStatusLabel(status: string): string {
    const map: Record<string, string> = {
      Draft: '📝 Taslak', Shipped: '🚚 Yolda',
      Received: '✅ Teslim', Cancelled: '❌ İptal'
    };
    return map[status] ?? status;
  }

  getStatusClass(status: string): string {
    const map: Record<string, string> = {
      Draft:     'bg-gray-100 text-gray-600',
      Shipped:   'bg-blue-100 text-blue-700',
      Received:  'bg-emerald-100 text-emerald-700',
      Cancelled: 'bg-red-100 text-red-600'
    };
    return map[status] ?? 'bg-gray-100 text-gray-600';
  }
}