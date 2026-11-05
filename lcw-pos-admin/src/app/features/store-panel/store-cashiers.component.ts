import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { apiErrorMessage } from '../../core/utils/api.util';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageService, ConfirmationService } from 'primeng/api';

interface Cashier {
  id:           string;
  firstName:    string;
  lastName:     string;
  email:        string;
  personelId:   string;
  isActive:     boolean;
  isFirstLogin: boolean;
  storeId?:     number;
  roles?:       string[];
}

@Component({
  selector: 'app-store-cashiers',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    TableModule, ButtonModule, DialogModule,
    TagModule, ToastModule, TooltipModule, ConfirmDialogModule
  ],
  providers: [MessageService, ConfirmationService],
  template: `
    <div class="space-y-6">

      <!-- ── Başlık ─────────────────────────────────────────── -->
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-2xl font-bold text-gray-800">Kasiyer Yönetimi</h2>
          <p class="text-gray-400 text-sm mt-1">
            Mağazanızdaki kasiyer listesi ve PIN yönetimi
          </p>
        </div>
        <button (click)="openCreateDialog()"
          class="inline-flex items-center gap-2 bg-red-500 hover:bg-red-600
                 text-white px-5 py-2.5 rounded-xl transition-colors
                 font-medium text-sm shadow-sm shadow-red-200">
          <i class="pi pi-plus"></i>
          Yeni Kasiyer
        </button>
      </div>

      <!-- ── Özet ──────────────────────────────────────────── -->
      <div class="grid grid-cols-3 gap-4">
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">Toplam Kasiyer</p>
          <p class="text-3xl font-bold text-gray-800 mt-1">{{ cashiers.length }}</p>
        </div>
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">Aktif</p>
          <p class="text-3xl font-bold text-emerald-600 mt-1">
            
            {{ activeCount }}
          </p>
        </div>
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
          <p class="text-xs font-semibold text-gray-400 uppercase">
            İlk Giriş Bekleyen
          </p>
          <p class="text-3xl font-bold text-orange-500 mt-1">
            {{ firstLoginCount }}
          </p>
        </div>
      </div>

      <!-- ── Tablo ──────────────────────────────────────────── -->
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        <div class="p-4 border-b border-gray-100 flex items-center gap-3 flex-wrap">
          <div class="relative">
            <i class="pi pi-search absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm"></i>
            <input type="text" [(ngModel)]="searchTerm"
                   placeholder="Ad, e-posta veya personel no..."
                   class="pl-9 pr-4 py-2 text-sm border border-gray-200 rounded-xl
                          focus:outline-none focus:ring-2 focus:ring-red-300 w-72" />
          </div>
          <label class="flex items-center gap-2 text-sm text-gray-600 cursor-pointer">
            <input type="checkbox" [(ngModel)]="showInactive" />
            Pasifleri göster
          </label>
        </div>
        <p-table
          [value]="filteredCashiers"
          [loading]="isLoading"
          styleClass="p-datatable-sm"
          [rowHover]="true">

          <ng-template pTemplate="header">
            <tr>
              <th class="text-xs font-semibold text-gray-500 uppercase">
                Personel No
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Ad Soyad</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Email</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                Durum
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                PIN Durumu
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                İşlemler
              </th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-cashier>
            <tr class="border-b border-gray-50">

              <td>
                <span class="font-mono text-sm font-bold text-gray-700
                             bg-gray-100 px-2.5 py-1 rounded-lg">
                  {{ cashier.personelId }}
                </span>
              </td>

              <td>
                <div class="flex items-center gap-3">
                  <div class="w-8 h-8 bg-gradient-to-br from-red-400 to-red-600
                              rounded-lg flex items-center justify-center
                              flex-shrink-0">
                    <span class="text-white text-xs font-bold">
                      {{ getInitials(cashier) }}
                    </span>
                  </div>
                  <span class="font-semibold text-gray-800 text-sm">
                    {{ cashier.firstName }} {{ cashier.lastName }}
                  </span>
                </div>
              </td>

              <td class="text-gray-500 text-sm">{{ cashier.email }}</td>

              <td class="text-center">
                <p-tag
                  [value]="cashier.isActive ? 'Aktif' : 'Pasif'"
                  [severity]="cashier.isActive ? 'success' : 'danger'"
                  [rounded]="true" />
              </td>

              <td class="text-center">
                @if (cashier.isFirstLogin) {
                  <span class="text-xs bg-orange-100 text-orange-700
                               px-2.5 py-1 rounded-full font-medium">
                    ⚠️ İlk Giriş Bekliyor
                  </span>
                } @else {
                  <span class="text-xs bg-emerald-100 text-emerald-700
                               px-2.5 py-1 rounded-full font-medium">
                    ✅ PIN Aktif
                  </span>
                }
              </td>

              <td>
                <div class="flex items-center justify-center gap-1.5">
                  <p-button
                    icon="pi pi-pencil"
                    size="small"
                    severity="secondary"
                    [rounded]="true"
                    pTooltip="Bilgileri Düzenle"
                    tooltipPosition="top"
                    (onClick)="openEditDialog(cashier)" />
                  <p-button
                    icon="pi pi-key"
                    size="small"
                    severity="warning"
                    [rounded]="true"
                    pTooltip="PIN Sıfırla"
                    tooltipPosition="top"
                    (onClick)="confirmResetPin(cashier)" />
                  <p-button
                    [icon]="cashier.isActive ? 'pi pi-ban' : 'pi pi-check'"
                    size="small"
                    [severity]="cashier.isActive ? 'danger' : 'success'"
                    [rounded]="true"
                    [pTooltip]="cashier.isActive ? 'Pasife Al' : 'Aktife Al'"
                    tooltipPosition="top"
                    (onClick)="confirmToggleStatus(cashier)" />
                </div>
              </td>

            </tr>
          </ng-template>

          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="6">
                <div class="text-center py-12 text-gray-300">
                  <i class="pi pi-users text-4xl mb-3 block"></i>
                  <p>Henüz kasiyer eklenmemiş</p>
                </div>
              </td>
            </tr>
          </ng-template>

        </p-table>
      </div>

    </div>

    <!-- ── Yeni Kasiyer Dialog ────────────────────────────── -->
    <p-dialog
      [visible]="createDialogVisible"
      (visibleChange)="createDialogVisible = $event"
      [modal]="true" [draggable]="false"
      header="Yeni Kasiyer Oluştur"
      [style]="{ width: '480px' }">

      <div class="space-y-4 pt-2">

        <div class="bg-blue-50 rounded-xl px-4 py-3 flex items-start gap-3">
          <i class="pi pi-info-circle text-blue-500 mt-0.5"></i>
          <p class="text-sm text-blue-700">
            Personel numarası otomatik atanır. Başlangıç PIN'i
            <strong>1234</strong> olarak ayarlanır. İlk girişte değiştirmesi
            zorunludur.
          </p>
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Ad <span class="text-red-500">*</span>
          </label>
          <input type="text" [(ngModel)]="newCashier.firstName"
                 placeholder="Ahmet"
                 class="w-full px-3 py-2 text-sm border border-gray-200
                        rounded-xl focus:outline-none focus:ring-2
                        focus:ring-red-300" />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Soyad <span class="text-red-500">*</span>
          </label>
          <input type="text" [(ngModel)]="newCashier.lastName"
                 placeholder="Yılmaz"
                 class="w-full px-3 py-2 text-sm border border-gray-200
                        rounded-xl focus:outline-none focus:ring-2
                        focus:ring-red-300" />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Email <span class="text-red-500">*</span>
          </label>
          <input type="email" [(ngModel)]="newCashier.email"
                 placeholder="kasiyer@lcwaikiki.com"
                 class="w-full px-3 py-2 text-sm border border-gray-200
                        rounded-xl focus:outline-none focus:ring-2
                        focus:ring-red-300" />
          @if (newCashier.email && !isEmailValid) {
            <p class="text-xs text-red-500 mt-1">Geçerli bir e-posta adresi girin.</p>
          }
        </div>

      </div>

      <ng-template pTemplate="footer">
        <div class="flex justify-end gap-2">
          <p-button label="İptal" severity="secondary" [outlined]="true"
            (onClick)="createDialogVisible = false" />
          <p-button
            label="Kasiyer Oluştur"
            icon="pi pi-user-plus"
            [loading]="isSubmitting"
            [disabled]="!isCreateValid || isSubmitting"
            (onClick)="onCreateSubmit()" />
        </div>
      </ng-template>
    </p-dialog>

    <!-- ── Kasiyer Düzenle Dialog ─────────────────────────── -->
    <p-dialog
      [visible]="editDialogVisible"
      (visibleChange)="editDialogVisible = $event"
      [modal]="true" [draggable]="false"
      header="Kasiyer Bilgilerini Düzenle"
      [style]="{ width: '480px' }">

      @if (editTarget) {
        <div class="space-y-4 pt-2">
          <p class="text-xs text-gray-400">
            Personel No: <span class="font-mono font-semibold text-gray-600">{{ editTarget.personelId }}</span>
            — personel numarası ve PIN bu ekrandan değişmez.
          </p>
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Ad <span class="text-red-500">*</span></label>
              <input type="text" [(ngModel)]="editForm.firstName" maxlength="100"
                     class="w-full px-3 py-2 text-sm border border-gray-200 rounded-xl
                            focus:outline-none focus:ring-2 focus:ring-red-300" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Soyad <span class="text-red-500">*</span></label>
              <input type="text" [(ngModel)]="editForm.lastName" maxlength="100"
                     class="w-full px-3 py-2 text-sm border border-gray-200 rounded-xl
                            focus:outline-none focus:ring-2 focus:ring-red-300" />
            </div>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Email <span class="text-red-500">*</span></label>
            <input type="email" [(ngModel)]="editForm.email"
                   class="w-full px-3 py-2 text-sm border border-gray-200 rounded-xl
                          focus:outline-none focus:ring-2 focus:ring-red-300" />
            @if (editForm.email && !isValidEmail(editForm.email)) {
              <p class="text-xs text-red-500 mt-1">Geçerli bir e-posta adresi girin.</p>
            }
          </div>
        </div>
      }

      <ng-template pTemplate="footer">
        <div class="flex justify-end gap-2">
          <p-button label="İptal" severity="secondary" [outlined]="true"
            (onClick)="editDialogVisible = false" />
          <p-button label="Kaydet" icon="pi pi-check"
            [loading]="isSubmitting"
            [disabled]="!isEditValid || isSubmitting"
            (onClick)="onEditSubmit()" />
        </div>
      </ng-template>
    </p-dialog>

    <p-toast position="top-right" />
    <p-confirmDialog />
  `
})
export class StoreCashiersComponent implements OnInit {
  cashiers:          Cashier[] = [];
  isLoading          = false;
  isSubmitting       = false;
  createDialogVisible = false;

