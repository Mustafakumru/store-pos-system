import { Component, OnInit, HostListener } from '@angular/core';
import { assetUrl, apiErrorMessage } from '../../../core/utils/api.util';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { ProductService } from '../../../core/services/product.service';
import { CategoryService } from '../../../core/services/category.service';
import { environment } from '../../../environments/environment';
import { Category, Product, ProductVariant, ProductCodePreview, Season, Department,
         SEASON_OPTIONS, DEPARTMENT_OPTIONS } from '../../../shared/models/product.model';
import { VariantMatrixComponent } from './variant-matrix/variant-matrix.component';
import { ColorGroupImagesComponent } from './color-group-images/color-group-images.component';

import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService, SharedModule } from 'primeng/api';

interface VariantRow {
  barcode:         string;
  color:           string;
  size:            string;
  initialPrice:    number | null;
  imageFile?:      File;
  imagePreview?:   string;
  imagePath?:      string;
  barcodeDetected: boolean;
}

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule,
    InputTextModule, InputNumberModule,
    DropdownModule, ButtonModule, ToastModule,
    TagModule, TooltipModule, SharedModule,
    VariantMatrixComponent,
    ColorGroupImagesComponent,
  ],
  providers: [MessageService],
  template: `
    <div class="max-w-4xl mx-auto space-y-6">

      <!-- ── Başlık ─────────────────────────────────────────── -->
      <div class="flex items-center gap-3">
        <button (click)="goBack()"
          class="w-9 h-9 rounded-xl bg-gray-100 hover:bg-gray-200
                 flex items-center justify-center transition-colors">
          <i class="pi pi-arrow-left text-gray-600 text-sm"></i>
        </button>
        <div>
          <h2 class="text-2xl font-bold text-gray-800">
            {{ isEditMode ? 'Ürün Düzenle' : 'Yeni Ürün Ekle' }}
          </h2>
          <p class="text-gray-400 text-sm mt-0.5">
            {{ isEditMode
              ? 'Ürün bilgilerini güncelleyin veya yeni varyant ekleyin'
              : 'Ürün bilgilerini ve varyantları girin' }}
          </p>
        </div>
      </div>

      @if (isPageLoading) {
        <div class="bg-white rounded-2xl shadow-sm p-12 flex items-center
                    justify-center text-gray-300">
          <i class="pi pi-spin pi-spinner text-3xl mr-3"></i>
          <span>Yükleniyor...</span>
        </div>
      } @else {

        <!-- ── Tab Navigasyonu ──────────────────────────────── -->
        <div class="bg-white rounded-2xl shadow-sm border border-gray-100
                    p-1 flex gap-1">
          <button
            (click)="activeTab = 'basic'"
            [class]="activeTab === 'basic'
              ? 'bg-red-500 text-white shadow-sm'
              : 'text-gray-500 hover:bg-gray-100'"
            class="flex-1 py-2 px-4 rounded-xl text-sm font-medium
                   transition-colors flex items-center justify-center gap-2">
            <i class="pi pi-box"></i>
            Temel Bilgiler
          </button>

          <button
            (click)="activeTab = 'variants'"
            [class]="activeTab === 'variants'
              ? 'bg-red-500 text-white shadow-sm'
              : 'text-gray-500 hover:bg-gray-100'"
            class="flex-1 py-2 px-4 rounded-xl text-sm font-medium
                   transition-colors flex items-center justify-center gap-2">
            <i class="pi pi-tags"></i>
            Varyantlar
          </button>

          <button
            (click)="activeTab = 'matrix'"
            [class]="activeTab === 'matrix'
              ? 'bg-red-500 text-white shadow-sm'
              : 'text-gray-500 hover:bg-gray-100'"
            class="flex-1 py-2 px-4 rounded-xl text-sm font-medium
                   transition-colors flex items-center justify-center gap-2">
            <i class="pi pi-th-large"></i>
            Toplu Ekle
          </button>

          @if (isEditMode && productId) {
            <button
              (click)="activeTab = 'images'"
              [class]="activeTab === 'images'
                ? 'bg-red-500 text-white shadow-sm'
                : 'text-gray-500 hover:bg-gray-100'"
              class="flex-1 py-2 px-4 rounded-xl text-sm font-medium
                     transition-colors flex items-center justify-center gap-2">
              <i class="pi pi-palette"></i>
              Renk Görselleri
            </button>
          }
        </div>

        <!-- ── Tab: Temel Bilgiler ─────────────────────────── -->
        @if (activeTab === 'basic') {
          <div class="bg-white rounded-2xl shadow-sm p-6">
            <h3 class="text-sm font-bold text-gray-700 uppercase tracking-wide
                       mb-5 flex items-center gap-2">
              <i class="pi pi-box text-red-500"></i>
              Temel Bilgiler
            </h3>

            <form [formGroup]="productForm">
              <div class="grid grid-cols-2 gap-5">

                <div class="col-span-2">
                  <label class="block text-sm font-medium text-gray-700 mb-1">
                    Ürün Adı <span class="text-red-500">*</span>
                  </label>
                  <input pInputText formControlName="name"
                         placeholder="Erkek Slim Fit Pantolon"
                         class="w-full" />
                </div>

                <div class="col-span-2">
                  <label class="block text-sm font-medium text-gray-700 mb-1">
                    Açıklama
                  </label>
                  <input pInputText formControlName="description"
                         placeholder="Ürün açıklaması (opsiyonel)"
                         class="w-full" />
                </div>

                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">
                    Kategori <span class="text-red-500">*</span>
                  </label>
                  <p-dropdown
                    formControlName="categoryId"
                    [options]="categoryOptions"
                    optionLabel="display"
                    optionValue="id"
                    placeholder="Kategori seçin..."
                    styleClass="w-full"
                    [filter]="true" />
                </div>

                <!-- ── Ürün kodu sınıflandırması: kodu belirler, sonradan değişmez ── -->
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">
                    Sezon <span class="text-red-500">*</span>
                  </label>
                  <p-dropdown formControlName="season" [options]="seasonOptions"
                              optionLabel="label" optionValue="value" styleClass="w-full" />
                </div>

                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">
                    Sezon Yılı <span class="text-red-500">*</span>
                  </label>
                  <p-inputNumber formControlName="seasonYear" [useGrouping]="false"
                                 [min]="minYear" [max]="maxYear" [showButtons]="true"
                                 styleClass="w-full" inputStyleClass="w-full" />
                </div>

                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">
                    Bölüm / Yaş Kitlesi <span class="text-red-500">*</span>
                  </label>
                  <p-dropdown formControlName="department" [options]="departmentOptions"
                              optionValue="value" placeholder="Bölüm seçin..." styleClass="w-full">
                    <ng-template let-o pTemplate="item">
                      <span>{{ o.label }}</span>
                      <span class="ml-2 text-xs text-gray-400">{{ o.z }} · {{ o.hint }}</span>
                    </ng-template>
                    <ng-template let-o pTemplate="selectedItem">
                      <span>{{ o.label }} <span class="text-xs text-gray-400">({{ o.z }})</span></span>
                    </ng-template>
                  </p-dropdown>
                </div>

                <!-- Kod önizleme / mevcut kod -->
                <div class="col-span-2 rounded-xl border-2 border-dashed p-4"
                     [class.border-emerald-300]="displayCode" [class.border-gray-200]="!displayCode">
                  <div class="flex items-center gap-4">
                    <div>
                      <p class="text-xs font-semibold text-gray-500 uppercase tracking-wide">
                        {{ isEditMode ? 'Ürün Kodu' : 'Oluşacak Ürün Kodu' }}
                      </p>
                      <p class="font-mono text-3xl font-bold tracking-widest"
                         [class.text-emerald-600]="displayCode" [class.text-gray-300]="!displayCode">
                        {{ displayCode || '--------' }}
                      </p>
                    </div>
                    @if (displayCode) {
                      <div class="flex flex-wrap gap-2 text-xs">
                        <span class="bg-blue-50 text-blue-700 px-2 py-1 rounded-lg">
                          <b class="font-mono">{{ displayCode[0] }}</b> {{ displayCode[0] === 'W' ? 'Kış' : 'Yaz' }}
                        </span>
                        <span class="bg-blue-50 text-blue-700 px-2 py-1 rounded-lg">
                          <b class="font-mono">{{ displayCode[1] }}</b> {{ productForm.get('seasonYear')?.value }}
                        </span>
                        <span class="bg-purple-50 text-purple-700 px-2 py-1 rounded-lg">
                          <b class="font-mono">{{ displayCode.substring(2,4) }}</b> {{ selectedCategoryName }}
                        </span>
                        <span class="bg-gray-100 text-gray-700 px-2 py-1 rounded-lg">
                          <b class="font-mono">{{ displayCode.substring(4,6) }}</b> Model no
                        </span>
                        <span class="bg-amber-50 text-amber-700 px-2 py-1 rounded-lg">
                          <b class="font-mono">{{ displayCode.substring(6,8) }}</b> {{ ageLabel(displayCode.substring(6,8)) }}
                        </span>
                      </div>
                    }
                  </div>
                  <p class="text-xs text-gray-400 mt-2">
                    @if (isEditMode) {
                      Ürün kodu kimliktir; sezon, yıl, kategori ve bölüm oluşturulduktan sonra değiştirilemez.
                    } @else {
                      Kod kaydederken sistem tarafından kesinleştirilir (aynı anda başka ürün açılırsa model no ilerleyebilir).
                    }
                  </p>
                  @if (codePreviewError) {
                    <p class="text-xs text-red-500 mt-1">{{ codePreviewError }}</p>
                  }
                </div>

                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">
                    Ana Görsel
                  </label>
                  <div class="border-2 border-dashed border-gray-200 rounded-xl p-4
                              text-center hover:border-red-400 transition-colors
                              cursor-pointer h-24 flex items-center justify-center"
                       (click)="mainFileInput.click()">
                    @if (mainImagePreview) {
                      <img [src]="mainImagePreview"
                           class="h-full object-contain rounded-lg" />
                    } @else {
                      <div class="text-gray-300">
                        <i class="pi pi-cloud-upload text-2xl block mb-1"></i>
                        <p class="text-xs">Görsel yükle</p>
                      </div>
                    }
                    <input #mainFileInput type="file"
                           accept=".jpg,.jpeg,.png,.webp"
                           class="hidden"
                           (change)="onMainImageSelected($event)" />
                  </div>
                </div>

              </div>
            </form>

            <!-- Kaydet Butonu -->
            <div class="flex gap-3 justify-end mt-6">
              <p-button label="İptal" severity="secondary"
                        type="button" (onClick)="goBack()" />
              <p-button
                [label]="isEditMode ? 'Güncelle' : 'Ürünü Oluştur ve Varyantlara Geç'"
                icon="pi pi-check"
                [loading]="isLoading"
                [disabled]="productForm.invalid || isLoading"
                (onClick)="onSubmitBasic()" />
            </div>
          </div>
        }

        <!-- ── Tab: Varyantlar (Tek Tek Ekleme) ──────────── -->
        @if (activeTab === 'variants' && !isEditMode) {
          <div class="bg-amber-50 rounded-2xl border border-amber-200 p-8 text-center">
            <i class="pi pi-info-circle text-amber-500 text-3xl mb-3 block"></i>
            <p class="text-amber-700 font-semibold">Önce ürünü (modeli) oluşturun</p>
            <p class="text-amber-600 text-sm mt-1">
              Ürün kodu oluştuktan sonra renk ve bedenleri ekleyebilirsiniz.
            </p>
            <button (click)="activeTab = 'basic'"
              class="mt-4 bg-amber-500 hover:bg-amber-600 text-white px-4 py-2 rounded-xl text-sm font-medium">
              Temel Bilgilere Dön
            </button>
          </div>
        }
        @if (activeTab === 'variants' && isEditMode) {
          <div class="bg-white rounded-2xl shadow-sm p-6">

            <!-- Mevcut Varyantlar -->
            @if (isEditMode && existingVariants.length > 0) {
              <div class="mb-6">
                <h3 class="text-sm font-bold text-gray-700 uppercase tracking-wide
                           mb-4 flex items-center gap-2">
                  <i class="pi pi-tags text-purple-500"></i>
                  Mevcut Varyantlar ({{ existingVariants.length }})
                </h3>
                <div class="space-y-2">
                  @for (variant of existingVariants; track variant.id) {
                    <div class="flex items-center gap-4 p-3 bg-gray-50 rounded-xl">
                      <span class="font-mono text-sm text-gray-600 bg-white
                                   border border-gray-200 px-2.5 py-1 rounded-lg w-40">
                        {{ variant.barcode }}
                      </span>
                      @if (variant.color) {
                        <span class="text-xs bg-blue-100 text-blue-700
                                     px-2.5 py-1 rounded-full font-medium">
                          {{ variant.color }}
                        </span>
                      }
                      @if (variant.size) {
                        <span class="text-xs bg-gray-200 text-gray-700
                                     px-2.5 py-1 rounded-full font-semibold">
                          {{ variant.size }}
                        </span>
                      }
                      <span class="font-bold text-gray-800 text-sm ml-auto">
                        {{ variant.currentPrice | currency:'TRY':'symbol':'1.2-2' }}
                      </span>
                      <p-tag
                        [value]="variant.isActive ? 'Aktif' : 'Pasif'"
                        [severity]="variant.isActive ? 'success' : 'danger'"
                        [rounded]="true" />
                    </div>
                  }
                </div>
              </div>
            }

            <!-- Yeni Varyant Ekleme -->
            <div class="flex items-center justify-between mb-4">
              <h3 class="text-sm font-bold text-gray-700 uppercase tracking-wide
                         flex items-center gap-2">
                <i class="pi pi-plus-circle text-emerald-500"></i>
                {{ isEditMode ? 'Yeni Varyant Ekle' : 'Varyantlar' }}
              </h3>
              <button (click)="addVariantRow()"
                class="inline-flex items-center gap-2 text-sm text-emerald-600
                       hover:text-emerald-700 font-medium">
                <i class="pi pi-plus"></i>
                Varyant Ekle
              </button>
            </div>

            @if (variantRows.length === 0) {
              <div class="text-center py-8 text-gray-300">
                <i class="pi pi-tags text-4xl mb-3 block"></i>
                <p class="text-sm">En az bir varyant ekleyin</p>
                <p class="text-xs mt-1">Barkodu okutabilir ya da boş bırakıp sisteme ürettirebilirsiniz</p>
              </div>
            }

            <div class="space-y-4">
              @for (row of variantRows; track row; let i = $index) {
                <div class="border border-gray-100 rounded-xl p-4 bg-gray-50 relative">

                  @if (variantRows.length > 1 || isEditMode) {
                    <button (click)="removeVariantRow(i)"
                      class="absolute top-3 right-3 w-7 h-7 bg-red-100
                             hover:bg-red-200 text-red-500 rounded-lg
                             flex items-center justify-center transition-colors">
                      <i class="pi pi-times text-xs"></i>
                    </button>
                  }

                  <div class="grid grid-cols-4 gap-3">

                    <div class="col-span-4 md:col-span-2">
                      <label class="block text-xs font-medium text-gray-500 mb-1">
                        Barkod <span class="text-gray-400">(boş = sistem üretir)</span>
                      </label>
                      <div class="flex gap-2">
                        <input type="text"
                               [(ngModel)]="row.barcode"
                               placeholder="Okutun ya da boş bırakın"
                               class="flex-1 px-3 py-2 text-sm border border-gray-200
                                      rounded-xl font-mono focus:outline-none
                                      focus:ring-2 focus:ring-red-300"
                               [class.border-green-400]="row.barcodeDetected"
                               (focus)="activeVariantIndex = i" />
                        <button type="button" (click)="generateBarcodeForRow(i)"
                          class="px-3 py-2 bg-gray-200 hover:bg-gray-300 rounded-xl
                                 text-gray-600 transition-colors">
                          <i class="pi pi-refresh text-sm"></i>
                        </button>
                      </div>
                      @if (row.barcodeDetected) {
                        <p class="text-green-600 text-xs mt-1">
                          <i class="pi pi-check-circle"></i> Barkod algılandı!
                        </p>
                      }
                    </div>

                    <div>
                      <label class="block text-xs font-medium text-gray-500 mb-1">
                        Renk
                      </label>
                      <input type="text" [(ngModel)]="row.color"
                             placeholder="Siyah, Lacivert..."
                             class="w-full px-3 py-2 text-sm border border-gray-200
                                    rounded-xl focus:outline-none focus:ring-2
                                    focus:ring-red-300" />
                    </div>

                    <div>
                      <label class="block text-xs font-medium text-gray-500 mb-1">
                        Beden
                      </label>
                      <input type="text" [(ngModel)]="row.size"
                             placeholder="S, M, L, 38..."
                             class="w-full px-3 py-2 text-sm border border-gray-200
                                    rounded-xl focus:outline-none focus:ring-2
                                    focus:ring-red-300" />
                    </div>

                    <div class="col-span-2">
                      <label class="block text-xs font-medium text-gray-500 mb-1">
                        Başlangıç Fiyatı <span class="text-red-500">*</span>
                      </label>
                      <p-inputNumber
                        [(ngModel)]="row.initialPrice"
                        mode="currency" currency="TRY" locale="tr-TR"
                        [minFractionDigits]="2" [min]="0.01"
                        styleClass="w-full" inputStyleClass="w-full"
                        placeholder="0,00" />
                    </div>

                    <div class="col-span-2">
                      <label class="block text-xs font-medium text-gray-500 mb-1">
                        Varyant Görseli (opsiyonel)
                      </label>
                      <div class="border border-dashed border-gray-200 rounded-xl p-2
                                  flex items-center gap-3 cursor-pointer
                                  hover:border-red-300 transition-colors"
                           (click)="triggerVariantFileInput(i)">
                        @if (row.imagePreview) {
                          <img [src]="row.imagePreview"
                               class="w-10 h-10 object-cover rounded-lg" />
                          <span class="text-xs text-gray-500">Değiştir</span>
                        } @else {
                          <div class="w-10 h-10 bg-gray-100 rounded-lg flex
                                      items-center justify-center">
                            <i class="pi pi-image text-gray-300"></i>
                          </div>
                          <span class="text-xs text-gray-400">Görsel ekle</span>
                        }
                      </div>
                      <input type="file" accept=".jpg,.jpeg,.png,.webp"
                             class="hidden"
                             [id]="'variant-file-' + i"
                             (change)="onVariantImageSelected($event, i)" />
                    </div>

                  </div>
                </div>
              }
            </div>

            @if (variantRows.length > 0) {
              <div class="flex justify-end mt-5">
                <p-button
                  [label]="isEditMode ? 'Varyantları Ekle' : 'Ürünü Kaydet'"
                  icon="pi pi-check"
                  [loading]="isLoading"
                  [disabled]="isFormInvalid || isLoading"
                  (onClick)="onSubmit()" />
              </div>
            }

          </div>
        }

        <!-- ── Tab: Toplu Ekle (Matrix Generator) ────────── -->
        @if (activeTab === 'matrix') {
          @if (isEditMode && productId) {
            <app-variant-matrix
              [productId]="productId"
              [existingVariants]="existingVariants"
              (saved)="onMatrixSaved()" />
          } @else {
            <div class="bg-amber-50 rounded-2xl border border-amber-200 p-8
                        text-center">
              <i class="pi pi-info-circle text-amber-500 text-3xl mb-3 block"></i>
              <p class="text-amber-700 font-semibold">
                Önce ürünü kaydedin
              </p>
              <p class="text-amber-600 text-sm mt-1">
                Matrix Generator, ürün oluşturulduktan sonra kullanılabilir.
              </p>
              <button (click)="activeTab = 'basic'"
                class="mt-4 bg-amber-500 hover:bg-amber-600 text-white
                       px-4 py-2 rounded-xl text-sm font-medium">
                Temel Bilgilere Dön
              </button>
            </div>
          }
        }

        <!-- ── Tab: Renk Görselleri ───────────────────────── -->
        @if (activeTab === 'images') {
          @if (isEditMode && productId) {
            <div class="bg-white rounded-2xl shadow-sm p-6">
              <h3 class="text-sm font-bold text-gray-700 uppercase tracking-wide
                         mb-5 flex items-center gap-2">
                <i class="pi pi-palette text-red-500"></i>
                Renk Bazlı Görsel Yönetimi
              </h3>
              <p class="text-xs text-gray-400 mb-4">
                Her renk grubuna görsel ekleyin. Aynı renkteki tüm bedenler
                bu görselleri paylaşır.
              </p>
              <app-color-group-images [productId]="productId" />
            </div>
          }
        }

      }

    </div>
    <p-toast position="top-right" />
  `
})
export class ProductFormComponent implements OnInit {
  productForm!:      FormGroup;
  categories:        Category[]       = [];
  categoryOptions:   (Category & { display: string })[] = [];

