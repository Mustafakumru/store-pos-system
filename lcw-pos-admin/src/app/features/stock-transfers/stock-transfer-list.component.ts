import { Component, OnInit } from '@angular/core';
import { apiErrorMessage } from '../../core/utils/api.util';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { StockTransferService } from '../../core/services/stock-transfer.service';
import { StockTransfer } from '../../shared/models/stock-transfer.model';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageService, ConfirmationService } from 'primeng/api';

@Component({
  selector: 'app-stock-transfer-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    TableModule, ButtonModule, DialogModule,
    InputNumberModule, TagModule, ToastModule,
    TooltipModule, ConfirmDialogModule
  ],
  providers: [MessageService, ConfirmationService],
  template: `
    <div class="space-y-6">

      <!-- ── Başlık ─────────────────────────────────────────── -->
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-2xl font-bold text-gray-800">Sevkiyat Yönetimi</h2>
          <p class="text-gray-400 text-sm mt-1">
            Mağazalardan gelen talepler — depo gönderim ekranı
          </p>
        </div>
        <div class="bg-blue-50 border border-blue-200 rounded-xl
                    px-4 py-2 text-sm text-blue-700 flex items-center gap-2">
          <i class="pi pi-info-circle"></i>
          Talepler mağaza panelinden oluşturulur
        </div>
      </div>

      <!-- ── İstatistik Kartları ────────────────────────────── -->
      <div class="grid grid-cols-4 gap-4">
        @for (stat of stats; track stat.label) {
          <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                  {{ stat.label }}
                </p>
                <p class="text-3xl font-bold mt-1" [class]="stat.color">
                  {{ stat.value }}
                </p>
              </div>
              <div class="w-12 h-12 rounded-xl flex items-center justify-center"
                   [class]="stat.bgColor">
                <i [class]="'pi ' + stat.icon + ' text-xl ' + stat.iconColor"></i>
              </div>
            </div>
          </div>
        }
      </div>

      <!-- ── Filtreler ──────────────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
        <div class="flex items-center gap-3 flex-wrap">
          <span class="text-sm font-semibold text-gray-600">Filtrele:</span>
          @for (s of statusFilters; track s.value) {
            <button
              (click)="activeFilter = s.value"
              [class]="activeFilter === s.value
                ? 'bg-red-500 text-white'
                : 'bg-gray-100 text-gray-600 hover:bg-gray-200'"
              class="px-4 py-1.5 rounded-xl text-sm font-medium transition-colors">
              {{ s.label }}
              <span class="ml-1 text-xs opacity-70">
                ({{ getCount(s.value) }})
              </span>
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
              <th class="text-xs font-semibold text-gray-500 uppercase">Mağaza</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">Ürün</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">Miktar</th>
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
              <td class="font-semibold text-gray-800 text-sm">
                {{ transfer.storeName }}
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
                {{ transfer.createdAt | date:'dd.MM.yyyy HH:mm' }}
              </td>
              <td>
                <div class="flex items-center justify-center gap-1.5">
                  <!-- DEPO: Sadece Gönder ve İptal -->
                  @if (transfer.status === 'Draft') {
                    <p-button icon="pi pi-send" size="small"
                      severity="info" [rounded]="true"
                      pTooltip="Gönder" tooltipPosition="top"
                      (onClick)="openShipDialog(transfer)" />
                    <p-button icon="pi pi-times" size="small"
                      severity="danger" [rounded]="true"
                      pTooltip="İptal Et" tooltipPosition="top"
                      (onClick)="confirmCancel(transfer)" />
                  }
                  @if (transfer.status === 'Shipped') {
                    <span class="text-xs text-blue-500 font-medium">
                      🚚 Yolda — Mağaza teslim alacak
                    </span>
                  }
                  @if (transfer.status === 'Received') {
                    <span class="text-xs text-emerald-500 font-medium">
                      ✓ Tamamlandı
                    </span>
                  }
                  @if (transfer.status === 'Cancelled') {
                    <span class="text-xs text-gray-400 font-medium">
                      İptal edildi
                    </span>
                  }
                </div>
              </td>
            </tr>
          </ng-template>

          <!-- Detay satırı -->
          <ng-template pTemplate="rowexpansion" let-transfer>
            <tr>
              <td colspan="8" class="bg-gray-50 px-8 py-4">
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
                                             px-2 py-0.5 rounded-full font-semibold">
                                  {{ item.size }}
                                </span>
                              }
                            </div>
                          </td>
                          <td class="px-4 py-2 text-center font-bold text-gray-700">
                            {{ item.requestedQuantity }}
                          </td>
                          <td class="px-4 py-2 text-center font-bold"
                              [class.text-blue-600]="item.shippedQuantity > 0"
                              [class.text-gray-300]="item.shippedQuantity === 0">
                            {{ item.shippedQuantity }}
                          </td>
                          <td class="px-4 py-2 text-center font-bold"
                              [class.text-emerald-600]="item.receivedQuantity > 0"
                              [class.text-gray-300]="item.receivedQuantity === 0">
                            {{ item.receivedQuantity }}
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
              <td colspan="8">
                <div class="flex flex-col items-center justify-center
                            py-16 text-gray-300">
                  <i class="pi pi-truck text-5xl mb-4"></i>
                  <p class="text-base font-medium">Sevkiyat talebi yok</p>
                  <p class="text-sm mt-1">
                    Mağaza yöneticileri talep oluşturduğunda burada görünür
                  </p>
                </div>
              </td>
            </tr>
          </ng-template>

        </p-table>
      </div>

    </div>

    <!-- ── Gönder Dialog ──────────────────────────────────── -->
    <p-dialog
      [visible]="shipDialogVisible"
      (visibleChange)="shipDialogVisible = $event"
      [modal]="true" [draggable]="false"
      header="Sevkiyatı Gönder"
      [style]="{ width: '620px' }">

      @if (shippingTransfer) {
        <div class="space-y-4 pt-2">

          <div class="bg-blue-50 rounded-xl px-4 py-3 flex items-start gap-3">
            <i class="pi pi-info-circle text-blue-500 mt-0.5"></i>
            <div>
              <p class="text-sm font-semibold text-blue-700">
                {{ shippingTransfer.transferNumber }} —
                {{ shippingTransfer.storeName }}
              </p>
              <p class="text-xs text-blue-600 mt-0.5">
                Gerçekte gönderilen miktarı girin (talepten farklı olabilir)
              </p>
            </div>
          </div>

          <div class="border border-gray-100 rounded-xl overflow-hidden">
            <div class="bg-gray-50 px-4 py-2.5 grid grid-cols-12 gap-3
                        text-xs font-semibold text-gray-500 uppercase">
              <div class="col-span-5">Ürün</div>
              <div class="col-span-3 text-center">Talep Edilen</div>
              <div class="col-span-4 text-center">Gönderilecek</div>
            </div>

            <div class="divide-y divide-gray-50 max-h-72 overflow-y-auto">
              @for (item of shipItems; track item.variantId) {
                <div class="px-4 py-3 grid grid-cols-12 gap-3 items-center">
                  <div class="col-span-5">
                    <p class="text-sm font-semibold text-gray-800">
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
                                     px-1.5 py-0.5 rounded-full font-semibold">
                          {{ item.size }}
                        </span>
                      }
                    </div>
                  </div>
                  <div class="col-span-3 text-center font-bold text-gray-400">
                    {{ item.requestedQuantity }}
                  </div>
                  <div class="col-span-4 flex justify-center">
                    <p-inputNumber
                      [(ngModel)]="item.shippedQuantity"
                      [min]="0" [max]="9999"
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
        </div>
      }

      <ng-template pTemplate="footer">
        <div class="flex justify-end gap-2">
          <p-button label="İptal" severity="secondary" [outlined]="true"
            (onClick)="shipDialogVisible = false" />
          <p-button label="Gönder" icon="pi pi-send"
            severity="info" [loading]="isSubmitting"
            (onClick)="onShipSubmit()" />
        </div>
      </ng-template>
    </p-dialog>

    <p-toast position="top-right" />
    <p-confirmDialog />
  `
})
export class StockTransferListComponent implements OnInit {
  transfers:      StockTransfer[] = [];
  shipItems:      any[]           = [];
  shippingTransfer: StockTransfer | null = null;

