import { Component, OnInit } from '@angular/core';
import { apiErrorMessage } from '../../../core/utils/api.util';
import { assetUrl } from '../../../core/utils/api.util';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterModule, Router } from '@angular/router';
import { ProductService } from '../../../core/services/product.service';
import { Product, ProductVariant } from '../../../shared/models/product.model';
import { environment } from '../../../environments/environment';
import { PriceUpdateModalComponent } from './price-update-modal/price-update-modal.component';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService, ConfirmationService } from 'primeng/api';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [
    CommonModule, RouterLink, RouterModule,
    TableModule, ButtonModule, TagModule,
    ToastModule, ConfirmDialogModule,
    InputTextModule, TooltipModule,
    PriceUpdateModalComponent
  ],
  providers: [MessageService, ConfirmationService],
  template: `
    <div class="space-y-6">

      <!-- ── Başlık ─────────────────────────────────────────── -->
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-2xl font-bold text-gray-800">Ürün Yönetimi</h2>
          <p class="text-gray-400 text-sm mt-1">
            Toplam
            <span class="font-semibold text-gray-600">{{ products.length }}</span>
            ürün,
            <span class="font-semibold text-gray-600">{{ totalVariants }}</span>
            varyant
          </p>
        </div>
        <a routerLink="/admin/products/new"
           class="inline-flex items-center gap-2 bg-red-500 hover:bg-red-600
                  text-white px-5 py-2.5 rounded-xl transition-colors
                  font-medium text-sm shadow-sm shadow-red-200">
          <i class="pi pi-plus"></i>
          Yeni Ürün
        </a>
      </div>

      <!-- ── İstatistik Kartları ────────────────────────────── -->
      <div class="grid grid-cols-3 gap-4">
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                Toplam Ürün
              </p>
              <p class="text-3xl font-bold text-gray-800 mt-1">
                {{ products.length }}
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
                Aktif
              </p>
              <p class="text-3xl font-bold text-emerald-600 mt-1">
                {{ activeCount }}
              </p>
            </div>
            <div class="w-12 h-12 bg-emerald-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-check-circle text-emerald-500 text-xl"></i>
            </div>
          </div>
        </div>

        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                Toplam Varyant
              </p>
              <p class="text-3xl font-bold text-blue-600 mt-1">
                {{ totalVariants }}
              </p>
            </div>
            <div class="w-12 h-12 bg-purple-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-tags text-purple-500 text-xl"></i>
            </div>
          </div>
        </div>
      </div>

      <!-- ── Tablo ──────────────────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">

        <div class="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
          <span class="text-sm font-semibold text-gray-600">Ürün Listesi</span>
          <div class="relative">
            <i class="pi pi-search absolute left-3 top-1/2 -translate-y-1/2
                       text-gray-400 text-sm"></i>
            <input
              type="text"
              placeholder="Ürün kodu, ad, barkod, renk ara..."
              class="pl-9 pr-4 py-2 text-sm border border-gray-200 rounded-xl
                     focus:outline-none focus:ring-2 focus:ring-red-300 w-64"
              (input)="onSearch($event)" />
          </div>
        </div>

        <p-table
          [value]="filteredProducts"
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
              <th class="text-xs font-semibold text-gray-500 uppercase w-16">
                Görsel
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase">
                Ürün Kodu
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase">
                Ürün Adı
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase">
                Kategori
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                Varyant
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase">
                Durum
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                İşlemler
              </th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-product let-expanded="expanded">
            <tr class="border-b border-gray-50">

              <!-- Genişlet -->
              <td>
                <p-button
                  [icon]="expanded ? 'pi pi-chevron-down' : 'pi pi-chevron-right'"
                  size="small" severity="secondary" [text]="true"
                  (onClick)="toggleRow(product)" />
              </td>

              <!-- Görsel -->
              <td>
                @if (product.imagePath) {
                  <img [src]="getImageUrl(product.imagePath)"
                       [alt]="product.name"
                       class="w-11 h-11 object-cover rounded-xl border border-gray-200"
                       (error)="onImageError($event)" />
                } @else {
                  <div class="w-11 h-11 bg-gray-100 rounded-xl flex
                              items-center justify-center">
                    <i class="pi pi-image text-gray-300 text-lg"></i>
                  </div>
                }
              </td>

              <!-- Urun kodu + siniflandirma -->
              <td>
                @if (product.code) {
                  <p class="font-mono font-bold text-emerald-700 tracking-wider">{{ product.code }}</p>
                  <p class="text-[11px] text-gray-400 mt-0.5">
                    {{ product.seasonLabel }} {{ product.seasonYear }} · {{ product.departmentLabel }}
                  </p>
                } @else {
                  <span class="text-xs text-gray-300">kodsuz</span>
                }
              </td>

              <!-- Ad -->
              <td>
                <p class="font-semibold text-gray-800 text-sm">
                  {{ product.name }}
                </p>
                @if (product.description) {
                  <p class="text-xs text-gray-400 mt-0.5 truncate max-w-48">
                    {{ product.description }}
                  </p>
                }
              </td>

              <!-- Kategori -->
              <td>
                <span class="text-xs bg-blue-50 text-blue-600
                             px-2.5 py-1 rounded-full font-medium">
                  {{ product.categoryName }}
                </span>
              </td>

              <!-- Varyant Sayısı -->
              <td class="text-center">
                <span class="text-sm font-bold text-purple-600 bg-purple-50
                             px-3 py-1 rounded-full">
                  {{ product.variantCount }}
                </span>
              </td>

              <!-- Durum -->
              <td>
                <p-tag
                  [value]="product.isActive ? 'Aktif' : 'Pasif'"
                  [severity]="product.isActive ? 'success' : 'danger'"
                  [rounded]="true" />
              </td>

              <!-- İşlemler -->
              <td>
                <div class="flex items-center justify-center gap-1.5">
                  <a [routerLink]="['/admin/products/edit', product.id]">
                    <p-button icon="pi pi-pencil" size="small"
                      severity="secondary" [rounded]="true"
                      pTooltip="Düzenle" tooltipPosition="top" />
                  </a>
                  <p-button icon="pi pi-plus" size="small"
                    severity="info" [rounded]="true"
                    pTooltip="Varyant Ekle" tooltipPosition="top"
                    (onClick)="navigateToAddVariant(product.id)" />
                  <p-button icon="pi pi-ban" size="small"
                    severity="danger" [rounded]="true"
                    pTooltip="Pasife Al" tooltipPosition="top"
                    (onClick)="confirmDelete(product)" />
                </div>
              </td>

            </tr>
          </ng-template>

          <!-- Varyant Detayları -->
          <ng-template pTemplate="rowexpansion" let-product>
            <tr>
              <td colspan="8" class="bg-gray-50 px-8 py-4">
                <div class="rounded-xl overflow-hidden border border-gray-200">

                  <div class="bg-gray-100 px-4 py-2.5 flex items-center
                              justify-between">
                    <span class="text-xs font-semibold text-gray-500 uppercase">
                      Varyantlar
                    </span>
                  </div>

                  <table class="w-full text-sm">
                    <thead class="bg-white border-b border-gray-100">
                      <tr>
                        <th class="px-4 py-2 text-left text-xs font-semibold
                                   text-gray-500 uppercase">Barkod</th>
                        <th class="px-4 py-2 text-left text-xs font-semibold
                                   text-gray-500 uppercase">Renk</th>
                        <th class="px-4 py-2 text-left text-xs font-semibold
                                   text-gray-500 uppercase">Beden</th>
                        <th class="px-4 py-2 text-center text-xs font-semibold
                                   text-gray-500 uppercase">Fiyat</th>
                        <th class="px-4 py-2 text-center text-xs font-semibold
                                   text-gray-500 uppercase">Durum</th>
                        <th class="px-4 py-2 text-center text-xs font-semibold
                                   text-gray-500 uppercase">İşlemler</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-gray-50 bg-white">
                      @for (variant of product.variants; track variant.id) {
                        <tr class="hover:bg-gray-50">
                          <td class="px-4 py-2">
                            <span class="font-mono text-xs text-gray-600 bg-gray-100
                                         px-2 py-0.5 rounded">
                              {{ variant.barcode }}
                            </span>
                          </td>
                          <td class="px-4 py-2">
                            @if (variant.color) {
                              <span class="text-sm text-gray-700">
                                {{ variant.color }}
                              </span>
                            } @else {
                              <span class="text-gray-300 text-xs">—</span>
                            }
                          </td>
                          <td class="px-4 py-2">
                            @if (variant.size) {
                              <span class="text-xs bg-gray-100 text-gray-700
                                           px-2 py-0.5 rounded font-semibold">
                                {{ variant.size }}
                              </span>
                            } @else {
                              <span class="text-gray-300 text-xs">—</span>
                            }
                          </td>
                          <td class="px-4 py-2 text-center">
                            @if (variant.currentPrice > 0) {
                              <span class="font-bold text-gray-800 text-sm">
                                {{ variant.currentPrice | currency:'TRY':'symbol':'1.2-2' }}
                              </span>
                            } @else {
                              <span class="text-xs text-red-400">⚠️ Fiyat yok</span>
                            }
                          </td>
                          <td class="px-4 py-2 text-center">
                            <p-tag
                              [value]="variant.isActive ? 'Aktif' : 'Pasif'"
                              [severity]="variant.isActive ? 'success' : 'danger'"
                              [rounded]="true" />
                          </td>
                          <td class="px-4 py-2">
                            <div class="flex items-center justify-center gap-1.5">
                              <p-button icon="pi pi-tag" size="small"
                                severity="info" [rounded]="true"
                                pTooltip="Fiyat Güncelle" tooltipPosition="top"
                                (onClick)="openPriceModal(product, variant)" />
                              <p-button icon="pi pi-ban" size="small"
                                severity="danger" [rounded]="true"
                                pTooltip="Varyantı Sil" tooltipPosition="top"
                                (onClick)="confirmDeleteVariant(variant)" />
                            </div>
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
                  <i class="pi pi-box text-5xl mb-4"></i>
                  <p class="text-base font-medium">Ürün bulunamadı</p>
                </div>
              </td>
            </tr>
          </ng-template>

        </p-table>
      </div>

    </div>

    <p-toast position="top-right" />
    <p-confirmDialog />

    @if (selectedVariant) {
      <app-price-update-modal
        [product]="selectedProduct!"
        [variant]="selectedVariant"
        [visible]="priceModalVisible"
        (visibleChange)="priceModalVisible = $event"
        (priceUpdated)="onPriceUpdated()" />
    }
  `
})
export class ProductListComponent implements OnInit {
  products:         Product[] = [];
  filteredProducts: Product[] = [];
  isLoading         = false;
  priceModalVisible = false;
  selectedProduct:  Product | null = null;
  selectedVariant:  ProductVariant | null = null;
  expandedRows:     { [key: number]: boolean } = {};
  private searchTerm = '';

