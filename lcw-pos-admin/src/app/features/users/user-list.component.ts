import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { UserService } from '../../core/services/user.service';
import { StoreService } from '../../core/services/store.service';
import { AppUser, CreateUserRequest } from '../../shared/models/user.model';
import { Store } from '../../shared/models/store.model';
import { apiErrorMessage } from '../../core/utils/api.util';

// PrimeNG
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { DropdownModule } from 'primeng/dropdown';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { PasswordModule } from 'primeng/password';
import { MessageService } from 'primeng/api';

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    TableModule, ButtonModule, DialogModule,
    InputTextModule, DropdownModule, TagModule,
    ToastModule, TooltipModule, PasswordModule
  ],
  providers: [MessageService],
  template: `
    <div class="space-y-6">

      <!-- ── Başlık ─────────────────────────────────────────── -->
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-2xl font-bold text-gray-800">Kullanıcı Yönetimi</h2>
          <p class="text-gray-400 text-sm mt-1">
            Toplam
            <span class="font-semibold text-gray-600">{{ users.length }}</span>
            kullanıcı
          </p>
        </div>
        <button
          (click)="openForm()"
          class="inline-flex items-center gap-2 bg-red-500 hover:bg-red-600
                 text-white px-5 py-2.5 rounded-xl transition-colors
                 font-medium text-sm shadow-sm shadow-red-200">
          <i class="pi pi-user-plus"></i>
          Kullanıcı Ekle
        </button>
      </div>

      <!-- ── İstatistik Kartları ────────────────────────────── -->
      <div class="grid grid-cols-4 gap-4">
        @for (stat of roleStats; track stat.role) {
          <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">
                  {{ stat.label }}
                </p>
                <p class="text-3xl font-bold mt-1" [class]="stat.color">
                  {{ stat.count }}
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

        <div class="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
          <span class="text-sm font-semibold text-gray-600">Kullanıcı Listesi</span>
          <div class="flex items-center gap-2">
            @for (filter of roleFilters; track filter.value) {
              <button
                (click)="activeRoleFilter = filter.value"
                [class]="activeRoleFilter === filter.value
                  ? 'bg-red-500 text-white'
                  : 'bg-gray-100 text-gray-600 hover:bg-gray-200'"
                class="px-3 py-1.5 rounded-xl text-xs font-medium transition-colors">
                {{ filter.label }}
              </button>
            }
          </div>
        </div>

        <p-table
          [value]="filteredUsers"
          [loading]="isLoading"
          styleClass="p-datatable-sm"
          [rowHover]="true"
          [paginator]="true"
          [rows]="10">

          <ng-template pTemplate="header">
            <tr>
              <th class="text-xs font-semibold text-gray-500 uppercase">
                Kullanıcı
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase">
                E-Posta
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase">
                Rol
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase">
                Mağaza
              </th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center">
                Durum
              </th>
            </tr>
          </ng-template>

          <ng-template pTemplate="body" let-user>
            <tr class="border-b border-gray-50">

              <!-- Kullanıcı -->
              <td>
                <div class="flex items-center gap-3">
                  <div class="w-9 h-9 rounded-xl flex items-center justify-center
                              font-bold text-sm flex-shrink-0"
                       [class]="getAvatarClass(user.roles?.[0])">
                    {{ getInitials(user.fullName) }}
                  </div>
                  <span class="font-semibold text-gray-800 text-sm">
                    {{ user.fullName }}
                  </span>
                </div>
              </td>

              <!-- Email -->
              <td class="text-gray-500 text-sm">{{ user.email }}</td>

              <!-- Rol -->
              <td>
                @for (role of user.roles ?? []; track role) {
                  <span [class]="getRoleBadgeClass(role)"
                        class="text-xs px-2.5 py-1 rounded-full font-medium mr-1">
                    {{ getRoleLabel(role) }}
                  </span>
                }
              </td>

              <!-- Mağaza -->
              <td>
                @if (user.storeId) {
                  <span class="text-xs bg-blue-50 text-blue-600
                               px-2.5 py-1 rounded-full font-medium">
                    {{ getStoreName(user.storeId) }}
                  </span>
                } @else {
                  <span class="text-gray-300 text-xs">—</span>
                }
              </td>

              <!-- Durum -->
              <td class="text-center">
                <p-tag
                  [value]="user.isActive ? 'Aktif' : 'Pasif'"
                  [severity]="user.isActive ? 'success' : 'danger'"
                  [rounded]="true" />
              </td>

            </tr>
          </ng-template>

          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="5">
                <div class="flex flex-col items-center justify-center
                            py-16 text-gray-300">
                  <i class="pi pi-users text-5xl mb-4"></i>
                  <p class="text-base font-medium">Kullanıcı bulunamadı</p>
                </div>
              </td>
            </tr>
          </ng-template>

        </p-table>
      </div>

    </div>

    <!-- ── Kullanıcı Ekleme Modal ─────────────────────────── -->
    <p-dialog
      [visible]="formVisible"
      (visibleChange)="formVisible = $event"
      [modal]="true"
      [draggable]="false"
      header="Yeni Kullanıcı Ekle"
      [style]="{ width: '500px' }">

      <form [formGroup]="userForm" class="space-y-4 pt-2">

        <div class="grid grid-cols-2 gap-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Ad <span class="text-red-500">*</span>
            </label>
            <input pInputText formControlName="firstName"
                   placeholder="Ad" class="w-full" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Soyad <span class="text-red-500">*</span>
            </label>
            <input pInputText formControlName="lastName"
                   placeholder="Soyad" class="w-full" />
          </div>
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            E-Posta <span class="text-red-500">*</span>
          </label>
          <input pInputText formControlName="email" type="email"
                 placeholder="kasiyer@lcwaikiki.com" class="w-full" />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Şifre
            @if (needsPassword) { <span class="text-red-500">*</span> }
            @else { <span class="text-gray-400 text-xs">(kasiyer POS'a PIN ile girer, opsiyonel)</span> }
          </label>
          <p-password
            formControlName="password"
            [feedback]="true"
            [toggleMask]="true"
            styleClass="w-full"
            inputStyleClass="w-full"
            placeholder="En az 8 karakter" />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Rol <span class="text-red-500">*</span>
          </label>
          <p-dropdown
            formControlName="role"
            [options]="roleOptions"
            optionLabel="label"
            optionValue="value"
            placeholder="Rol seçin..."
            styleClass="w-full"
            (onChange)="onRoleChange($event.value)" />
        </div>

        <!-- Mağaza — Cashier veya StoreManager seçildiyse -->
        @if (needsStore) {
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Mağaza <span class="text-red-500">*</span>
            </label>
            <p-dropdown
              formControlName="storeId"
              [options]="activeStores"
              optionLabel="name"
              optionValue="id"
              placeholder="Mağaza seçin..."
              styleClass="w-full"
              [filter]="true" />
          </div>
        }

      </form>

      <ng-template pTemplate="footer">
        <div class="flex justify-end gap-2">
          <p-button
            label="İptal"
            severity="secondary"
            [outlined]="true"
            (onClick)="formVisible = false" />
          <p-button
            label="Kullanıcı Ekle"
            icon="pi pi-user-plus"
            [loading]="isSubmitting"
            [disabled]="userForm.invalid || isSubmitting"
            (onClick)="onSubmit()" />
        </div>
      </ng-template>

    </p-dialog>

    <p-toast position="top-right" />
  `
})
export class UserListComponent implements OnInit {
  users:          AppUser[] = [];
  stores:         Store[]   = [];
  isLoading       = false;
  isSubmitting    = false;
  formVisible     = false;
  activeRoleFilter = 'all';
  needsStore       = false;
  needsPassword    = true;
  userForm!:       FormGroup;