  readonly seasonOptions     = SEASON_OPTIONS;
  readonly departmentOptions = DEPARTMENT_OPTIONS;
  readonly minYear = new Date().getFullYear() - 5;
  readonly maxYear = new Date().getFullYear() + 2;

  /** Duzenlemede urunun kodu; yeni urunde canli onizleme. */
  existingCode:      string | null = null;
  codePreview:       ProductCodePreview | null = null;
  codePreviewError:  string | null = null;
  get displayCode(): string | null { return this.existingCode ?? this.codePreview?.code ?? null; }
  get selectedCategoryName(): string {
    const id = this.productForm?.get('categoryId')?.value;
    return this.categories.find(c => c.id === id)?.name ?? '';
  }
  ageLabel(z: string): string {
    return z === 'Z1' ? 'Bebek' : z === 'Z4' ? '10-14 yaş' : 'Yetişkin';
  }
  existingVariants:  ProductVariant[] = [];
  variantRows:       VariantRow[]     = [];

  isEditMode        = false;
  isLoading         = false;
  isPageLoading     = false;
  productId:        number | null     = null;
  // Düzenlenen ürünün mevcut aktiflik durumu - güncellemede korunur
  // (eskiden her güncelleme isActive: true gönderip pasif ürünü aktife alıyordu).
  productIsActive   = true;
  mainImagePreview: string | null     = null;
  mainImageFile:    File | null       = null;
  activeVariantIndex = 0;
  activeTab: 'basic' | 'variants' | 'matrix' | 'images' = 'basic'; // ✅

