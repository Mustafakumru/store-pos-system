import { Component, OnInit } from '@angular/core';
import { apiErrorMessage } from '../../core/utils/api.util';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { StoreService } from '../../core/services/store.service';
import { Store } from '../../shared/models/store.model';

// PrimeNG
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageService, ConfirmationService } from 'primeng/api';

@Component({
  selector: 'app-store-list',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    TableModule, ButtonModule, DialogModule,
    InputTextModule, TagModule, ToastModule,
    TooltipModule, ConfirmDialogModule
  ],
  providers: [MessageService, ConfirmationService],
  template: `
    <div class="space-y-6">

      <!-- ── Başlık ─────────────────────────────────────────── -->
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-2xl font-bold text-gray-800">Mağaza Yönetimi</h2>
          <p class="text-gray-400 text-sm mt-1">
            Toplam
            <span class="font-semibold text-gray-600">{{ stores.length }}</span>
            mağaza
          </p>
        </div>
        <button
          (click)="openForm()"
          class="inline-flex items-center gap-2 bg-red-500 hover:bg-red-600
                 text-white px-5 py-2.5 rounded-xl transition-colors
                 font-medium text-sm shadow-sm shadow-red-200">
          <i class="pi pi-plus"></i>
          Yeni Mağaza
        </button>
      </div>

      <!-- ── İstatistik Kartları ────────────────────────────── -->
      <div class="grid grid-cols-4 gap-4">

        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                Toplam
              </p>
              <p class="text-3xl font-bold text-gray-800 mt-1">
                {{ stores.length }}
              </p>
            </div>
            <div class="w-12 h-12 bg-blue-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-shop text-blue-500 text-xl"></i>
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
                Kritik Stok
              </p>
              <p class="text-3xl font-bold text-red-500 mt-1">
                {{ totalCritical }}
              </p>
            </div>
            <div class="w-12 h-12 bg-red-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-exclamation-triangle text-red-500 text-xl"></i>
            </div>
          </div>
        </div>

        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <div class="flex items-center justify-between">
            <div>
              <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                Düşük Stok
              </p>
              <p class="text-3xl font-bold text-orange-500 mt-1">
                {{ totalLow }}
              </p>
            </div>
            <div class="w-12 h-12 bg-orange-50 rounded-xl flex items-center justify-center">
              <i class="pi pi-bell text-orange-500 text-xl"></i>
            </div>
          </div>
        </div>

      </div>

      <!-- ── Tablo ──────────────────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">

        <div class="px-6 py-4 border-b border-gray-100">
          <span class="text-sm font-semibold text-gray-600">Mağaza Listesi</span>
        </div>

        <p-table
          [value]="stores"
          [loading]="isLoading"
          styleClass="p-datatable-sm"
          [rowHover]="true">

          <ng-template pTemplate="header">
            <tr>
              <th class="text-xs font-semibold text-gray-500 uppercase">
                Mağaza Kodu
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase">
                Mağaza Adı
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase">
                Şehir
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase">
                Telefon
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                Stok Uyarısı
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase">
                Durum
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                İşlemler
              </th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-store>
            <tr class="border-b border-gray-50">

              <!-- Kod -->
              <td>
                <span class="font-mono text-xs bg-gray-100 text-gray-600
                             px-2.5 py-1 rounded-lg font-semibold">
                  {{ store.code }}
                </span>
              </td>

              <!-- Ad -->
              <td class="font-semibold text-gray-800">{{ store.name }}</td>

              <!-- Şehir -->
              <td class="text-gray-500 text-sm">{{ store.city ?? '-' }}</td>

              <!-- Telefon -->
              <td class="text-gray-500 text-sm">{{ store.phone ?? '-' }}</td>

              <!-- Stok Uyarısı -->
              <td>
                <div class="flex items-center justify-center gap-2">
                  @if (store.criticalStockCount > 0) {
                    <span class="inline-flex items-center gap-1 text-xs
                                 bg-red-50 text-red-600 px-2 py-1 rounded-full font-medium">
                      <i class="pi pi-exclamation-triangle text-xs"></i>
                      {{ store.criticalStockCount }} Kritik
                    </span>
                  }
                  @if (store.lowStockCount > 0) {
                    <span class="inline-flex items-center gap-1 text-xs
                                 bg-orange-50 text-orange-600 px-2 py-1 rounded-full font-medium">
                      <i class="pi pi-bell text-xs"></i>
                      {{ store.lowStockCount }} Düşük
                    </span>
                  }
                  @if (store.criticalStockCount === 0 && store.lowStockCount === 0) {
                    <span class="text-xs text-gray-300">—</span>
                  }
                </div>
              </td>

              <!-- Durum -->
              <td>
                <p-tag
                  [value]="store.isActive ? 'Aktif' : 'Pasif'"
                  [severity]="store.isActive ? 'success' : 'danger'"
                  [rounded]="true" />
              </td>

              <!-- İşlemler -->
              <td>
                <div class="flex items-center justify-center gap-1.5">

                  <p-button
                    icon="pi pi-pencil"
                    size="small"
                    severity="secondary"
                    [rounded]="true"
                    pTooltip="Düzenle"
                    tooltipPosition="top"
                    (onClick)="openForm(store)" />

                  <p-button
                    [icon]="store.isActive ? 'pi pi-ban' : 'pi pi-check'"
                    size="small"
                    [severity]="store.isActive ? 'danger' : 'success'"
                    [rounded]="true"
                    [pTooltip]="store.isActive ? 'Pasife Al' : 'Aktif Et'"
                    tooltipPosition="top"
                    (onClick)="toggleStatus(store)" />

                </div>
              </td>

            </tr>
          </ng-template>

          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="7">
                <div class="flex flex-col items-center justify-center
                            py-16 text-gray-300">
                  <i class="pi pi-shop text-5xl mb-4"></i>
                  <p class="text-base font-medium">Henüz mağaza eklenmemiş</p>
                  <p class="text-sm mt-1">Yeni mağaza eklemek için butona tıklayın</p>
                </div>
              </td>
            </tr>
          </ng-template>

        </p-table>
      </div>

    </div>

    <!-- ── Mağaza Formu Modal ──────────────────────────────── -->
    <p-dialog
      [visible]="formVisible"
      (visibleChange)="formVisible = $event"
      [modal]="true"
      [closable]="true"
      [draggable]="false"
      [style]="{ width: '480px' }"
      [header]="editingStore ? 'Mağaza Düzenle' : 'Yeni Mağaza'">

      <form [formGroup]="storeForm" (ngSubmit)="onSubmit()" class="space-y-4 pt-2">

        <!-- Mağaza Adı -->
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Mağaza Adı <span class="text-red-500">*</span>
          </label>
          <input
            pInputText
            formControlName="name"
            placeholder="LCW İstanbul AVM"
            class="w-full" />
        </div>

        <!-- Mağaza Kodu -->
        @if (!editingStore) {
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Mağaza Kodu <span class="text-red-500">*</span>
            </label>
            <input
              pInputText
              formControlName="code"
              placeholder="LCW-IST-001"
              class="w-full font-mono"
              style="text-transform: uppercase" />
            <p class="text-xs text-gray-400 mt-1">
              Benzersiz olmalı. Örn: LCW-IST-001
            </p>
          </div>
        }

        <!-- Şehir -->
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Şehir
          </label>
          <input
            pInputText
            formControlName="city"
            placeholder="İstanbul"
            class="w-full" />
        </div>

        <!-- Adres -->
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Adres
          </label>
          <input
            pInputText
            formControlName="address"
            placeholder="Mağaza adresi"
            class="w-full" />
        </div>

        <!-- Telefon -->
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Telefon
          </label>
          <input
            pInputText
            formControlName="phone"
            placeholder="0212 000 00 00"
            class="w-full" />
        </div>

      </form>

      <ng-template pTemplate="footer">
        <div class="flex justify-end gap-2">
          <p-button
            label="İptal"
            severity="secondary"
            [outlined]="true"
            (onClick)="formVisible = false" />
          <p-button
            [label]="editingStore ? 'Güncelle' : 'Mağaza Ekle'"
            icon="pi pi-check"
            [loading]="isSubmitting"
            [disabled]="storeForm.invalid || isSubmitting"
            (onClick)="onSubmit()" />
        </div>
      </ng-template>

    </p-dialog>

    <p-toast position="top-right" />
    <p-confirmDialog />
  `
})
export class StoreListComponent implements OnInit {
  stores:       Store[] = [];
  isLoading             = false;
  isSubmitting          = false;
  formVisible           = false;
  editingStore: Store | null = null;
  storeForm!:   FormGroup;

