import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { StoreService } from '../../core/services/store.service';
import { WarehouseService } from '../../core/services/warehouse.service';
import { StockTransferService } from '../../core/services/stock-transfer.service';
import { AuthService } from '../../core/services/auth.service';
import { StockAlert, Store } from '../../shared/models/store.model';
import { WarehouseStock } from '../../shared/models/warehouse.model';
import { StockTransfer } from '../../shared/models/stock-transfer.model';

// PrimeNG
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule, RouterLink,
    TableModule, TagModule,
    ButtonModule, ToastModule
  ],
  providers: [MessageService],
  template: `
    <div class="space-y-6">

      <!-- ── Hoşgeldin ─────────────────────────────────────── -->
      <div class="bg-gradient-to-r from-gray-900 to-gray-800 rounded-2xl
                  p-6 flex items-center justify-between">
        <div>
          <p class="text-gray-400 text-sm">Hoş geldiniz,</p>
          <h2 class="text-2xl font-bold text-white mt-0.5">
            {{ fullName() }}
          </h2>
          <p class="text-gray-500 text-xs mt-1">
            {{ today | date:'EEEE, dd MMMM yyyy' : '' : 'tr' }}
          </p>
        </div>
        <div class="w-16 h-16 bg-red-500 rounded-2xl flex items-center
                    justify-center shadow-lg shadow-red-500/30">
          <i class="pi pi-chart-bar text-white text-2xl"></i>
        </div>
      </div>

      <!-- ── Özet Kartlar ───────────────────────────────────── -->
      <div class="grid grid-cols-4 gap-4">

        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                Aktif Mağaza
              </p>
              <p class="text-3xl font-bold text-gray-800 mt-1">
                {{ activeStoreCount }}
              </p>
            </div>
            <div class="w-12 h-12 bg-blue-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-shop text-blue-500 text-xl"></i>
            </div>
          </div>
          <a routerLink="/admin/stores"
             class="text-xs text-blue-500 hover:underline mt-2 block">
            Mağazaları görüntüle →
          </a>
        </div>

        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                Depo Stok Çeşidi
              </p>
              <p class="text-3xl font-bold text-gray-800 mt-1">
                {{ warehouseStocks.length }}
              </p>
            </div>
            <div class="w-12 h-12 bg-emerald-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-warehouse text-emerald-500 text-xl"></i>
            </div>
          </div>
          <a routerLink="/admin/warehouse"
             class="text-xs text-emerald-500 hover:underline mt-2 block">
            Depoyu görüntüle →
          </a>
        </div>

        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                🔴 Kritik Stok
              </p>
              <p class="text-3xl font-bold text-red-500 mt-1">
                {{ criticalAlerts.length }}
              </p>
            </div>
            <div class="w-12 h-12 bg-red-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-exclamation-triangle text-red-500 text-xl"></i>
            </div>
          </div>
          <p class="text-xs text-red-400 mt-2">
            {{ criticalAlerts.length > 0 ? 'Acil müdahale gerekiyor!' : 'Sorun yok ✓' }}
          </p>
        </div>

        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                🟡 Düşük Stok
              </p>
              <p class="text-3xl font-bold text-orange-500 mt-1">
                {{ lowAlerts.length }}
              </p>
            </div>
            <div class="w-12 h-12 bg-orange-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-bell text-orange-500 text-xl"></i>
            </div>
          </div>
          <p class="text-xs text-orange-400 mt-2">
            {{ lowAlerts.length > 0 ? 'Sevkiyat planlanmalı' : 'Sorun yok ✓' }}
          </p>
        </div>

      </div>

      <div class="grid grid-cols-2 gap-6">

        <!-- ── Stok Uyarıları ─────────────────────────────── -->
        <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">

          <div class="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
            <span class="text-sm font-semibold text-gray-700">
              Stok Uyarıları
            </span>
            @if (isLoadingAlerts) {
              <i class="pi pi-spin pi-spinner text-gray-400"></i>
            }
          </div>

          @if (stockAlerts.length === 0 && !isLoadingAlerts) {
            <div class="flex flex-col items-center justify-center py-12 text-gray-300">
              <i class="pi pi-check-circle text-4xl mb-3"></i>
              <p class="text-sm font-medium">Stok uyarısı yok</p>
            </div>
          } @else {
            <div class="divide-y divide-gray-50 max-h-80 overflow-y-auto">
              <!-- Anahtar varyant+mağaza: eskiden productId + storeId SAYI olarak
                   toplanıyordu (ürün 1/mağaza 2 = ürün 2/mağaza 1 = 3) ve aynı
                   ürünün farklı varyantları da çakışıyordu. -->
              @for (alert of stockAlerts.slice(0, 10); track alert.storeId + '-' + alert.variantId) {
                <div class="px-5 py-3 flex items-center justify-between hover:bg-gray-50">
                  <div class="flex-1 min-w-0">
                    <p class="text-sm font-semibold text-gray-800 truncate">
                      {{ alert.productName }}
                      @if (alert.color || alert.size) {
                        <span class="text-xs font-normal text-gray-400">
                          · {{ alert.color }} {{ alert.size }}
                        </span>
                      }
                    </p>
                    <p class="text-xs text-gray-400 mt-0.5">
                      {{ alert.storeName }} · {{ alert.barcode }}
                    </p>
                  </div>
                  <div class="flex items-center gap-3 ml-3">
                    <span class="text-sm font-bold"
                          [class.text-red-500]="alert.quantity < 0"
                          [class.text-orange-500]="alert.quantity >= 0">
                      {{ alert.quantity }}
                    </span>
                    <span [class]="getAlertClass(alert.alertLevel)"
                          class="text-xs px-2 py-1 rounded-full font-medium whitespace-nowrap">
                      {{ alert.alertLevelText }}
                    </span>
                  </div>
                </div>
              }
            </div>
          }

        </div>

        <!-- ── Son Sevkiyatlar ────────────────────────────── -->
        <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">

          <div class="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
            <span class="text-sm font-semibold text-gray-700">
              Son Sevkiyatlar
            </span>
            <a routerLink="/admin/stock-transfers"
               class="text-xs text-red-500 hover:underline">
              Tümünü gör →
            </a>
          </div>

          @if (recentTransfers.length === 0 && !isLoadingTransfers) {
            <div class="flex flex-col items-center justify-center py-12 text-gray-300">
              <i class="pi pi-truck text-4xl mb-3"></i>
              <p class="text-sm font-medium">Henüz sevkiyat yok</p>
            </div>
          } @else {
            <div class="divide-y divide-gray-50 max-h-80 overflow-y-auto">
              @for (transfer of recentTransfers; track transfer.id) {
                <div class="px-5 py-3 flex items-center justify-between hover:bg-gray-50">
                  <div class="flex-1 min-w-0">
                    <p class="text-sm font-semibold text-gray-800">
                      {{ transfer.storeName }}
                    </p>
                    <p class="text-xs font-mono text-gray-400 mt-0.5">
                      {{ transfer.transferNumber }}
                    </p>
                  </div>
                  <div class="flex items-center gap-3 ml-3">
                    <span class="text-xs text-gray-400">
                      {{ transfer.createdAt | date:'dd.MM.yy' }}
                    </span>
                    <span [class]="getTransferStatusClass(transfer.status)"
                          class="text-xs px-2 py-1 rounded-full font-medium whitespace-nowrap">
                      {{ getTransferStatusLabel(transfer.status) }}
                    </span>
                  </div>
                </div>
              }
            </div>
          }

        </div>

      </div>

      <!-- ── Mağaza Stok Özeti ───────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">

        <div class="px-6 py-4 border-b border-gray-100">
          <span class="text-sm font-semibold text-gray-700">Mağaza Stok Özeti</span>
        </div>

        <div class="grid grid-cols-3 gap-0 divide-x divide-gray-100">
          @for (store of stores.slice(0, 6); track store.id) {
            <div class="p-5 hover:bg-gray-50 transition-colors">
              <div class="flex items-start justify-between">
                <div>
                  <p class="text-sm font-bold text-gray-800 truncate">
                    {{ store.name }}
                  </p>
                  <p class="text-xs font-mono text-gray-400 mt-0.5">
                    {{ store.code }}
                  </p>
                </div>
                <div [class]="store.isActive
                  ? 'w-2 h-2 rounded-full bg-emerald-400 mt-1.5'
                  : 'w-2 h-2 rounded-full bg-gray-300 mt-1.5'">
                </div>
              </div>
              <div class="flex items-center gap-3 mt-3">
                @if (store.criticalStockCount > 0) {
                  <span class="text-xs bg-red-50 text-red-600 px-2 py-0.5 rounded-full">
                    🔴 {{ store.criticalStockCount }}
                  </span>
                }
                @if (store.lowStockCount > 0) {
                  <span class="text-xs bg-orange-50 text-orange-600 px-2 py-0.5 rounded-full">
                    🟡 {{ store.lowStockCount }}
                  </span>
                }
                @if (store.criticalStockCount === 0 && store.lowStockCount === 0) {
                  <span class="text-xs text-gray-300">Stok normal ✓</span>
                }
              </div>
            </div>
          }
        </div>

      </div>

    </div>

    <p-toast position="top-right" />
  `
})
export class DashboardComponent implements OnInit {
  stores:           Store[]         = [];
  stockAlerts:      StockAlert[]    = [];
  warehouseStocks:  WarehouseStock[]= [];
  recentTransfers:  StockTransfer[] = [];