  newCashier = { firstName: '', lastName: '', email: '' };

  get activeCount():     number { return this.cashiers.filter(c => c.isActive).length; }
  get firstLoginCount(): number { return this.cashiers.filter(c => c.isFirstLogin).length; }
  get isEmailValid():    boolean {
    return this.isValidEmail(this.newCashier.email);
  }

  isValidEmail(email: string): boolean {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test((email ?? '').trim());
  }

  // ── Arama / filtre ──────────────────────────────────────────
  searchTerm   = '';
  showInactive = true;

  get filteredCashiers(): Cashier[] {
    const term = this.searchTerm.trim().toLocaleLowerCase('tr');
    return this.cashiers.filter(c =>
      (this.showInactive || c.isActive) &&
      (!term ||
        `${c.firstName} ${c.lastName}`.toLocaleLowerCase('tr').includes(term) ||
        c.email?.toLocaleLowerCase('tr').includes(term) ||
        c.personelId?.includes(term)));
  }

  // ── Düzenleme ───────────────────────────────────────────────
  editDialogVisible = false;
  editTarget: Cashier | null = null;
  editForm = { firstName: '', lastName: '', email: '' };

  get isEditValid(): boolean {
    const f = this.editForm;
    const t = this.editTarget;
    const changed = !!t && (f.firstName.trim() !== t.firstName ||
                            f.lastName.trim()  !== t.lastName  ||
                            f.email.trim()     !== t.email);
    return !!f.firstName.trim() && !!f.lastName.trim() && this.isValidEmail(f.email) && changed;
  }