  private barcodeBuffer  = '';
  private barcodeTimeout: any;

  get isFormInvalid(): boolean {
    if (this.productForm?.invalid) return true;
    if (this.variantRows.length === 0) return true;
    // Barkod opsiyonel (sunucu uretir); renk/beden ve fiyat zorunlu
    if (this.variantRows.some(
      r => !r.color?.trim() || !r.size?.trim() || !r.initialPrice || r.initialPrice <= 0
    )) return true;
    return false;
  }

  constructor(
    private fb:              FormBuilder,
    private productService:  ProductService,
    private categoryService: CategoryService,
    private messageService:  MessageService,
    public  router:          Router,
    private route:           ActivatedRoute
  ) {}

  ngOnInit(): void {
    const idParam   = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!idParam;
    this.productId  = idParam ? +idParam : null;

    const now = new Date();
    // Eylul-Subat arasi varsayilan Kis, Mart-Agustos Yaz
    const month = now.getMonth() + 1;
    const defaultSeason = month >= 3 && month <= 8 ? Season.Summer : Season.Winter;

    this.productForm = this.fb.group({
      name:        ['', [Validators.required, Validators.minLength(2)]],
      description: [''],
      categoryId:  [null, Validators.required],
      season:      [defaultSeason, Validators.required],
      seasonYear:  [now.getFullYear(), [Validators.required, Validators.min(this.minYear), Validators.max(this.maxYear)]],
      department:  [null, Validators.required],
      imagePath:   ['']
    });

    this.loadCategories();

    const tab = this.route.snapshot.queryParamMap.get('tab');
    if (tab === 'matrix' || tab === 'variants' || tab === 'images') this.activeTab = tab;

    if (!this.isEditMode) {
      this.addVariantRow();
      // Kod onizlemesi: siniflandirma degistikce yenilenir
      for (const f of ['categoryId', 'season', 'seasonYear', 'department'])
        this.productForm.get(f)?.valueChanges.subscribe(() => this.refreshCodePreview());
    } else if (this.productId) {
      // Kod kimliktir: sezon/yil/bolum duzenlemede kilitli
      this.productForm.get('season')?.disable();
      this.productForm.get('seasonYear')?.disable();
      this.productForm.get('department')?.disable();
      this.loadProduct(this.productId);
    }
  }

