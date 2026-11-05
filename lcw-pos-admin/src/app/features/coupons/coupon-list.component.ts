import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule, FormBuilder, FormGroup, Validators,
  AbstractControl, ValidationErrors
} from '@angular/forms';
import { CouponService } from '../../core/services/coupon.service';
import { apiErrorMessage } from '../../core/utils/api.util';
import {
  Coupon, CouponDiscountType, CouponStatus, couponStatus
} from '../../shared/models/coupon.model';

// PrimeNG
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { CalendarModule } from 'primeng/calendar';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageService, ConfirmationService } from 'primeng/api';

type StatusFilter = 'all' | CouponStatus;

/** Form seviyesi kurallar (backend CreateCouponCommand ile aynı). */
function couponFormValidator(group: AbstractControl): ValidationErrors | null {
  const type  = group.get('discountType')?.value as CouponDiscountType;
  const value = group.get('value')?.value as number | null;
  const start = group.get('startDate')?.value as Date | null;
  const end   = group.get('endDate')?.value as Date | null;

  const errors: ValidationErrors = {};
  if (type === CouponDiscountType.Percentage && value != null && value > 100)
    errors['percentOver100'] = true;
  if (start && end && endOfDay(end) <= start)
    errors['endBeforeStart'] = true;
  return Object.keys(errors).length ? errors : null;
}

function endOfDay(d: Date): Date {
  const x = new Date(d);
  x.setHours(23, 59, 59, 999);
  return x;
}