  roleFilters = [
    { label: 'Tümü',          value: 'all'          },
    { label: 'Admin',         value: 'Admin'         },
    { label: 'Müdür',         value: 'StoreManager'  },
    { label: 'Kasiyer',       value: 'Cashier'       },
  ];

  roleOptions = [
    { label: '🔑 Admin',          value: 'Admin'        },
    { label: '🏪 Mağaza Müdürü',  value: 'StoreManager' },
    { label: '💳 Kasiyer',        value: 'Cashier'      },
  ];

  get activeStores(): Store[] {
    return this.stores.filter(s => s.isActive);
  }

  get filteredUsers(): AppUser[] {
    if (this.activeRoleFilter === 'all') return this.users;
    return this.users.filter(u => (u.roles ?? []).includes(this.activeRoleFilter));
  }

  get roleStats() {
    return [
      { role: 'Admin',        label: 'Admin',
        count: this.countByRole('Admin'),
        color: 'text-purple-600', bgColor: 'bg-purple-50',
        icon: 'pi-shield',        iconColor: 'text-purple-500' },
      { role: 'StoreManager', label: 'Mağaza Müdürü',
        count: this.countByRole('StoreManager'),
        color: 'text-blue-600',   bgColor: 'bg-blue-50',
        icon: 'pi-briefcase',     iconColor: 'text-blue-500' },
      { role: 'Cashier',      label: 'Kasiyer',
        count: this.countByRole('Cashier'),
        color: 'text-emerald-600',bgColor: 'bg-emerald-50',
        icon: 'pi-credit-card',   iconColor: 'text-emerald-500' },
      { role: 'all',          label: 'Toplam',
        count: this.users.length,
        color: 'text-gray-800',   bgColor: 'bg-gray-50',
        icon: 'pi-users',         iconColor: 'text-gray-500' },
    ];
  }

