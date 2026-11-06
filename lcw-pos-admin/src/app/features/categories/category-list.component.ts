import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, switchMap, of } from 'rxjs';
import { CategoryRow, CategoryService } from '../../core/services/category.service';
import { apiErrorMessage } from '../../core/utils/api.util';

import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageService, ConfirmationService } from 'primeng/api';

/**
 * Kategori yönetimi (Admin). Kategori kodu ürün kodunun 3-4. harfleridir
 * (W6[ER]01Z8): kodlu ürün oluştuktan sonra değiştirilemez; ürünü olan
 * kategori silinemez. Ad ve açıklama her zaman değiştirilebilir.
 */
@Component({
  selector: 'app-category-list',
  standalone: true,
  imports: [CommonModule, FormsModule, TableModule, DialogModule, ToastModule, TooltipModule, ConfirmDialogModule],
  providers: [MessageService, ConfirmationService],
  template: `
    <p-toast />
    <p-confirmDialog />
    <div class="space-y-6">
      <div class="flex items-center justify-between gap-4 flex-wrap">
        <div>
          <h2 class="text-2xl font-bold text-gray-800">Kategoriler</h2>
          <p class="text-gray-400 text-sm mt-1">
            Kategori kodu ürün kodunun parçasıdır (W6<b>ER</b>01Z8). Kategoride kodlu ürün oluştuktan sonra kod değiştirilemez.
          </p>
        </div>
        <button (click)="openNew()"
                class="inline-flex items-center gap-2 bg-red-500 hover:bg-red-600 text-white px-5 py-2.5 rounded-xl font-medium text-sm shadow-sm">
          <i class="pi pi-plus"></i> Yeni Kategori
        </button>
      </div>

      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        <div class="px-6 py-4 border-b border-gray-100">
          <div class="relative w-72">
            <i class="pi pi-search absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm"></i>
            <input [(ngModel)]="search" placeholder="Ad veya kod ara..."
                   class="pl-9 pr-4 py-2 text-sm border border-gray-200 rounded-xl w-full focus:outline-none focus:ring-2 focus:ring-red-300" />
          </div>
        </div>
        <p-table [value]="filtered" [loading]="loading" styleClass="p-datatable-sm" [rowHover]="true"
                 [paginator]="filtered.length > 20" [rows]="20">
          <ng-template pTemplate="header">
            <tr>
              <th class="text-xs font-semibold text-gray-500 uppercase w-24">Kod</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Ad</th>
              <th class="text-xs font-semibold text-gray-500 uppercase">Açıklama</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-right w-28">Ürün</th>
              <th class="text-xs font-semibold text-gray-500 uppercase text-center w-32">İşlem</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-c>
            <tr class="border-b border-gray-50">
              <td>
                <span class="font-mono text-sm bg-gray-100 text-gray-800 px-2.5 py-1 rounded-lg font-bold tracking-widest">
                  {{ c.code || '—' }}
                </span>
                @if (!c.canChangeCode) {
                  <i class="pi pi-lock text-gray-300 text-xs ml-1" pTooltip="Kodlu ürün var: kod kilitli"></i>
                }
              </td>
              <td class="font-medium text-gray-800">{{ c.name }}</td>
              <td class="text-sm text-gray-500 max-w-md truncate">{{ c.description || '—' }}</td>
              <td class="text-right text-sm text-gray-700">{{ c.productCount }}</td>
              <td class="text-center whitespace-nowrap">
                <button (click)="openEdit(c)" pTooltip="Düzenle"
                        class="bg-transparent border-0 cursor-pointer p-2 text-gray-500 hover:text-blue-600">
                  <i class="pi pi-pencil"></i>
                </button>
                <button (click)="confirmDelete(c)" [disabled]="!c.canDelete"
                        [pTooltip]="c.canDelete ? 'Sil' : 'Kategoride ürün var; silinemez'"
                        class="bg-transparent border-0 cursor-pointer p-2 text-gray-500 hover:text-red-600 disabled:opacity-30 disabled:cursor-not-allowed">
                  <i class="pi pi-trash"></i>
                </button>
              </td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr><td colspan="5" class="text-center text-gray-400 py-10">Kategori yok</td></tr>
          </ng-template>
        </p-table>
      </div>
    </div>

    <p-dialog [(visible)]="dialogOpen" [modal]="true" [style]="{ width: '30rem' }"
              [header]="editing ? 'Kategoriyi Düzenle' : 'Yeni Kategori'">
      <div class="space-y-4">
        <label class="block text-sm font-medium text-gray-700">Ad
          <input [(ngModel)]="form.name" (ngModelChange)="nameChanged($event)" maxlength="60"
                 class="mt-1 w-full px-3 py-2 border border-gray-200 rounded-lg text-sm" placeholder="örn. Hırka" />
        </label>
        <label class="block text-sm font-medium text-gray-700">Kod (2 harf)
          <input [(ngModel)]="form.code" (ngModelChange)="codeEdited = true" maxlength="2"
                 [disabled]="editing !== null && !editing.canChangeCode"
                 class="mt-1 w-28 px-3 py-2 border border-gray-200 rounded-lg text-sm font-mono uppercase tracking-widest disabled:bg-gray-100" />
          @if (editing && !editing.canChangeCode) {
            <span class="block text-xs text-gray-400 mt-1">Bu kategoride kodlu ürün var; kod değiştirilemez.</span>
          } @else if (!editing) {
            <span class="block text-xs text-gray-400 mt-1">Addan otomatik önerilir; isterseniz değiştirin.</span>
          }
        </label>
        <label class="block text-sm font-medium text-gray-700">Açıklama
          <textarea [(ngModel)]="form.description" rows="2" maxlength="300"
                    class="mt-1 w-full px-3 py-2 border border-gray-200 rounded-lg text-sm"></textarea>
        </label>
        @if (formError) { <p class="text-sm text-red-600">{{ formError }}</p> }
      </div>
      <ng-template pTemplate="footer">
        <button (click)="dialogOpen = false" class="px-4 py-2 rounded-xl text-sm bg-gray-100 text-gray-700 mr-2">Vazgeç</button>
        <button (click)="save()" [disabled]="saving"
                class="px-5 py-2 rounded-xl text-sm bg-red-500 hover:bg-red-600 text-white font-medium disabled:opacity-60">
          {{ saving ? 'Kaydediliyor...' : 'Kaydet' }}
        </button>
      </ng-template>
    </p-dialog>
  `
})
export class CategoryListComponent implements OnInit {
  private readonly api = inject(CategoryService);
  private readonly toast = inject(MessageService);
  private readonly confirm = inject(ConfirmationService);