  get activeCount():  number { return this.stores.filter(s => s.isActive).length; }
  get totalCritical():number { return this.stores.reduce((a, s) => a + s.criticalStockCount, 0); }
  get totalLow():     number { return this.stores.reduce((a, s) => a + s.lowStockCount, 0); }

  constructor(
    private storeService:        StoreService,
    private fb:                  FormBuilder,
    private messageService:      MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit(): void {
    this.buildForm();
    this.loadStores();
  }

  buildForm(): void {
    this.storeForm = this.fb.group({
      name:    ['', [Validators.required, Validators.minLength(2)]],
      code:    ['', Validators.required],
      city:    [''],
      address: [''],
      phone:   ['']
    });
  }

  loadStores(): void {
    this.isLoading = true;
    this.storeService.getAll().subscribe({
      next: data => {
        this.stores    = data;
        this.isLoading = false;
      },
      error: (err) => {
        this.isLoading = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Mağazalar yüklenirken hata oluştu.')
        });
      }
    });
  }

  openForm(store?: Store): void {
    this.editingStore = store ?? null;
    this.buildForm();

    if (store) {
      // Edit modda code alanını kaldır
      this.storeForm.removeControl('code');
      this.storeForm.patchValue({
        name:    store.name,
        city:    store.city ?? '',
        address: store.address ?? '',
        phone:   store.phone ?? ''
      });
    }

    this.formVisible = true;
  }

  onSubmit(): void {
    if (this.storeForm.invalid) return;
    this.isSubmitting = true;
    const v = this.storeForm.value;

    if (this.editingStore) {
      this.storeService.update(this.editingStore.id, {
        id:      this.editingStore.id,
        name:    v.name,
        city:    v.city,
        address: v.address,
        phone:   v.phone
      }).subscribe({
        next: () => this.onSuccess('Mağaza güncellendi.'),
        error: (err) => this.onError(apiErrorMessage(err, 'Güncelleme sırasında hata oluştu.'))
      });
    } else {
      this.storeService.create({
        name:    v.name,
        code:    v.code?.toUpperCase(),
        city:    v.city,
        address: v.address,
        phone:   v.phone
      }).subscribe({
        next: () => this.onSuccess('Mağaza eklendi.'),
        error: (err) => this.onError(apiErrorMessage(err, 'Mağaza eklenirken hata oluştu.'))
      });
    }
  }

  toggleStatus(store: Store): void {
    const action = store.isActive ? 'pasife almak' : 'aktif etmek';
    this.confirmationService.confirm({
      message:              `"${store.name}" mağazasını ${action} istediğinize emin misiniz?`,
      header:               store.isActive ? 'Pasife Al' : 'Aktif Et',
      icon:                 'pi pi-exclamation-triangle',
      acceptLabel:          'Evet',
      rejectLabel:          'İptal',
      acceptButtonStyleClass: store.isActive ? 'p-button-danger' : 'p-button-success',
      accept: () => {
        this.storeService.toggleStatus(store.id).subscribe({
          next: res => {
            this.loadStores();
            this.messageService.add({
              severity: 'success', summary: 'Başarılı',
              detail: res.message
            });
          },
          error: (err) => this.messageService.add({
            severity: 'error', summary: 'Hata',
            detail: apiErrorMessage(err, 'İşlem sırasında hata oluştu.')
          })
        });
      }
    });
  }

  private onSuccess(detail: string): void {
    this.isSubmitting = false;
    this.formVisible  = false;
    this.loadStores();
    this.messageService.add({
      severity: 'success', summary: 'Başarılı', detail
    });
  }

  private onError(detail: string): void {
    this.isSubmitting = false;
    this.messageService.add({
      severity: 'error', summary: 'Hata', detail
    });
  }
}