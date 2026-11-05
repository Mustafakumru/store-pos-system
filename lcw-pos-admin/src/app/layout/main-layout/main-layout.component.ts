import { Component, computed, OnInit, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { apiErrorMessage } from '../../core/utils/api.util';
import { SidebarComponent } from './sidebar/sidebar.component';
import { AuthService } from '../../core/services/auth.service';
import { StoreService } from '../../core/services/store.service';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, CommonModule, FormsModule, DialogModule],
  template: `
    <div class="flex h-screen overflow-hidden bg-gray-100">
      <app-sidebar />
      <div class="flex-1 flex flex-col overflow-hidden">

        <!-- Topbar -->
        <header class="bg-white shadow-sm px-6 py-4 flex items-center
                       justify-between border-b border-gray-100">
          <div class="flex items-center gap-3">
            <h1 class="text-xl font-semibold text-gray-700">
              {{ panelTitle() }}
            </h1>
            @if (isStoreManager()) {
              <span class="text-xs bg-emerald-100 text-emerald-700
                           px-3 py-1 rounded-full font-semibold">
                {{ storeName() }}
              </span>
            }
          </div>

          <div class="flex items-center gap-3">
            <!-- Rol Rozeti -->
            <span [class]="isAdmin()
              ? 'bg-red-50 text-red-600'
              : 'bg-blue-50 text-blue-600'"
              class="text-xs px-2.5 py-1 rounded-full font-medium">
              {{ isAdmin() ? '🔑 Admin' : '🏪 Mağaza Yöneticisi' }}
            </span>

            <i class="pi pi-user text-gray-400"></i>
            <span class="text-sm text-gray-600 font-medium">
              {{ username() }}
            </span>

            <button
              (click)="logout()"
              class="ml-2 text-sm bg-red-50 text-red-600 hover:bg-red-100
                     px-3 py-1.5 rounded-lg transition-colors font-medium
                     flex items-center gap-1.5">
              <i class="pi pi-sign-out"></i>
              Çıkış
            </button>
          </div>
        </header>

        <!-- POS PIN'i hâlâ varsayılan: panel kullanılabilir ama müdür onayı veremez -->
        @if (pinIsDefault()) {
          <div class="bg-amber-50 border-b border-amber-200 px-6 py-3 flex items-center
                      justify-between gap-4 text-sm text-amber-800">
            <span>
              <i class="pi pi-exclamation-triangle mr-2"></i>
              POS PIN'iniz hâlâ varsayılan (1234). PIN belirlemeden kasada yönetici onayı veremezsiniz.
            </span>
            <button (click)="openPinDialog()"
              class="bg-amber-600 hover:bg-amber-700 text-white px-3 py-1.5 rounded-lg font-medium">
              PIN Belirle
            </button>
          </div>
        }

        <p-dialog header="POS PIN'i Belirle" [(visible)]="pinDialogVisible" [modal]="true"
                  [style]="{ width: '360px' }" [draggable]="false">
          <div class="flex flex-col gap-3">
            <label class="text-sm text-gray-600">Mevcut PIN
              <input type="password" inputmode="numeric" maxlength="6" [(ngModel)]="oldPin"
                     class="mt-1 w-full border rounded-lg px-3 py-2" autocomplete="off" />
            </label>
            <label class="text-sm text-gray-600">Yeni PIN (4-6 rakam)
              <input type="password" inputmode="numeric" maxlength="6" [(ngModel)]="newPin"
                     class="mt-1 w-full border rounded-lg px-3 py-2" autocomplete="off" />
            </label>
            <label class="text-sm text-gray-600">Yeni PIN (tekrar)
              <input type="password" inputmode="numeric" maxlength="6" [(ngModel)]="newPin2"
                     class="mt-1 w-full border rounded-lg px-3 py-2" autocomplete="off" />
            </label>
            @if (pinError()) {
              <div class="text-sm text-red-600">{{ pinError() }}</div>
            }
            <button (click)="savePin()" [disabled]="pinSaving()"
              class="bg-emerald-600 hover:bg-emerald-700 disabled:opacity-60 text-white
                     px-4 py-2 rounded-lg font-medium">
              {{ pinSaving() ? 'Kaydediliyor...' : 'Kaydet' }}
            </button>
          </div>
        </p-dialog>

        <!-- İçerik -->
        <main class="flex-1 overflow-y-auto p-6">
          <router-outlet />
        </main>

      </div>
    </div>
  `
})
export class MainLayoutComponent implements OnInit {
  storeName = signal<string>('');

  readonly isAdmin        = this.authService.isAdmin;
  readonly isStoreManager = this.authService.isStoreManager;

  readonly username = computed(() => {
    const user = this.authService.currentUser();
    if (!user) return 'Kullanıcı';
    return user.unique_name ?? user.email ?? 'Kullanıcı';
  });

  readonly pinIsDefault = computed(() =>
    !!this.authService.currentUser()?.pinIsDefault && !!this.authService.currentUser()?.personelId);

  pinDialogVisible = false;
  oldPin = '';
  newPin = '';
  newPin2 = '';
  readonly pinError  = signal<string | null>(null);
  readonly pinSaving = signal(false);

  openPinDialog(): void {
    this.oldPin = this.newPin = this.newPin2 = '';
    this.pinError.set(null);
    this.pinDialogVisible = true;
  }

  savePin(): void {
    if (!/^\d{4,6}$/.test(this.newPin)) { this.pinError.set('Yeni PIN 4-6 rakam olmalıdır.'); return; }
    if (this.newPin !== this.newPin2)   { this.pinError.set('Yeni PIN tekrarı eşleşmiyor.'); return; }
    this.pinSaving.set(true);
    this.pinError.set(null);
    this.authService.changePin(this.oldPin, this.newPin).subscribe({
      next: () => { this.pinSaving.set(false); this.pinDialogVisible = false; },
      error: err => {
        this.pinSaving.set(false);
        this.pinError.set(apiErrorMessage(err, 'PIN değiştirilemedi.'));
      }
    });
  }

  readonly panelTitle = computed(() => {
    if (this.isAdmin()) return 'LC Waikiki — POS Yönetim Paneli';
    return 'LC Waikiki — Mağaza Paneli';
  });

  constructor(
    private authService:  AuthService,
    private storeService: StoreService
  ) {}

  ngOnInit(): void {
    // Mağaza yöneticisi ise mağaza adını çek
    if (this.isStoreManager()) {
      const storeId = this.authService.storeId();
      if (storeId) {
        this.storeService.getById(+storeId).subscribe({
          next: store => this.storeName.set(store.name),
          error: () => {}
        });
      }
    }
  }

  logout(): void {
    this.authService.logout();
  }
}