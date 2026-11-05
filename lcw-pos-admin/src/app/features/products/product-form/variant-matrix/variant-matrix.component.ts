import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProductService } from '../../../../core/services/product.service';
import { apiErrorMessage } from '../../../../core/utils/api.util';
import { ProductVariant } from '../../../../shared/models/product.model';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

/**
 * Renk x Beden matrisi. Varyantlar SUNUCUDA uretilir:
 *  - barkodlar EAN-13 olarak sayactan (cakismasiz),
 *  - urunde zaten olan renk/beden kombinasyonlari atlanir,
 *  - her renk otomatik olarak kendi renk grubuna baglanir (renk gorselleri).
 * (Eskiden barkodlar tarayicida rastgele uretilip varyantlar tek tek
 *  gonderiliyor, renk gruplari istemcide kuruluyordu.)
 */
@Component({
  selector: 'app-variant-matrix',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, InputNumberModule, ToastModule, TooltipModule],
  providers: [MessageService],
  template: `
    <div class="space-y-5">
      <div class="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
        <h4 class="text-sm font-bold text-gray-700 uppercase tracking-wide mb-1">
          <i class="pi pi-th-large text-red-500 mr-2"></i>Renk x Beden Matrisi
        </h4>
        <p class="text-xs text-gray-400 mb-4">
          Renkleri ve bedenleri girin (Enter ya da virgül). Barkodlar sistem tarafından üretilir;
          üründe zaten olan kombinasyonlar atlanır.
        </p>

        <!-- Renkler -->
        <label class="block text-xs font-semibold text-gray-500 mb-1">RENKLER</label>
        <div class="flex flex-wrap items-center gap-2 p-2 bg-gray-50 rounded-xl border border-gray-200 min-h-10 mb-4">
          @for (c of colors; track c; let i = $index) {
            <span class="inline-flex items-center gap-1.5 bg-blue-100 text-blue-700 text-sm px-3 py-1 rounded-full">
              {{ c }}
              <button (click)="colors.splice(i, 1)" class="hover:text-red-500"><i class="pi pi-times text-xs"></i></button>
            </span>
          }
          <input type="text" [(ngModel)]="colorInput" placeholder="Siyah, Lacivert..."
                 class="flex-1 min-w-24 bg-transparent text-sm outline-none px-2 py-1"
                 (keydown.enter)="addColor()" (keydown.comma)="addColor(); $event.preventDefault()"
                 (blur)="addColor()" />
        </div>

        <!-- Bedenler -->
        <div class="flex items-center justify-between mb-1">
          <label class="block text-xs font-semibold text-gray-500">BEDENLER</label>
          <div class="flex gap-2">
            @for (preset of sizePresets; track preset.label) {
              <button (click)="applySizePreset(preset.sizes)"
                class="text-xs text-blue-600 hover:text-blue-800 bg-blue-50 px-2 py-0.5 rounded-lg">
                {{ preset.label }}
              </button>
            }
          </div>
        </div>
        <div class="flex flex-wrap items-center gap-2 p-2 bg-gray-50 rounded-xl border border-gray-200 min-h-10 mb-4">
          @for (s of sizes; track s; let i = $index) {
            <span class="inline-flex items-center gap-1.5 bg-gray-200 text-gray-700 text-sm px-3 py-1 rounded-full font-semibold">
              {{ s }}
              <button (click)="sizes.splice(i, 1)" class="hover:text-red-500"><i class="pi pi-times text-xs"></i></button>
            </span>
          }
          <input type="text" [(ngModel)]="sizeInput" placeholder="S, M, L, 38..."
                 class="flex-1 min-w-24 bg-transparent text-sm outline-none px-2 py-1"
                 (keydown.enter)="addSize()" (keydown.comma)="addSize(); $event.preventDefault()"
                 (blur)="addSize()" />
        </div>

        <!-- Fiyat -->
        <div class="grid grid-cols-2 gap-4">
          <div>
            <label class="block text-xs font-semibold text-gray-500 mb-1">VARSAYILAN FİYAT (₺) *</label>
            <p-inputNumber [(ngModel)]="price" mode="currency" currency="TRY" locale="tr-TR"
                           [minFractionDigits]="2" [min]="0.01" styleClass="w-full" inputStyleClass="w-full" />
          </div>
          @if (colors.length > 1) {
            <div>
              <label class="block text-xs font-semibold text-gray-500 mb-1">RENK BAZLI FİYAT (opsiyonel)</label>
              <div class="space-y-1">
                @for (c of colors; track c) {
                  <div class="flex items-center gap-2">
                    <span class="text-sm w-24 truncate">{{ c }}</span>
                    <p-inputNumber [(ngModel)]="colorPrices[c]" mode="currency" currency="TRY" locale="tr-TR"
                                   [minFractionDigits]="2" [min]="0" placeholder="varsayılan"
                                   inputStyleClass="w-32 text-sm" />
                  </div>
                }
              </div>
            </div>
          }
        </div>
      </div>

      <!-- Önizleme -->
      @if (colors.length && sizes.length) {
        <div class="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
          <div class="flex items-center justify-between mb-3">
            <h4 class="text-sm font-bold text-gray-700 uppercase tracking-wide">
              <i class="pi pi-table text-purple-500 mr-2"></i>Önizleme
            </h4>
            <span class="text-xs text-gray-500">
              <b class="text-emerald-600">{{ newCount }}</b> yeni ·
              <b class="text-gray-400">{{ existingCount }}</b> mevcut (atlanır)
            </span>
          </div>
          <div class="overflow-x-auto">
            <table class="text-sm border-collapse">
              <thead>
                <tr>
                  <th class="px-3 py-2 text-left text-xs text-gray-400">Renk \\ Beden</th>
                  @for (s of sizes; track s) {
                    <th class="px-3 py-2 text-xs font-semibold text-gray-600">{{ s }}</th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (c of colors; track c) {
                  <tr>
                    <td class="px-3 py-2 font-medium">{{ c }}</td>
                    @for (s of sizes; track s) {
                      <td class="px-3 py-2 text-center">
                        @if (exists(c, s)) {
                          <span class="text-xs bg-gray-100 text-gray-400 px-2 py-1 rounded" pTooltip="Üründe zaten var">mevcut</span>
                        } @else {
                          <span class="text-xs bg-emerald-100 text-emerald-700 px-2 py-1 rounded">
                            {{ priceFor(c) | number:'1.2-2' }} ₺
                          </span>
                        }
                      </td>
                    }
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <div class="flex justify-end mt-4">
            <button (click)="onSave()" [disabled]="newCount === 0 || !price || isSaving"
              class="inline-flex items-center gap-2 bg-emerald-500 hover:bg-emerald-600 disabled:opacity-50
                     text-white px-5 py-2.5 rounded-xl text-sm font-medium transition-colors">
              @if (isSaving) { <i class="pi pi-spin pi-spinner"></i> } @else { <i class="pi pi-bolt"></i> }
              {{ newCount }} Varyantı Üret ve Kaydet
            </button>
          </div>
        </div>
      }

      @if (lastResult.length) {
        <div class="bg-emerald-50 rounded-2xl border border-emerald-200 p-4">
          <p class="text-sm font-semibold text-emerald-700 mb-2">Son üretilen varyantlar</p>
          <div class="grid grid-cols-3 gap-2 text-xs">
            @for (v of lastResult; track v.id) {
              <span class="font-mono bg-white rounded-lg px-2 py-1 border border-emerald-100">
                {{ v.color }} / {{ v.size }} — {{ v.barcode }}
              </span>
            }
          </div>
        </div>
      }
    </div>
    <p-toast position="top-right" />
  `
})
export class VariantMatrixComponent {
  @Input() productId!: number;
  @Input() existingVariants: ProductVariant[] = [];
  @Output() saved = new EventEmitter<void>();