@Component({
  selector: 'app-coupon-list',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    TableModule, ButtonModule, DialogModule, InputTextModule,
    InputNumberModule, CalendarModule, ToastModule,
    TooltipModule, ConfirmDialogModule
  ],
  providers: [MessageService, ConfirmationService],
  template: `
    <div class="space-y-6">

      <!-- ── Başlık ─────────────────────────────────────────── -->
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-2xl font-bold text-gray-800">Kupon Yönetimi</h2>
          <p class="text-gray-400 text-sm mt-1">
            Kasada uygulanan indirim kuponları — indirim her zaman sunucuda hesaplanır
          </p>
        </div>
        <button
          (click)="openForm()"
          class="inline-flex items-center gap-2 bg-red-500 hover:bg-red-600
                 text-white px-5 py-2.5 rounded-xl transition-colors
                 font-medium text-sm shadow-sm shadow-red-200">
          <i class="pi pi-plus"></i>
          Yeni Kupon
        </button>
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

      <!-- ── Tablo ──────────────────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">

        <div class="px-6 py-4 border-b border-gray-100 flex items-center justify-between gap-4 flex-wrap">
          <div class="relative">
            <i class="pi pi-search absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm"></i>
            <input type="text"
                   [value]="searchTerm"
                   (input)="searchTerm = $any($event.target).value"
                   placeholder="Kod veya açıklama ara..."
                   class="pl-9 pr-4 py-2 text-sm border border-gray-200 rounded-xl
                          focus:outline-none focus:ring-2 focus:ring-red-300 w-64" />
          </div>
          <div class="flex items-center gap-2 flex-wrap">
            @for (f of statusFilters; track f.value) {
              <button
                (click)="activeFilter = f.value"
                [class]="activeFilter === f.value
                  ? 'bg-red-500 text-white'
                  : 'bg-gray-100 text-gray-600 hover:bg-gray-200'"
                class="px-3 py-1.5 rounded-xl text-xs font-medium transition-colors">
                {{ f.label }}
                <span class="opacity-70">({{ countByStatus(f.value) }})</span>
              </button>
            }
          </div>
        </div>

        <p-table
          [value]="filteredCoupons"
          [loading]="isLoading"
          styleClass="p-datatable-sm"
          [rowHover]="true"
          [paginator]="filteredCoupons.length > 15"
          [rows]="15">

          <ng-template pTemplate="header">
            <tr>
              <th class="text-xs font-semibold text-gray-500 uppercase">Kod</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">İndirim</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Koşullar</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Geçerlilik</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Kullanım</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Durum</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">İşlem</th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-coupon>
            <tr class="border-b border-gray-50">

              <!-- Kod + açıklama -->
              <td>
                <div class="flex items-center gap-2">
                  <span class="font-mono text-sm bg-gray-100 text-gray-800
                               px-2.5 py-1 rounded-lg font-bold tracking-wide">
                    {{ coupon.code }}
                  </span>
                  <button (click)="copyCode(coupon.code)"
                          pTooltip="Kodu kopyala" tooltipPosition="top"
                          class="bg-transparent border-0 cursor-pointer p-1
                                 text-gray-300 hover:text-gray-600">
                    <i class="pi pi-copy text-xs"></i>
                  </button>
                </div>
                @if (coupon.description) {
                  <p class="text-xs text-gray-400 mt-1 max-w-56 truncate">{{ coupon.description }}</p>
                }
              </td>

              <!-- İndirim -->
              <td>
                <span class="font-bold text-gray-800">{{ discountLabel(coupon) }}</span>
                @if (coupon.discountType === Percentage && coupon.maxDiscountAmount) {
                  <p class="text-xs text-gray-400">
                    en fazla {{ coupon.maxDiscountAmount | currency:'TRY':'symbol':'1.0-2' }}
                  </p>
                }
              </td>

              <!-- Koşullar -->
              <td class="text-sm text-gray-500">
                @if (coupon.minPurchaseAmount) {
                  Min. {{ coupon.minPurchaseAmount | currency:'TRY':'symbol':'1.0-2' }}
                } @else {
                  <span class="text-gray-300">—</span>
                }
              </td>

              <!-- Geçerlilik -->
              <td class="text-xs text-gray-500 whitespace-nowrap">
                {{ coupon.startDate | date:'dd.MM.yyyy' }}
                →
                @if (coupon.endDate) {
                  {{ coupon.endDate | date:'dd.MM.yyyy' }}
                } @else {
                  <span class="text-gray-400">süresiz</span>
                }
              </td>

              <!-- Kullanım -->
              <td class="min-w-32">
                <div class="text-sm text-gray-700 font-medium">
                  {{ coupon.usedCount }}
                  <span class="text-gray-400 font-normal">
                    / {{ coupon.usageLimit ?? '∞' }}
                  </span>
                </div>
                @if (coupon.usageLimit) {
                  <div class="w-full h-1.5 bg-gray-100 rounded-full mt-1 overflow-hidden">
                    <div class="h-full rounded-full"
                         [class]="usagePercent(coupon) >= 100 ? 'bg-red-400' : 'bg-emerald-400'"
                         [style.width.%]="usagePercent(coupon)"></div>
                  </div>
                }
              </td>

              <!-- Durum -->
              <td>
                <span [class]="statusClass(coupon)"
                      class="text-xs px-2.5 py-1 rounded-full font-medium whitespace-nowrap">
                  {{ statusLabel(coupon) }}
                </span>
              </td>

              <!-- İşlem -->
              <td class="text-center">
                <p-button
                  [icon]="coupon.isActive ? 'pi pi-ban' : 'pi pi-check'"
                  size="small"
                  [severity]="coupon.isActive ? 'danger' : 'success'"
                  [rounded]="true"
                  [loading]="togglingId === coupon.id"
                  [pTooltip]="coupon.isActive ? 'Pasife Al' : 'Aktif Et'"
                  tooltipPosition="top"
                  (onClick)="confirmToggle(coupon)" />
              </td>
            </tr>
          </ng-template>

          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="7">
                <div class="flex flex-col items-center justify-center py-16 text-gray-300">
                  <i class="pi pi-ticket text-5xl mb-4"></i>
                  <p class="text-base font-medium">
                    {{ coupons.length ? 'Filtreye uyan kupon yok' : 'Henüz kupon oluşturulmamış' }}
                  </p>
                </div>
              </td>
            </tr>
          </ng-template>

        </p-table>
      </div>

    </div>

    <!-- ── Kupon Oluşturma ─────────────────────────────────── -->
    <p-dialog
      [visible]="formVisible"
      (visibleChange)="formVisible = $event"
      [modal]="true"
      [draggable]="false"
      header="Yeni Kupon"
      [style]="{ width: '560px' }">

      <form [formGroup]="couponForm" class="space-y-4 pt-2">

        <!-- Kod -->
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Kupon Kodu <span class="text-red-500">*</span>
          </label>
          <div class="flex gap-2">
            <input pInputText formControlName="code"
                   placeholder="YAZ2026"
                   maxlength="30"
                   (input)="uppercaseCode()"
                   class="flex-1 font-mono uppercase tracking-wide" />
            <p-button icon="pi pi-refresh" severity="secondary" [outlined]="true"
                      pTooltip="Rastgele kod üret" tooltipPosition="top"
                      (onClick)="generateCode()" />
          </div>
          @if (couponForm.get('code')?.touched && couponForm.get('code')?.errors?.['pattern']) {
            <p class="text-red-500 text-xs mt-1">
              Sadece harf, rakam, tire ve alt çizgi kullanılabilir.
            </p>
          }
        </div>

        <!-- Açıklama -->
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">Açıklama</label>
          <input pInputText formControlName="description"
                 placeholder="Yaz kampanyası, sadakat kuponu..."
                 maxlength="200" class="w-full" />
        </div>

        <!-- İndirim tipi + değer -->
        <div class="grid grid-cols-2 gap-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              İndirim Tipi <span class="text-red-500">*</span>
            </label>
            <div class="flex rounded-xl overflow-hidden border border-gray-200">
              <button type="button" (click)="setType(Percentage)"
                [class]="isPercentage ? 'bg-red-500 text-white' : 'bg-white text-gray-600 hover:bg-gray-50'"
                class="flex-1 py-2 text-sm font-medium transition-colors">
                % Yüzde
              </button>
              <button type="button" (click)="setType(Amount)"
                [class]="!isPercentage ? 'bg-red-500 text-white' : 'bg-white text-gray-600 hover:bg-gray-50'"
                class="flex-1 py-2 text-sm font-medium transition-colors">
                ₺ Tutar
              </button>
            </div>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              {{ isPercentage ? 'Yüzde (%)' : 'Tutar (₺)' }} <span class="text-red-500">*</span>
            </label>
            <p-inputNumber formControlName="value"
                           [min]="0.01" [max]="isPercentage ? 100 : 1000000"
                           [minFractionDigits]="0" [maxFractionDigits]="2"
                           locale="tr-TR"
                           [prefix]="isPercentage ? '%' : '₺'"
                           styleClass="w-full" inputStyleClass="w-full" />
            @if (couponForm.errors?.['percentOver100']) {
              <p class="text-red-500 text-xs mt-1">Yüzde 100'ü aşamaz.</p>
            }
          </div>
        </div>

        <!-- Tavan + alt limit -->
        <div class="grid grid-cols-2 gap-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Alt Limit (₺)
            </label>
            <p-inputNumber formControlName="minPurchaseAmount"
                           [min]="0" [maxFractionDigits]="2" locale="tr-TR" prefix="₺"
                           placeholder="Yok"
                           styleClass="w-full" inputStyleClass="w-full" />
            <p class="text-xs text-gray-400 mt-1">Bu tutarın altındaki alışverişte geçmez</p>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1"
                   [class.text-gray-300]="!isPercentage">
              İndirim Tavanı (₺)
            </label>
            <p-inputNumber formControlName="maxDiscountAmount"
                           [min]="0.01" [maxFractionDigits]="2" locale="tr-TR" prefix="₺"
                           [placeholder]="isPercentage ? 'Yok' : 'Sadece yüzde kuponda'"
                           styleClass="w-full" inputStyleClass="w-full" />
            <p class="text-xs text-gray-400 mt-1">Yüzde indirimin en fazla tutarı</p>
          </div>
        </div>

        <!-- Tarihler -->
        <div class="grid grid-cols-2 gap-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Başlangıç <span class="text-red-500">*</span>
            </label>
            <p-calendar formControlName="startDate"
                        dateFormat="dd.mm.yy" [showTime]="true" hourFormat="24"
                        [showIcon]="true" appendTo="body"
                        styleClass="w-full" inputStyleClass="w-full" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Bitiş (gün sonuna kadar)
            </label>
            <p-calendar formControlName="endDate"
                        dateFormat="dd.mm.yy" [showIcon]="true" [showClear]="true"
                        [minDate]="couponForm.get('startDate')?.value"
                        placeholder="Süresiz" appendTo="body"
                        styleClass="w-full" inputStyleClass="w-full" />
            @if (couponForm.errors?.['endBeforeStart']) {
              <p class="text-red-500 text-xs mt-1">Bitiş başlangıçtan sonra olmalı.</p>
            }
          </div>
        </div>

        <!-- Kullanım limiti -->
        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Toplam Kullanım Limiti
          </label>
          <p-inputNumber formControlName="usageLimit"
                         [min]="1" [useGrouping]="false"
                         placeholder="Sınırsız"
                         styleClass="w-48" inputStyleClass="w-48" />
          <p class="text-xs text-gray-400 mt-1">
            Tüm mağazalarda toplam kaç satışta kullanılabileceği (tek kullanımlık kupon için 1)
          </p>
        </div>

        <!-- Önizleme -->
        @if (preview) {
          <div class="bg-emerald-50 border border-emerald-100 rounded-xl px-4 py-3 text-sm text-emerald-800">
            <i class="pi pi-eye mr-1"></i>{{ preview }}
          </div>
        }

        @if (formError) {
          <div class="bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-sm text-red-600">
            <i class="pi pi-exclamation-triangle mr-1"></i>{{ formError }}
          </div>
        }
      </form>

      <ng-template pTemplate="footer">
        <div class="flex justify-end gap-2">
          <p-button label="İptal" severity="secondary" [outlined]="true"
                    (onClick)="formVisible = false" />
          <p-button label="Kuponu Oluştur" icon="pi pi-check"
                    [loading]="isSubmitting"
                    [disabled]="couponForm.invalid || isSubmitting"
                    (onClick)="onSubmit()" />
        </div>
      </ng-template>
    </p-dialog>

    <p-toast position="top-right" />
    <p-confirmDialog />
  `
})
export class CouponListComponent implements OnInit {
  readonly Percentage = CouponDiscountType.Percentage;
  readonly Amount     = CouponDiscountType.Amount;

