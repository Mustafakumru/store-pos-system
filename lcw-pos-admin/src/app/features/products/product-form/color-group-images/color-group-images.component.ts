import { Component, Input, OnChanges, OnDestroy, SimpleChanges } from '@angular/core';
import { apiErrorMessage } from '../../../../core/utils/api.util';
import { assetUrl } from '../../../../core/utils/api.util';
import { CommonModule } from '@angular/common';
import { ProductService } from '../../../../core/services/product.service';
import { VariantGroup, GroupImage, GroupVariant } from '../../../../shared/models/product.model';
import { environment } from '../../../../environments/environment';

import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService, ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

@Component({
  selector: 'app-color-group-images',
  standalone: true,
  imports: [
    CommonModule, ButtonModule, ToastModule,
    TooltipModule, ConfirmDialogModule
  ],
 
  providers: [MessageService, ConfirmationService],
  template: `
    <div class="space-y-4">

      @if (isLoading) {
        <div class="flex items-center justify-center py-8 text-gray-300">
          <i class="pi pi-spin pi-spinner text-2xl mr-2"></i>
          <span class="text-sm">Yükleniyor...</span>
        </div>
      } @else if (groups.length === 0) {
        <div class="text-center py-8 text-gray-300">
          <i class="pi pi-palette text-4xl mb-3 block"></i>
          <p class="text-sm font-medium">Henüz renk grubu yok</p>
          <p class="text-xs mt-1">
            Matrix Generator ile varyant ekleyince gruplar otomatik oluşur
          </p>
        </div>
      } @else {
        @for (group of groups; track group.id) {
          <div class="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">

            <!-- Grup Başlığı -->
            <div class="flex items-center justify-between mb-4">
              <div class="flex items-center gap-3">
                <div class="w-8 h-8 rounded-xl flex items-center justify-center
                            font-bold text-sm text-white"
                     [style.background]="getColorBg(group.groupName)">
                  {{ group.groupName[0] }}
                </div>
                <div>
                  <h4 class="font-bold text-gray-800">{{ group.groupName }}</h4>
                  <p class="text-xs text-gray-400">
                    {{ group.variants.length }} varyant
                    ({{ getVariantSizes(group.variants) }})
                  </p>
                </div>
              </div>

              <!-- Görsel Ekle Butonu -->
              <button
                (click)="fileInputs[group.id].click()"
                class="inline-flex items-center gap-2 text-sm text-blue-600
                       hover:text-blue-700 font-medium bg-blue-50
                       hover:bg-blue-100 px-3 py-1.5 rounded-xl transition-colors">
                <i class="pi pi-plus"></i>
                Görsel Ekle
              </button>
              <input
                type="file"
                accept=".jpg,.jpeg,.png,.webp"
                multiple
                class="hidden"
                #fileInput
                (change)="onFilesSelected($event, group.id)" />
            </div>

            <!-- Görsel Galerisi -->
            @if (group.images.length > 0) {
              <div class="flex flex-wrap gap-3">
                @for (image of group.images; track image.id; let i = $index) {
                  <div class="relative group/img">
                    <img
                      [src]="getImageUrl(image.imagePath)"
                      class="w-20 h-20 object-cover rounded-xl border
                             border-gray-200 cursor-pointer hover:opacity-80
                             transition-opacity"
                      (error)="onImageError($event)" />

                    <!-- Sıra göstergesi -->
                    <span class="absolute top-1 left-1 w-5 h-5 bg-black/50
                                 text-white text-xs rounded-full flex items-center
                                 justify-center font-bold">
                      {{ i + 1 }}
                    </span>

                    <!-- Sil butonu -->
                    <button
                      (click)="deleteImage(image.id, group.id)"
                      class="absolute -top-2 -right-2 w-6 h-6 bg-red-500
                             hover:bg-red-600 text-white rounded-full
                             flex items-center justify-center
                             opacity-0 group-hover/img:opacity-100
                             transition-opacity shadow-sm">
                      <i class="pi pi-times text-xs"></i>
                    </button>
                  </div>
                }

                <!-- Ekle placeholder -->
                <button
                  (click)="fileInputs[group.id].click()"
                  class="w-20 h-20 border-2 border-dashed border-gray-200
                         rounded-xl flex flex-col items-center justify-center
                         text-gray-300 hover:border-blue-300 hover:text-blue-400
                         transition-colors">
                  <i class="pi pi-plus text-lg"></i>
                  <span class="text-xs mt-1">Ekle</span>
                </button>
              </div>
            } @else {
              <div
                (click)="fileInputs[group.id].click()"
                class="border-2 border-dashed border-gray-200 rounded-xl p-6
                       text-center cursor-pointer hover:border-blue-300
                       transition-colors text-gray-300">
                <i class="pi pi-cloud-upload text-2xl mb-2 block"></i>
                <p class="text-sm">
                  {{ group.groupName }} rengi için görsel yükle
                </p>
                <p class="text-xs mt-1">
                  Bu görseller tüm {{ group.groupName }} varyantlarında görünür
                </p>
              </div>
            }

            <!-- Yükleniyor göstergesi -->
            @if (uploadingGroups.has(group.id)) {
              <div class="flex items-center gap-2 mt-3 text-blue-500 text-sm">
                <i class="pi pi-spin pi-spinner"></i>
                Yükleniyor...
              </div>
            }

          </div>
        }
      }

    </div>

    <p-toast position="top-right" />
    <p-confirmDialog />
  `
})