  colors: string[] = [];
  sizes:  string[] = [];
  colorInput = '';
  sizeInput  = '';
  price: number | null = null;
  colorPrices: Record<string, number | null> = {};
  isSaving = false;
  lastResult: { id: number; barcode: string; color?: string; size?: string }[] = [];

  readonly sizePresets = [
    { label: 'XS-XXL', sizes: ['XS', 'S', 'M', 'L', 'XL', 'XXL'] },
    { label: 'Jean 28-36', sizes: ['28', '29', '30', '31', '32', '33', '34', '36'] },
    { label: 'Çocuk 10-14', sizes: ['10-11', '11-12', '12-13', '13-14'] },
    { label: 'Bebek', sizes: ['0-3 AY', '3-6 AY', '6-9 AY', '9-12 AY', '12-18 AY'] },
  ];

  constructor(private productService: ProductService, private messageService: MessageService) {}

  private static normColor(v: string): string {
    const t = v.trim();
    return t ? t[0].toLocaleUpperCase('tr') + t.slice(1) : '';
  }

  addColor(): void {
    for (const part of this.colorInput.split(',')) {
      const c = VariantMatrixComponent.normColor(part);
      if (c && !this.colors.some(x => x.toLocaleLowerCase('tr') === c.toLocaleLowerCase('tr')))
        this.colors.push(c);
    }
    this.colorInput = '';
  }

  addSize(): void {
    for (const part of this.sizeInput.split(',')) {
      const s = part.trim().toUpperCase();
      if (s && !this.sizes.includes(s)) this.sizes.push(s);
    }
    this.sizeInput = '';
  }

  applySizePreset(sizes: string[]): void {
    for (const s of sizes) if (!this.sizes.includes(s)) this.sizes.push(s);
  }

  exists(color: string, size: string): boolean {
    const c = color.toLocaleLowerCase('tr'), s = size.toUpperCase();
    return this.existingVariants.some(v =>
      (v.color ?? '').toLocaleLowerCase('tr') === c && (v.size ?? '').toUpperCase() === s);
  }

  priceFor(color: string): number {
    const p = this.colorPrices[color];
    return p && p > 0 ? p : (this.price ?? 0);
  }

  get existingCount(): number {
    let n = 0;
    for (const c of this.colors) for (const s of this.sizes) if (this.exists(c, s)) n++;
    return n;
  }

  get newCount(): number { return this.colors.length * this.sizes.length - this.existingCount; }

  async onSave(): Promise<void> {
    if (!this.price || this.newCount === 0) return;
    this.isSaving = true;
    try {
      const colorPrices: Record<string, number> = {};
      for (const c of this.colors) {
        const p = this.colorPrices[c];
        if (p && p > 0) colorPrices[c] = p;
      }
      const res = await this.productService.generateVariants(this.productId, {
        productId: this.productId,
        colors: this.colors,
        sizes: this.sizes,
        price: this.price,
        colorPrices
      }).toPromise();

      this.lastResult = res?.variants ?? [];
      this.messageService.add({
        severity: 'success', summary: 'Varyantlar üretildi',
        detail: `${res?.created ?? 0} yeni varyant · ${res?.skipped ?? 0} mevcut atlandı. Barkodlar otomatik üretildi.`
      });
      this.saved.emit();
    } catch (err) {
      this.messageService.add({
        severity: 'error', summary: 'Hata',
        detail: apiErrorMessage(err, 'Varyantlar üretilemedi.')
      });
    } finally {
      this.isSaving = false;
    }
  }
}