  openEditDialog(cashier: Cashier): void {
    this.editTarget = cashier;
    this.editForm = { firstName: cashier.firstName, lastName: cashier.lastName, email: cashier.email };
    this.editDialogVisible = true;
  }

  onEditSubmit(): void {
    const t = this.editTarget;
    if (!t || !this.isEditValid) return;
    this.isSubmitting = true;
    this.http.put(`${this.apiUrl}/users/${t.id}`, {
      firstName: this.editForm.firstName.trim(),
      lastName:  this.editForm.lastName.trim(),
      email:     this.editForm.email.trim()
    }).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.editDialogVisible = false;
        this.loadCashiers();
        this.messageService.add({
          severity: 'success', summary: 'Güncellendi',
          detail: `${this.editForm.firstName.trim()} ${this.editForm.lastName.trim()} bilgileri kaydedildi.`
        });
      },
      error: (err) => {
        this.isSubmitting = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Kasiyer güncellenemedi.')
        });
      }
    });
  }
  get isCreateValid():   boolean {
    return !!this.newCashier.firstName.trim() &&
           !!this.newCashier.lastName.trim()  &&
           this.isEmailValid;
  }

  private readonly apiUrl = `${environment.apiUrl}/auth`;

  constructor(
    private http:                HttpClient,
    private authService:         AuthService,
    private messageService:      MessageService,
    private confirmationService: ConfirmationService
  ) {}

  ngOnInit(): void { this.loadCashiers(); }

  loadCashiers(): void {
    this.isLoading = true;
    const storeId  = this.authService.storeId();

    this.http.get<Cashier[]>(`${this.apiUrl}/users`).subscribe({
      next: users => {
        // Backend müdüre zaten sadece kendi mağazasını döner; burada ayrıca
        // SADECE kasiyerler gösterilir (müdürün kendisi listede çıkmasın).
        this.cashiers = users.filter(
          u => u.storeId === +(storeId ?? 0) && (u.roles ?? []).includes('Cashier')
        );
        this.isLoading = false;
      },
      error: (err) => {
        this.isLoading = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Kasiyerler yüklenemedi.')
        });
      }
    });
  }

  openCreateDialog(): void {
    this.newCashier     = { firstName: '', lastName: '', email: '' };
    this.createDialogVisible = true;
  }

  onCreateSubmit(): void {
    const storeId = this.authService.storeId();
    if (!storeId || !this.isCreateValid) return;
    this.isSubmitting = true;

    // Rol 'Cashier' (eskiden 'User' gidiyordu: PersonelId/PIN üretilmiyor,
    // kasiyer POS'a giremiyordu). Şifre gönderilmez - kasiyer POS'a
    // PersonelId + PIN ile girer. Mağaza backend'de müdürün hesabından alınır.
    this.http.post<{ personelId?: string }>(`${this.apiUrl}/create-user`, {
      firstName: this.newCashier.firstName.trim(),
      lastName:  this.newCashier.lastName.trim(),
      email:     this.newCashier.email.trim(),
      storeId:  +storeId,
      role:     'Cashier'
    }).subscribe({
      next: res => {
        this.isSubmitting        = false;
        this.createDialogVisible = false;
        this.loadCashiers();
        this.messageService.add({
          severity: 'success', summary: 'Kasiyer Oluşturuldu',
          detail: `${this.newCashier.firstName} ${this.newCashier.lastName} — ` +
                  `Personel No: ${res?.personelId ?? '-'} — PIN: 1234`,
          life: 10000
        });
      },
      error: (err) => {
        this.isSubmitting = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: apiErrorMessage(err, 'Kasiyer oluşturulamadı.')
        });
      }
    });
  }

  confirmResetPin(cashier: Cashier): void {
    this.confirmationService.confirm({
      message: `"${cashier.firstName} ${cashier.lastName}" kullanıcısının
                PIN'i sıfırlanacak. İlk girişte yeni PIN belirlemesi gerekecek.`,
      header:      'PIN Sıfırla',
      icon:        'pi pi-key',
      acceptLabel: 'Evet, Sıfırla',
      rejectLabel: 'İptal',
      acceptButtonStyleClass: 'p-button-warning',
      accept: () => this.resetPin(cashier)
    });
  }

  resetPin(cashier: Cashier): void {
    this.http.post(
      `${environment.apiUrl}/pos/auth/reset-pin/${cashier.id}`, {}
    ).subscribe({
      next: () => {
        this.loadCashiers();
        this.messageService.add({
          severity: 'success', summary: 'PIN Sıfırlandı',
          detail: `${cashier.firstName} ${cashier.lastName} — Yeni PIN: 1234`
        });
      },
      error: (err) => this.messageService.add({
        severity: 'error', summary: 'Hata',
        detail: apiErrorMessage(err, 'PIN sıfırlanamadı.')
      })
    });
  }

  confirmToggleStatus(cashier: Cashier): void {
    const action = cashier.isActive ? 'pasife almak' : 'aktife almak';
    this.confirmationService.confirm({
      message: `"${cashier.firstName} ${cashier.lastName}" kullanıcısını
                ${action} istediğinize emin misiniz?`,
      header:      cashier.isActive ? 'Pasife Al' : 'Aktife Al',
      icon:        'pi pi-exclamation-triangle',
      acceptLabel: 'Evet',
      rejectLabel: 'İptal',
      accept: () => this.toggleStatus(cashier)
    });
  }

  toggleStatus(cashier: Cashier): void {
    this.http.patch(
      `${this.apiUrl}/users/${cashier.id}/toggle-status`, {}
    ).subscribe({
      next: () => {
        this.loadCashiers();
        this.messageService.add({
          severity: 'info', summary: 'Güncellendi',
          detail: `${cashier.firstName} ${cashier.lastName} durumu değiştirildi.`
        });
      },
      error: (err) => this.messageService.add({
        severity: 'error', summary: 'Hata',
        detail: apiErrorMessage(err, 'Durum değiştirilemedi.')
      })
    });
  }

  getInitials(cashier: Cashier): string {
    return `${cashier.firstName?.[0] ?? ''}${cashier.lastName?.[0] ?? ''}`.toUpperCase() || '?';
  }
}