  coupons:      Coupon[] = [];
  isLoading     = false;
  isSubmitting  = false;
  formVisible   = false;
  formError     = '';
  togglingId:   number | null = null;
  activeFilter: StatusFilter = 'all';
  searchTerm    = '';
  couponForm!:  FormGroup;

  readonly statusFilters: { label: string; value: StatusFilter }[] = [
    { label: 'Tümü',          value: 'all'       },
    { label: '🟢 Geçerli',    value: 'active'    },
    { label: '⏳ Başlamadı',  value: 'scheduled' },
    { label: '⌛ Süresi Doldu', value: 'expired' },
    { label: '🔒 Limit Doldu', value: 'exhausted' },
    { label: '⛔ Pasif',      value: 'inactive'  },
  ];

  constructor(
    private couponService:       CouponService,
    private fb:                  FormBuilder,
    private messageService:      MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit(): void {
    this.buildForm();
    this.loadCoupons();
  }

  // ── Liste ──────────────────────────────────────────────────
  get filteredCoupons(): Coupon[] {
    const term = this.searchTerm.trim().toLocaleLowerCase('tr');
    return this.coupons.filter(c =>
      (this.activeFilter === 'all' || couponStatus(c) === this.activeFilter) &&
      (!term ||
        c.code.toLocaleLowerCase('tr').includes(term) ||
        (c.description ?? '').toLocaleLowerCase('tr').includes(term)));
  }

  get stats() {
    const totalUses = this.coupons.reduce((a, c) => a + c.usedCount, 0);
    return [
      { label: 'Toplam Kupon', value: this.coupons.length,
        color: 'text-gray-800', bgColor: 'bg-blue-50', icon: 'pi-ticket', iconColor: 'text-blue-500' },
      { label: 'Şu An Geçerli', value: this.countByStatus('active'),
        color: 'text-emerald-600', bgColor: 'bg-emerald-50', icon: 'pi-check-circle', iconColor: 'text-emerald-500' },
      { label: 'Süresi/Limiti Dolan', value: this.countByStatus('expired') + this.countByStatus('exhausted'),
        color: 'text-orange-500', bgColor: 'bg-orange-50', icon: 'pi-clock', iconColor: 'text-orange-500' },
      { label: 'Toplam Kullanım', value: totalUses,
        color: 'text-purple-600', bgColor: 'bg-purple-50', icon: 'pi-shopping-bag', iconColor: 'text-purple-500' },
    ];
  }

  countByStatus(filter: StatusFilter): number {
    return filter === 'all'
      ? this.coupons.length
      : this.coupons.filter(c => couponStatus(c) === filter).length;
  }

  loadCoupons(): void {
    this.isLoading = true;
    this.couponService.getAll().subscribe({
      next: data => { this.coupons = data; this.isLoading = false; },
      error: (err) => {
        this.isLoading = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Kuponlar yüklenemedi.')
        });
      }
    });
  }

  discountLabel(c: Coupon): string {
    return c.discountType === CouponDiscountType.Percentage
      ? `%${this.fmt(c.value)}`
      : `${this.fmt(c.value)} ₺`;
  }

  usagePercent(c: Coupon): number {
    return c.usageLimit ? Math.min(100, Math.round(c.usedCount / c.usageLimit * 100)) : 0;
  }

  statusLabel(c: Coupon): string {
    return ({
      active:    '🟢 Geçerli',
      scheduled: '⏳ Başlamadı',
      expired:   '⌛ Süresi doldu',
      exhausted: '🔒 Limit doldu',
      inactive:  '⛔ Pasif'
    } as Record<CouponStatus, string>)[couponStatus(c)];
  }

  statusClass(c: Coupon): string {
    return ({
      active:    'bg-emerald-100 text-emerald-700',
      scheduled: 'bg-blue-100 text-blue-700',
      expired:   'bg-gray-100 text-gray-500',
      exhausted: 'bg-orange-100 text-orange-700',
      inactive:  'bg-red-100 text-red-600'
    } as Record<CouponStatus, string>)[couponStatus(c)];
  }

  copyCode(code: string): void {
    navigator.clipboard?.writeText(code).then(
      () => this.messageService.add({ severity: 'info', summary: 'Kopyalandı', detail: code, life: 1500 }),
      () => {});
  }

  // ── Aktif / Pasif ──────────────────────────────────────────
  confirmToggle(c: Coupon): void {
    const activate = !c.isActive;
    this.confirmationService.confirm({
      header: activate ? 'Kuponu Aktif Et' : 'Kuponu Pasife Al',
      message: activate
        ? `"${c.code}" kuponu tekrar kasada kullanılabilir olacak.`
        : `"${c.code}" kuponu artık kasada kabul edilmeyecek. Geçmiş satışlar etkilenmez.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: activate ? 'Aktif Et' : 'Pasife Al',
      rejectLabel: 'Vazgeç',
      acceptButtonStyleClass: activate ? 'p-button-success' : 'p-button-danger',
      accept: () => this.toggle(c, activate)
    });
  }

  private toggle(c: Coupon, activate: boolean): void {
    this.togglingId = c.id;
    this.couponService.setActive(c.id, activate).subscribe({
      next: updated => {
        this.togglingId = null;
        this.coupons = this.coupons.map(x => x.id === updated.id ? updated : x);
        this.messageService.add({
          severity: 'success', summary: 'Güncellendi',
          detail: `${updated.code} ${updated.isActive ? 'aktif edildi' : 'pasife alındı'}.`
        });
      },
      error: (err) => {
        this.togglingId = null;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Kupon durumu değiştirilemedi.')
        });
      }
    });
  }

  // ── Oluşturma ──────────────────────────────────────────────
  get isPercentage(): boolean {
    return this.couponForm?.get('discountType')?.value === CouponDiscountType.Percentage;
  }

  buildForm(): void {
    this.couponForm = this.fb.group({
      code:              ['', [Validators.required, Validators.maxLength(30),
                               Validators.pattern(/^[A-Za-z0-9_-]+$/)]],
      description:       ['', Validators.maxLength(200)],
      discountType:      [CouponDiscountType.Percentage, Validators.required],
      value:             [null, [Validators.required, Validators.min(0.01)]],
      minPurchaseAmount: [null, Validators.min(0)],
      maxDiscountAmount: [null, Validators.min(0.01)],
      startDate:         [new Date(), Validators.required],
      endDate:           [null],
      usageLimit:        [null, Validators.min(1)]
    }, { validators: couponFormValidator });
  }

  openForm(): void {
    this.buildForm();
    this.formError = '';
    this.formVisible = true;
  }

  setType(type: CouponDiscountType): void {
    this.couponForm.patchValue({ discountType: type });
    // Tavan sadece yüzde kuponda anlamlı
    const max = this.couponForm.get('maxDiscountAmount');
    if (type === CouponDiscountType.Amount) {
      max?.setValue(null);
      max?.disable();
    } else {
      max?.enable();
    }
  }

  uppercaseCode(): void {
    const ctrl = this.couponForm.get('code');
    const v = (ctrl?.value ?? '') as string;
    const up = v.toLocaleUpperCase('en-US');
    if (v !== up) ctrl?.setValue(up, { emitEvent: false });
  }

  generateCode(): void {
    // Karışabilecek karakterler (0/O, 1/I) çıkarıldı - kasiyer elle girerken hata olmasın
    const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    const rnd = Array.from({ length: 8 },
      () => alphabet[Math.floor(Math.random() * alphabet.length)]).join('');
    this.couponForm.patchValue({ code: `LCW-${rnd}` });
    this.couponForm.get('code')?.markAsTouched();
  }

  /** Örnek bir sepette kuponun ne kadar indirim yapacağını gösterir. */
  get preview(): string {
    const v = this.couponForm?.getRawValue();
    if (!v?.value || v.value <= 0) return '';
    const sample = Math.max(500, v.minPurchaseAmount ?? 0);
    let discount = v.discountType === CouponDiscountType.Percentage
      ? Math.round(sample * v.value) / 100
      : v.value;
    if (v.discountType === CouponDiscountType.Percentage && v.maxDiscountAmount)
      discount = Math.min(discount, v.maxDiscountAmount);
    discount = Math.min(discount, sample);
    const min = v.minPurchaseAmount ? ` · ${this.fmt(v.minPurchaseAmount)} ₺ altındaki sepette geçmez` : '';
    return `${this.fmt(sample)} ₺ sepette ${this.fmt(discount)} ₺ indirim${min}`;
  }

  onSubmit(): void {
    if (this.couponForm.invalid) return;
    const v = this.couponForm.getRawValue();

    this.isSubmitting = true;
    this.formError = '';

    this.couponService.create({
      code:              (v.code as string).trim().toLocaleUpperCase('en-US'),
      description:       v.description?.trim() || null,
      discountType:      v.discountType,
      value:             v.value,
      minPurchaseAmount: v.minPurchaseAmount || null,
      maxDiscountAmount: v.discountType === CouponDiscountType.Percentage
                           ? (v.maxDiscountAmount || null) : null,
      startDate:         (v.startDate as Date).toISOString(),
      endDate:           v.endDate ? endOfDay(v.endDate).toISOString() : null,
      usageLimit:        v.usageLimit || null
    }).subscribe({
      next: created => {
        this.isSubmitting = false;
        this.formVisible = false;
        this.coupons = [created, ...this.coupons];
        this.messageService.add({
          severity: 'success', summary: 'Kupon Oluşturuldu',
          detail: `${created.code} — ${this.discountLabel(created)}`
        });
      },
      error: (err) => {
        this.isSubmitting = false;
        // Hata dialog içinde gösterilir (örn. "Bu kod zaten var") - form kapanmaz
        this.formError = apiErrorMessage(err, 'Kupon oluşturulamadı.');
      }
    });
  }

  private fmt(n: number): string {
    return n.toLocaleString('tr-TR', { maximumFractionDigits: 2 });
  }
}