  isLoading      = false;
  isSubmitting   = false;
  shipDialogVisible = false;
  activeFilter   = 'all';
  expandedRows:  { [key: number]: boolean } = {};

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

  get stats() {
    return [
      { label: 'Toplam',  value: this.transfers.length,
        color: 'text-gray-800',    bgColor: 'bg-blue-50',
        icon: 'pi-truck',          iconColor: 'text-blue-500' },
      { label: 'Taslak',  value: this.getCount('Draft'),
        color: 'text-gray-600',    bgColor: 'bg-gray-50',
        icon: 'pi-file',           iconColor: 'text-gray-500' },
      { label: 'Yolda',   value: this.getCount('Shipped'),
        color: 'text-blue-600',    bgColor: 'bg-blue-50',
        icon: 'pi-send',           iconColor: 'text-blue-500' },
      { label: 'Teslim',  value: this.getCount('Received'),
        color: 'text-emerald-600', bgColor: 'bg-emerald-50',
        icon: 'pi-check-circle',   iconColor: 'text-emerald-500' },
    ];
  }

  constructor(
    private transferService:     StockTransferService,
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

  openShipDialog(transfer: StockTransfer): void {
    this.shippingTransfer = transfer;
    this.shipItems = transfer.items.map(i => ({
      ...i,
      shippedQuantity: i.requestedQuantity
    }));
    this.shipDialogVisible = true;
  }

  onShipSubmit(): void {
    if (!this.shippingTransfer) return;
    this.isSubmitting = true;

    this.transferService.ship(this.shippingTransfer.id, {
      id:    this.shippingTransfer.id,
      items: this.shipItems.map(i => ({
        variantId:       i.variantId,   // ✅
        shippedQuantity: i.shippedQuantity
      }))
    }).subscribe({
      next: () => {
        this.isSubmitting      = false;
        this.shipDialogVisible = false;
        this.loadTransfers();
        this.messageService.add({
          severity: 'success', summary: 'Gönderildi',
          detail: 'Mağaza teslim alabilir.'
        });
      },
      error: (err) => {
        this.isSubmitting = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Gönderme işlemi başarısız.')
        });
      }
    });
  }

  confirmCancel(transfer: StockTransfer): void {
    this.confirmationService.confirm({
      message:    `"${transfer.transferNumber}" iptal edilsin mi?`,
      header:     'İptal Et',
      icon:       'pi pi-exclamation-triangle',
      acceptLabel: 'Evet',
      rejectLabel: 'Vazgeç',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.transferService.cancel(transfer.id).subscribe({
          next: () => {
            this.loadTransfers();
            this.messageService.add({
              severity: 'info', summary: 'İptal Edildi',
              detail: transfer.transferNumber
            });
          },
          error: (err) => this.messageService.add({
            severity: 'error', summary: 'Hata',
            detail: apiErrorMessage(err, 'Sevkiyat iptal edilemedi.')
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
      Draft: 'bg-gray-100 text-gray-600',
      Shipped: 'bg-blue-100 text-blue-700',
      Received: 'bg-emerald-100 text-emerald-700',
      Cancelled: 'bg-red-100 text-red-600'
    };
    return map[status] ?? 'bg-gray-100 text-gray-600';
  }
}