  get activeCount():   number { return this.products.filter(p => p.isActive).length; }
  get totalVariants(): number { return this.products.reduce((a, p) => a + p.variantCount, 0); }

  constructor(
    private productService:      ProductService,
    private messageService:      MessageService,
    private confirmationService: ConfirmationService,
    private router:              Router
  ) {}

  ngOnInit(): void { this.loadProducts(); }

  loadProducts(): void {
    this.isLoading = true;
    this.productService.getAll().subscribe({
      next: data => {
        this.products         = data;
        this.applySearch();
        this.isLoading        = false;
      },
      error: (err) => {
        this.isLoading = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Ürünler yüklenirken hata oluştu.')
        });
      }
    });
  }

  toggleRow(product: Product): void {
    if (this.expandedRows[product.id])
      delete this.expandedRows[product.id];
    else
      this.expandedRows[product.id] = true;
    this.expandedRows = { ...this.expandedRows };
  }

  onSearch(event: Event): void {
    this.searchTerm = (event.target as HTMLInputElement).value.toLowerCase();
    this.applySearch();
  }

  private applySearch(): void {
    const term = this.searchTerm;
    if (!term) { this.filteredProducts = [...this.products]; return; }
    this.filteredProducts = this.products.filter(p =>
      (p.code ?? '').toLowerCase().includes(term) ||
      p.name.toLowerCase().includes(term) ||
      p.categoryName.toLowerCase().includes(term) ||
      p.variants.some(v =>
        v.barcode.toLowerCase().includes(term) ||
        v.color?.toLowerCase().includes(term) ||
        v.size?.toLowerCase().includes(term)
      )
    );
  }

  navigateToAddVariant(productId: number): void {
    this.router.navigate(['/admin/products/edit', productId]);
  }

  openPriceModal(product: Product, variant: ProductVariant): void {
    this.selectedProduct  = product;
    this.selectedVariant  = variant;
    this.priceModalVisible = true;
  }

  onPriceUpdated(): void {
    this.priceModalVisible = false;
    this.loadProducts();
    this.messageService.add({
      severity: 'success', summary: 'Başarılı',
      detail: 'Fiyat güncellendi.'
    });
  }

  confirmDelete(product: Product): void {
    this.confirmationService.confirm({
      message:    `"${product.name}" ürününü pasife almak istediğinize emin misiniz?`,
      header:     'Pasife Al',
      icon:       'pi pi-exclamation-triangle',
      acceptLabel: 'Evet',
      rejectLabel: 'İptal',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.deleteProduct(product.id)
    });
  }

  confirmDeleteVariant(variant: ProductVariant): void {
    this.confirmationService.confirm({
      message:    `"${variant.barcode}" barkodlu varyantı silmek istediğinize emin misiniz?`,
      header:     'Varyantı Sil',
      icon:       'pi pi-exclamation-triangle',
      acceptLabel: 'Evet, Sil',
      rejectLabel: 'İptal',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.deleteVariant(variant.id)
    });
  }

  deleteProduct(id: number): void {
    this.productService.delete(id).subscribe({
      next: () => {
        this.loadProducts();
        this.messageService.add({
          severity: 'success', summary: 'Başarılı',
          detail: 'Ürün pasife alındı.'
        });
      },
      error: (err) => this.messageService.add({
        severity: 'error', summary: 'Hata',
        detail: apiErrorMessage(err, 'İşlem sırasında hata oluştu.')
      })
    });
  }

  deleteVariant(variantId: number): void {
    this.productService.deleteVariant(variantId).subscribe({
      next: () => {
        this.loadProducts();
        this.messageService.add({
          severity: 'success', summary: 'Başarılı',
          detail: 'Varyant silindi.'
        });
      },
      error: (err) => this.messageService.add({
        severity: 'error', summary: 'Hata',
        detail: apiErrorMessage(err, 'Varyant silinirken hata oluştu.')
      })
    });
  }

  getImageUrl(path?: string | null): string {
    return assetUrl(path);
  }

  onImageError(event: Event): void {
    (event.target as HTMLImageElement).style.display = 'none';
  }
}