  constructor(
    private userService:    UserService,
    private storeService:   StoreService,
    private fb:             FormBuilder,
    private messageService: MessageService
  ) {}

  ngOnInit(): void {
    this.buildForm();
    this.loadUsers();
    this.loadStores();
  }

  buildForm(): void {
    this.userForm = this.fb.group({
      firstName: ['', Validators.required],
      lastName:  ['', Validators.required],
      email:     ['', [Validators.required, Validators.email]],
      password:  ['', [Validators.required, Validators.minLength(8)]],
      role:      ['', Validators.required],
      storeId:   [null]
    });
  }

  loadUsers(): void {
    this.isLoading = true;
    this.userService.getAll().subscribe({
      next: data => { this.users = data; this.isLoading = false; },
      error: () => {
        this.isLoading = false;
        this.messageService.add({
          severity: 'error', summary: 'Hata',
          detail: 'Kullanıcılar yüklenemedi.'
        });
      }
    });
  }

  loadStores(): void {
    this.storeService.getAll().subscribe({
      next: data => this.stores = data
    });
  }

  openForm(): void {
    this.needsStore = false;
    this.needsPassword = true;
    this.buildForm();
    this.formVisible = true;
  }

  onRoleChange(role: string): void {
    this.needsStore = role === 'Cashier' || role === 'StoreManager';
    if (this.needsStore) {
      this.userForm.get('storeId')?.setValidators(Validators.required);
    } else {
      this.userForm.get('storeId')?.clearValidators();
      this.userForm.get('storeId')?.setValue(null);
    }
    this.userForm.get('storeId')?.updateValueAndValidity();

    // Kasiyer web paneline girmez - şifre opsiyonel (POS: PersonelId + PIN)
    this.needsPassword = role !== 'Cashier';
    const password = this.userForm.get('password');
    password?.setValidators(this.needsPassword
      ? [Validators.required, Validators.minLength(8)]
      : [Validators.minLength(8)]);
    password?.updateValueAndValidity();
  }

  onSubmit(): void {
    if (this.userForm.invalid) return;
    this.isSubmitting = true;
    const v = this.userForm.value;

    this.userService.createUser({
      firstName: v.firstName,
      lastName:  v.lastName,
      email:     v.email,
      password:  v.password || undefined,
      role:      v.role,
      storeId:   v.storeId
    }).subscribe({
      next: (res: any) => {
        this.isSubmitting = false;
        this.formVisible  = false;
        this.loadUsers();
        this.messageService.add({
          severity: 'success', summary: 'Başarılı',
          detail: res?.personelId
            ? `Kullanıcı oluşturuldu. Personel No: ${res.personelId} — PIN: 1234`
            : 'Kullanıcı oluşturuldu.',
          life: 8000
        });
      },
      error: (err) => {
        this.isSubmitting = false;
        const detail = apiErrorMessage(err, 'Kullanıcı oluşturulamadı.');
        this.messageService.add({
          severity: 'error', summary: 'Hata', detail
        });
      }
    });
  }

  getInitials(name?: string | null): string {
    return (name ?? '').split(' ').filter(n => n).map(n => n[0]).join('').substring(0, 2).toUpperCase() || '?';
  }

  getStoreName(storeId: number): string {
    return this.stores.find(s => s.id === storeId)?.name ?? `Mağaza #${storeId}`;
  }

  countByRole(role: string): number {
    return this.users.filter(u => (u.roles ?? []).includes(role)).length;
  }

  getAvatarClass(role?: string): string {
    const map: Record<string, string> = {
      Admin:        'bg-purple-100 text-purple-700',
      StoreManager: 'bg-blue-100 text-blue-700',
      Cashier:      'bg-emerald-100 text-emerald-700',
    };
    return (role && map[role]) || 'bg-gray-100 text-gray-600';
  }

  getRoleBadgeClass(role: string): string {
    const map: Record<string, string> = {
      Admin:        'bg-purple-100 text-purple-700',
      StoreManager: 'bg-blue-100 text-blue-700',
      Cashier:      'bg-emerald-100 text-emerald-700',
      User:         'bg-gray-100 text-gray-600',
    };
    return map[role] ?? 'bg-gray-100 text-gray-600';
  }

  getRoleLabel(role: string): string {
    const map: Record<string, string> = {
      Admin:        '🔑 Admin',
      StoreManager: '🏪 Müdür',
      Cashier:      '💳 Kasiyer',
      User:         '👤 Kullanıcı',
    };
    return map[role] ?? role;
  }
}