export class ColorGroupImagesComponent implements OnChanges, OnDestroy {
  @Input() productId!: number;

  groups:         VariantGroup[] = [];
  isLoading       = false;
  uploadingGroups = new Set<number>();
  fileInputs:     Record<number, HTMLInputElement> = {};

  constructor(
    private productService:      ProductService,
    private messageService:      MessageService,
    private confirmationService: ConfirmationService
  ) {}
 getVariantSizes(variants: GroupVariant[]): string {
  return variants
    .map(v => v.size)
    .filter(s => !!s)
    .join(', ');
}
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['productId'] && this.productId) this.loadGroups();
  }

  ngOnDestroy(): void { this.removeFileInputs(); }

  private removeFileInputs(): void {
    for (const input of Object.values(this.fileInputs)) input.remove();
    this.fileInputs = {};
  }

  loadGroups(): void {
    if (!this.productId) return;
    this.isLoading = true;
    this.productService.getVariantGroups(this.productId).subscribe({
      next: data => {
        this.groups    = data;
        this.isLoading = false;
        // File input referanslarını oluştur
        setTimeout(() => this.initFileInputs(), 100);
      },
      error: () => { this.isLoading = false; }
    });
  }

  initFileInputs(): void {
    this.removeFileInputs();
    for (const group of this.groups) {
      const input = document.createElement('input');
      input.type     = 'file';
      input.accept   = '.jpg,.jpeg,.png,.webp';
      input.multiple = true;
      input.style.display = 'none';
      input.addEventListener('change', (e) =>
        this.onFilesSelected(e as any, group.id));
      document.body.appendChild(input);
      this.fileInputs[group.id] = input;
    }
  }

  async onFilesSelected(event: Event, groupId: number): Promise<void> {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;

    this.uploadingGroups.add(groupId);
    const files = Array.from(input.files);

    try {
      // Sıra numarası mevcut görsellerin arkasından devam eder
      // (eskiden her yüklemede 0'dan başlıyor, sıralar çakışıyordu).
      const startOrder = this.groups.find(g => g.id === groupId)?.images.length ?? 0;
      for (let i = 0; i < files.length; i++) {
        await this.productService
          .addImageToGroup(groupId, files[i], startOrder + i)
          .toPromise();
      }

      this.loadGroups();
      this.messageService.add({
        severity: 'success', summary: 'Başarılı',
        detail:   `${files.length} görsel yüklendi.`
      });
    } catch (err) {
      this.loadGroups();
      this.messageService.add({
        severity: 'error', summary: 'Hata',
        detail: apiErrorMessage(err, 'Görsel yüklenirken hata oluştu.')
      });
    } finally {
      this.uploadingGroups.delete(groupId);
      input.value = '';
    }
  }

  deleteImage(imageId: number, groupId: number): void {
    this.confirmationService.confirm({
      message:    'Bu görseli silmek istediğinize emin misiniz?',
      header:     'Görseli Sil',
      icon:       'pi pi-exclamation-triangle',
      acceptLabel: 'Evet, Sil',
      rejectLabel: 'İptal',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.productService.deleteGroupImage(imageId).subscribe({
          next: () => {
            this.loadGroups();
            this.messageService.add({
              severity: 'success', summary: 'Silindi',
              detail: 'Görsel kaldırıldı.'
            });
          },
          error: (err) => this.messageService.add({
            severity: 'error', summary: 'Hata',
            detail: apiErrorMessage(err, 'Görsel silinemedi.')
          })
        });
      }
    });
  }

  getImageUrl(path?: string | null): string {
    return assetUrl(path);
  }

  onImageError(event: Event): void {
    (event.target as HTMLImageElement).style.display = 'none';
  }

  // Renk adına göre arka plan rengi üret
  getColorBg(colorName: string): string {
    const colorMap: Record<string, string> = {
      'Kırmızı': '#ef4444', 'Red':   '#ef4444',
      'Mavi':    '#3b82f6', 'Blue':  '#3b82f6',
      'Siyah':   '#1f2937', 'Black': '#1f2937',
      'Beyaz':   '#9ca3af', 'White': '#9ca3af',
      'Yeşil':   '#22c55e', 'Green': '#22c55e',
      'Sarı':    '#eab308', 'Yellow':'#eab308',
      'Mor':     '#a855f7', 'Purple':'#a855f7',
      'Turuncu': '#f97316', 'Orange':'#f97316',
      'Lacivert':'#1e3a5f', 'Navy':  '#1e3a5f',
      'Gri':     '#6b7280', 'Gray':  '#6b7280',
    };
    return colorMap[colorName] ?? '#6366f1';
  }
}