  private previewTimer: any;
  refreshCodePreview(): void {
    clearTimeout(this.previewTimer);
    this.previewTimer = setTimeout(() => {
      const v = this.productForm.getRawValue();
      if (!v.categoryId || !v.season || !v.seasonYear || !v.department) {
        this.codePreview = null;
        return;
      }
      this.productService.previewCode(v.season, v.seasonYear, v.categoryId, v.department).subscribe({
        next: p => { this.codePreview = p; this.codePreviewError = null; },
        error: err => { this.codePreview = null; this.codePreviewError = apiErrorMessage(err, 'Kod önizlenemedi.'); }
      });
    }, 250);
  }

  loadProduct(id: number): void {
    this.isPageLoading = true;
    this.productService.getById(id).subscribe({
      next: product => {
        this.productIsActive = product.isActive;
        this.productForm.patchValue({
          name:        product.name,
          description: product.description ?? '',
          categoryId:  product.categoryId,
          season:      product.season ?? null,
          seasonYear:  product.seasonYear ?? null,
          department:  product.department ?? null,
          imagePath:   product.imagePath ?? ''
        });
        this.existingCode = product.code ?? null;
        // Kodlu urunde kategori de kodun parcasi: degistirilemez
        if (this.existingCode) this.productForm.get('categoryId')?.disable();
        this.existingVariants = product.variants ?? [];
        if (product.imagePath) {
          this.mainImagePreview = assetUrl(product.imagePath);
        }
        this.isPageLoading = false;
      },
      error: (err) => {
        this.isPageLoading = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Ürün bilgileri yüklenemedi.')
        });
      }
    });
  }

  loadCategories(): void {
    this.categoryService.getAll().subscribe({
      next: data => {
        this.categories = data;
        this.categoryOptions = data.map(c => ({ ...c, display: c.code ? `${c.name} (${c.code})` : c.name }));
      }
    });
  }

  // ── Varyant Satır Yönetimi ───────────────────────────────
  addVariantRow(): void {
    this.variantRows.push({
      barcode: '', color: '', size: '',
      initialPrice: null, barcodeDetected: false
    });
  }

  removeVariantRow(index: number): void {
    this.variantRows.splice(index, 1);
  }

  /** Barkodu temizler: kayitta sunucu benzersiz EAN-13 uretir (eskiden tarayicida rastgele uretiliyordu). */
  generateBarcodeForRow(index: number): void {
    this.variantRows[index].barcode = '';
    this.messageService.add({
      severity: 'info', summary: 'Barkod', life: 2000,
      detail: 'Kaydedince sistem benzersiz EAN-13 barkod üretecek.'
    });
  }

  triggerVariantFileInput(index: number): void {
    const input = document.getElementById(`variant-file-${index}`) as HTMLInputElement;
    input?.click();
  }

  // ── Barkod Okuyucu ───────────────────────────────────────
  @HostListener('window:keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    const tag = (event.target as HTMLElement).tagName;
    if (tag === 'INPUT' || tag === 'TEXTAREA') return;

    if (event.key === 'Enter') {
      if (this.barcodeBuffer.length > 4 && this.variantRows.length > 0) {
        const idx = this.activeVariantIndex;
        if (idx < this.variantRows.length) {
          this.variantRows[idx].barcode         = this.barcodeBuffer;
          this.variantRows[idx].barcodeDetected = true;
          setTimeout(() =>
            this.variantRows[idx].barcodeDetected = false, 3000);
        }
      }
      this.barcodeBuffer = '';
      clearTimeout(this.barcodeTimeout);
    } else if (event.key.length === 1) {
      this.barcodeBuffer += event.key;
      clearTimeout(this.barcodeTimeout);
      this.barcodeTimeout = setTimeout(() => this.barcodeBuffer = '', 100);
    }
  }

  // ── Görsel ───────────────────────────────────────────────
  onMainImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    this.mainImageFile = input.files[0];
    const reader = new FileReader();
    reader.onload = e => this.mainImagePreview = e.target?.result as string;
    reader.readAsDataURL(this.mainImageFile);
  }

  onVariantImageSelected(event: Event, index: number): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    this.variantRows[index].imageFile = input.files[0];
    const reader = new FileReader();
    reader.onload = e =>
      this.variantRows[index].imagePreview = e.target?.result as string;
    reader.readAsDataURL(input.files[0]);
  }

  // ── Matrix Kaydedildi ────────────────────────────────────
  onMatrixSaved(): void {
    this.messageService.add({
      severity: 'success', summary: 'Varyantlar Eklendi',
      detail: 'Matrix Generator ile varyantlar başarıyla eklendi.'
    });
    // Renk görselleri tabına geç
    this.activeTab = 'images';
    // Mevcut varyant listesini güncelle
    if (this.productId) this.loadProduct(this.productId);
  }

  // ── Temel Bilgi Kaydet (Yeni Ürün İçin) ─────────────────
  async onSubmitBasic(): Promise<void> {
    if (this.productForm.invalid) return;
    this.isLoading = true;

    try {
      let mainImagePath = this.productForm.value.imagePath ?? '';
      if (this.mainImageFile) {
        const res = await this.productService
          .uploadImage(this.mainImageFile).toPromise();
        mainImagePath = res?.imagePath ?? '';
      }

      const v = this.productForm.value;

      if (this.isEditMode && this.productId) {
        await this.productService.update(this.productId, {
          id:          this.productId,
          name:        v.name,
          description: v.description,
          categoryId:  this.productForm.getRawValue().categoryId,
          imagePath:   mainImagePath,
          isActive:    this.productIsActive
        }).toPromise();

        this.messageService.add({
          severity: 'success', summary: 'Güncellendi',
          detail: 'Temel bilgiler güncellendi.'
        });
        this.isLoading = false;
      } else {
        // Yeni urun (model) olusturulur: kod sunucuda uretilir. Varyantlar
        // ardindan Toplu Ekle (renk x beden) ile eklenir.
        const created = await this.productService.create({
          name:        v.name,
          description: v.description || undefined,
          categoryId:  v.categoryId,
          season:      v.season,
          seasonYear:  v.seasonYear,
          department:  v.department,
          imagePath:   mainImagePath || undefined,
          variants:    []
        }).toPromise();

        this.isLoading = false;
        this.messageService.add({
          severity: 'success', summary: 'Ürün oluşturuldu',
          detail: `Ürün kodu: ${created!.code}. Şimdi renk ve bedenleri ekleyin.`, life: 6000
        });
        this.router.navigate(['/admin/products/edit', created!.id], { queryParams: { tab: 'matrix' } });
      }
    } catch (err) {
      this.isLoading = false;
      this.messageService.add({
        severity: 'error', summary: 'Hata',
        detail: apiErrorMessage(err, 'İşlem sırasında hata oluştu.')
      });
    }
  }

  // ── Varyant Tab Submit ───────────────────────────────────
  async onSubmit(): Promise<void> {
    if (this.isFormInvalid) return;
    this.isLoading = true;

    try {
      let mainImagePath = this.productForm.value.imagePath ?? '';
      if (this.mainImageFile) {
        const res = await this.productService
          .uploadImage(this.mainImageFile).toPromise();
        mainImagePath = res?.imagePath ?? '';
      }

      const v = this.productForm.value;

      if (this.isEditMode && this.productId) {
        const failedRows: VariantRow[] = [];
        const errors: string[] = [];
        let added = 0;
        for (const row of this.variantRows) {
          if (!row.initialPrice) continue;
          try {
            let variantImagePath: string | undefined;
            if (row.imageFile) {
              const imgRes = await this.productService
                .uploadImage(row.imageFile).toPromise();
              variantImagePath = imgRes?.imagePath;
            }
            await this.productService.addVariant(this.productId, {
              productId:    this.productId,
              barcode:      row.barcode || undefined,
              color:        row.color || undefined,
              size:         row.size || undefined,
              imagePath:    variantImagePath,
              initialPrice: row.initialPrice
            }).toPromise();
            added++;
          } catch (err) {
            failedRows.push(row);
            errors.push(`${row.color ?? '-'} / ${row.size ?? '-'}: ${apiErrorMessage(err, 'eklenemedi')}`);
          }
        }
        if (added > 0) {
          this.messageService.add({
            severity: 'success', summary: 'Başarılı',
            detail: `${added} varyant eklendi.`
          });
        }
        if (errors.length) {
          this.messageService.add({
            severity: 'error', summary: 'Eklenemeyen varyantlar',
            detail: errors.join(' | '), life: 10000
          });
        }
        this.loadProduct(this.productId);
        // Başarısız satırlar formda kalır (düzeltip tekrar denenebilir)
        this.variantRows = failedRows;
        if (!this.variantRows.length) this.addVariantRow();

      } else {
        const variantsWithImages = [];
        for (const row of this.variantRows) {
          let variantImagePath: string | undefined;
          if (row.imageFile) {
            const imgRes = await this.productService
              .uploadImage(row.imageFile).toPromise();
            variantImagePath = imgRes?.imagePath;
          }
          variantsWithImages.push({
            barcode:      row.barcode || undefined,
            color:        row.color || undefined,
            size:         row.size || undefined,
            imagePath:    variantImagePath,
            initialPrice: row.initialPrice!
          });
        }
        await this.productService.create({
          name:        v.name,
          description: v.description || undefined,
          categoryId:  v.categoryId,
          season:      v.season,
          seasonYear:  v.seasonYear,
          department:  v.department,
          imagePath:   mainImagePath || undefined,
          variants:    variantsWithImages
        }).toPromise();

        this.messageService.add({
          severity: 'success', summary: 'Başarılı',
          detail: 'Ürün ve varyantlar eklendi.'
        });
        setTimeout(() => this.goBack(), 1500);
      }

      this.isLoading = false;

    } catch (err) {
      this.isLoading = false;
      this.messageService.add({
        severity: 'error', summary: 'Hata',
        detail: apiErrorMessage(err, 'İşlem sırasında hata oluştu.')
      });
    }
  }

  goBack(): void {
    this.router.navigate(['/admin/products']);
  }
}