  rows: CategoryRow[] = [];
  loading = false;
  search = '';

  dialogOpen = false;
  editing: CategoryRow | null = null;
  form = { name: '', code: '', description: '' };
  codeEdited = false;
  formError = '';
  saving = false;
  private readonly nameInput = new Subject<string>();

  get filtered(): CategoryRow[] {
    const q = this.search.trim().toLocaleLowerCase('tr');
    return q ? this.rows.filter(r => r.name.toLocaleLowerCase('tr').includes(q) || (r.code ?? '').toLowerCase().includes(q)) : this.rows;
  }

  ngOnInit(): void {
    this.load();
    // Yeni kategoride ad yazıldıkça kod önerisi (kullanıcı kodu elle değiştirmediyse)
    this.nameInput.pipe(
      debounceTime(300), distinctUntilChanged(),
      switchMap(name => name.trim().length >= 2 && !this.editing && !this.codeEdited ? this.api.suggestCode(name) : of(null))
    ).subscribe(r => { if (r && !this.codeEdited) this.form.code = r.code; });
  }

  load(): void {
    this.loading = true;
    this.api.getForManagement().subscribe({
      next: rows => { this.rows = rows; this.loading = false; },
      error: err => { this.loading = false; this.notify('error', apiErrorMessage(err, 'Kategoriler yüklenemedi.')); }
    });
  }

  openNew(): void {
    this.editing = null;
    this.form = { name: '', code: '', description: '' };
    this.codeEdited = false;
    this.formError = '';
    this.dialogOpen = true;
  }

  openEdit(row: CategoryRow): void {
    this.editing = row;
    this.form = { name: row.name, code: row.code ?? '', description: row.description ?? '' };
    this.formError = '';
    this.dialogOpen = true;
  }

  nameChanged(name: string): void { this.nameInput.next(name); }

  save(): void {
    const name = this.form.name.trim();
    const code = this.form.code.trim().toUpperCase();
    if (name.length < 2) { this.formError = 'Kategori adı en az 2 karakter olmalıdır.'; return; }
    if (code && !/^[A-Z]{2}$/.test(code)) { this.formError = 'Kod 2 Latin harften oluşmalıdır (örn. HR).'; return; }
    this.saving = true;
    this.formError = '';
    const request = { name, description: this.form.description.trim() || null, code: code || null };

    const done = (message: string) => { this.saving = false; this.dialogOpen = false; this.notify('success', message); this.load(); };
    const fail = (err: unknown) => { this.saving = false; this.formError = apiErrorMessage(err, 'Kaydedilemedi.'); };

    if (!this.editing) {
      this.api.create(request).subscribe({ next: c => done(`'${c.name}' oluşturuldu (kod ${c.code}).`), error: fail });
      return;
    }
    const row = this.editing;
    this.api.update(row.id, request).subscribe({
      next: () => {
        // Kod değiştiyse (ve izinliyse) ayrı uçtan
        if (row.canChangeCode && code && code !== row.code) {
          this.api.updateCode(row.id, code).subscribe({ next: () => done('Kategori güncellendi.'), error: fail });
        } else {
          done('Kategori güncellendi.');
        }
      },
      error: fail
    });
  }

  confirmDelete(row: CategoryRow): void {
    if (!row.canDelete) return;
    this.confirm.confirm({
      header: 'Kategoriyi sil',
      message: `'${row.name}' kategorisi silinsin mi?`,
      acceptLabel: 'Sil', rejectLabel: 'Vazgeç',
      accept: () => this.api.delete(row.id).subscribe({
        next: () => { this.notify('success', `'${row.name}' silindi.`); this.load(); },
        error: err => this.notify('error', apiErrorMessage(err, 'Silinemedi.'))
      })
    });
  }

  private notify(severity: 'success' | 'error', detail: string): void {
    this.toast.add({ severity, summary: severity === 'success' ? 'Tamam' : 'Hata', detail, life: 4000 });
  }
}
