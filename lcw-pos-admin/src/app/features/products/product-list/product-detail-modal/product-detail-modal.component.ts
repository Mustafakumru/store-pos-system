import { Component, Input, Output, EventEmitter } from '@angular/core';
import { assetUrl } from '../../../../core/utils/api.util';
import { CommonModule } from '@angular/common';
import { Product } from '../../../../shared/models/product.model';
import { environment } from '../../../../environments/environment';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-product-detail-modal',
  standalone: true,
  imports: [CommonModule, DialogModule, TagModule, ButtonModule],
  template: `
    <p-dialog
      [visible]="visible"
      (visibleChange)="visibleChange.emit($event)"
      [modal]="true"
      [closable]="true"
      [draggable]="false"
      [style]="{ width: '520px' }"
      styleClass="p-0 overflow-hidden rounded-2xl">

      @if (product) {
        <!-- Header -->
        <ng-template pTemplate="header">
          <div class="flex items-center gap-3">
            <div class="w-8 h-8 bg-blue-50 rounded-lg flex items-center justify-center">
              <i class="pi pi-box text-blue-500"></i>
            </div>
            <span class="font-bold text-gray-800">Ürün Detayı</span>
          </div>
        </ng-template>

        <div class="p-6 space-y-6">

          <!-- Ürün Resim + Temel Bilgi -->
          <div class="flex gap-5">
            <div class="flex-shrink-0">
              @if (product.imagePath) {
                <img
                  [src]="getImageUrl(product.imagePath)"
                  [alt]="product.name"
                  class="w-28 h-28 object-cover rounded-2xl border border-gray-100 shadow-sm" />
              } @else {
                <div class="w-28 h-28 bg-gray-100 rounded-2xl flex flex-col
                            items-center justify-center text-gray-300">
                  <i class="pi pi-image text-3xl mb-1"></i>
                  <span class="text-xs">Resim Yok</span>
                </div>
              }
            </div>

            <div class="flex-1 space-y-2">
              <div>
                <h3 class="text-xl font-bold text-gray-800">{{ product.name }}</h3>
                <p class="text-gray-400 text-sm mt-0.5">{{ product.categoryName }}</p>
              </div>

              <!-- Fiyat -->
              <div class="bg-gray-50 rounded-xl px-4 py-3">
                <p class="text-xs text-gray-400 font-medium uppercase tracking-wide">
                  Güncel Fiyat
                </p>
                @if (product.currentPrice > 0) {
                  <p class="text-2xl font-bold text-gray-800 mt-0.5">
                    {{ product.currentPrice | currency:'TRY':'symbol':'1.2-2' }}
                  </p>
                } @else {
                  <p class="text-sm text-red-400 font-medium mt-0.5">
                    ⚠️ Fiyat tanımlanmamış
                  </p>
                }
              </div>

              <!-- Durum -->
              <div class="flex items-center gap-2">
                <p-tag
                  [value]="product.isActive ? 'Aktif' : 'Pasif'"
                  [severity]="product.isActive ? 'success' : 'danger'"
                  [rounded]="true" />
              </div>
            </div>
          </div>

          <!-- Detay Bilgiler -->
          <div class="border border-gray-100 rounded-2xl overflow-hidden">
            <div class="grid grid-cols-2 divide-x divide-gray-100">

              <div class="p-4 bg-gray-50">
                <p class="text-xs text-gray-400 font-medium uppercase tracking-wide">
                  Barkod
                </p>
                <p class="font-mono text-sm font-semibold text-gray-700 mt-1">
                  {{ product.barcode }}
                </p>
              </div>

              <div class="p-4">
                <p class="text-xs text-gray-400 font-medium uppercase tracking-wide">
                  Kategori
                </p>
                <p class="text-sm font-semibold text-gray-700 mt-1">
                  {{ product.categoryName }}
                </p>
              </div>

            </div>

            <div class="border-t border-gray-100 grid grid-cols-2 divide-x divide-gray-100">

              <div class="p-4 bg-gray-50">
                <p class="text-xs text-gray-400 font-medium uppercase tracking-wide">
                  Eklenme Tarihi
                </p>
                <p class="text-sm font-semibold text-gray-700 mt-1">
                  {{ product.createdAt | date:'dd.MM.yyyy HH:mm' }}
                </p>
              </div>

              <div class="p-4">
                <p class="text-xs text-gray-400 font-medium uppercase tracking-wide">
                  Ekleyen
                </p>
                <p class="text-sm font-semibold text-gray-700 mt-1">
                  {{ product.createdBy ?? '-' }}
                </p>
              </div>

            </div>
          </div>

        </div>

        <!-- Footer -->
        <ng-template pTemplate="footer">
          <div class="flex justify-end gap-2 px-6 pb-4">
            <p-button
              label="Kapat"
              severity="secondary"
              [outlined]="true"
              (onClick)="visibleChange.emit(false)" />
            <p-button
              label="Düzenle"
              icon="pi pi-pencil"
              severity="info"
              (onClick)="onEdit()" />
          </div>
        </ng-template>
      }

    </p-dialog>
  `
})
export class ProductDetailModalComponent {
  @Input() product: Product | null = null;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() editClicked   = new EventEmitter<Product>();

  getImageUrl(path?: string | null): string {
    return assetUrl(path);
  }

  onEdit(): void {
    if (this.product) {
      this.editClicked.emit(this.product);
      this.visibleChange.emit(false);
    }
  }
}