  isLoadingAlerts    = false;
  isLoadingTransfers = false;

  today = new Date();

  get fullName()         { return this.authService.fullName; }
  get activeStoreCount() { return this.stores.filter(s => s.isActive).length; }
  get criticalAlerts()   { return this.stockAlerts.filter(a => a.alertLevel === 3); }
  get lowAlerts()        { return this.stockAlerts.filter(a => a.alertLevel === 1); }

  constructor(
    private storeService:    StoreService,
    private warehouseService: WarehouseService,
    private transferService:  StockTransferService,
    private authService:      AuthService,
    private messageService:   MessageService
  ) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    // Mağazalar
    this.storeService.getAll().subscribe({
      next: data => this.stores = data
    });

    // Stok uyarıları
    this.isLoadingAlerts = true;
    this.storeService.getStockAlerts().subscribe({
      next: data => {
        this.stockAlerts     = data;
        this.isLoadingAlerts = false;
      },
      error: () => { this.isLoadingAlerts = false; }
    });

    // Depo stokları
    this.warehouseService.getStock().subscribe({
      next: data => this.warehouseStocks = data
    });

    // Son sevkiyatlar
    this.isLoadingTransfers = true;
    this.transferService.getAll().subscribe({
      next: data => {
        this.recentTransfers  = data.slice(0, 8);
        this.isLoadingTransfers = false;
      },
      error: () => { this.isLoadingTransfers = false; }
    });
  }

  getAlertClass(level: number): string {
    const map: Record<number, string> = {
      3: 'bg-red-100 text-red-700',
      2: 'bg-gray-100 text-gray-600',
      1: 'bg-orange-100 text-orange-700'
    };
    return map[level] ?? 'bg-gray-100 text-gray-500';
  }

  getTransferStatusClass(status: string): string {
    const map: Record<string, string> = {
      Draft:     'bg-gray-100 text-gray-600',
      Shipped:   'bg-blue-100 text-blue-700',
      Received:  'bg-emerald-100 text-emerald-700',
      Cancelled: 'bg-red-100 text-red-600'
    };
    return map[status] ?? 'bg-gray-100 text-gray-600';
  }

  getTransferStatusLabel(status: string): string {
    const map: Record<string, string> = {
      Draft:     '📝 Taslak',
      Shipped:   '🚚 Yolda',
      Received:  '✅ Teslim',
      Cancelled: '❌ İptal'
    };
    return map[status] ?? status;
  }
}