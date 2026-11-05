import { Component, Input, Output, EventEmitter, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Product, ProductVariant } from '../../../../shared/models/product.model';
import { ProductService } from '../../../../core/services/product.service';
import { apiErrorMessage } from '../../../../core/utils/api.util';

import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';

@Component({
  selector: 'app-price-update-modal',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    DialogModule, ButtonModule, InputNumberModule
  ],
  template: `
    <p-dialog
      [header]="'Fiyat Güncelle — ' + product.name"
      [(visible)]="visible"
      (visibleChange)="visibleChange.emit($event)"
      [modal]="true"
      [style]="{ width: '420px' }"
      [draggable]="false">

      <div class="py-2 space-y-4">

        <!-- Varyant Bilgisi -->
        @if (variant) {
          <div class="bg-gray-50 rounded-xl p-4 flex items-center gap-3">
            <div class="w-10 h-10 bg-purple-100 rounded-xl flex items-center
                        justify-center flex-shrink-0">
              <i class="pi pi-tag text-purple-500"></i>
            </div>
            <div>
              <p class="text-xs text-gray-400 font-medium">Varyant</p>
              <div class="flex items-center gap-2 mt-0.5">
                <span class="font-mono text-sm text-gray-700 font-semibold">
                  {{ variant.barcode }}
                </span>
                @if (variant.color) {
                  <span class="text-xs bg-blue-100 text-blue-700
                               px-2 py-0.5 rounded-full">
                    {{ variant.color }}
                  </span>
                }
                @if (variant.size) {
                  <span class="text-xs bg-gray-200 text-gray-700
                               px-2 py-0.5 rounded-full font-semibold">
                    {{ variant.size }}
                  </span>
                }
              </div>
            </div>
          </div>
        }

        <!-- Mevcut Fiyat -->
        <div class="bg-gray-50 rounded-xl p-4">
          <p class="text-xs text-gray-500 uppercase font-semibold mb-1">
            Mevcut Fiyat
          </p>
          @if (variant && variant.currentPrice > 0) {
            <p class="text-2xl font-bold text-gray-800">
              {{ variant.currentPrice | currency:'TRY':'symbol':'1.2-2' }}
            </p>
          } @else {
            <p class="text-sm text-red-400 font-medium">
              ⚠️ Henüz fiyat tanımlanmamış
            </p>
          }
        </div>

        <!-- Yeni Fiyat -->
        <form [formGroup]="priceForm">
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Yeni Fiyat (₺)
          </label>
          <p-inputNumber
            formControlName="newPrice"
            mode="currency"
            currency="TRY"
            locale="tr-TR"
            [minFractionDigits]="2"
            [min]="0.01"
            styleClass="w-full"
            inputStyleClass="w-full"
            placeholder="0,00" />

          @if (priceForm.get('newPrice')?.invalid &&
               priceForm.get('newPrice')?.touched) {
            <p class="text-red-500 text-xs mt-1">
              Geçerli bir fiyat giriniz.
            </p>
          }
          @if (errorMessage) {
            <p class="text-red-600 text-sm mt-2">
              <i class="pi pi-exclamation-triangle mr-1"></i>{{ errorMessage }}
            </p>
          }
        </form>

      </div>

      <ng-template pTemplate="footer">
        <div class="flex gap-2 justify-end">
          <p-button
            label="İptal"
            severity="secondary"
            (onClick)="visibleChange.emit(false)" />
          <p-button
            label="Fiyatı Güncelle"
            icon="pi pi-check"
            [loading]="isLoading"
            [disabled]="priceForm.invalid || isLoading"
            (onClick)="onSubmit()" />
        </div>
      </ng-template>

    </p-dialog>
  `
})
export class PriceUpdateModalComponent implements OnChanges {
  @Input() product!: Product;
  @Input() variant!: ProductVariant;  // ✅ Yeni
  @Input() visible  = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() priceUpdated  = new EventEmitter<void>();

  priceForm: FormGroup;
  isLoading = false;
  errorMessage = '';

  constructor(
    private fb:             FormBuilder,
    private productService: ProductService
  ) {
    this.priceForm = this.fb.group({
      newPrice: [null, [Validators.required, Validators.min(0.01)]]
    });
  }

  ngOnChanges(): void {
    if (this.visible) {
      this.priceForm.reset();
      this.errorMessage = '';
    }
  }

  onSubmit(): void {
    if (this.priceForm.invalid || !this.variant) return;
    this.isLoading = true;

    // ✅ variantId ile fiyat güncelle
    this.productService.updateVariantPrice(this.variant.id, {
      variantId: this.variant.id,
      newPrice:  this.priceForm.value.newPrice
    }).subscribe({
      next: () => {
        this.isLoading = false;
        this.priceUpdated.emit();
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = apiErrorMessage(err, 'Fiyat güncellenemedi.');
      }
    });
  }
}