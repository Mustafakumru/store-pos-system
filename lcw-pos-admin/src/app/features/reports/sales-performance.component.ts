import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { PerformanceService } from '../../core/services/performance.service';
import { apiErrorMessage } from '../../core/utils/api.util';
import { Pace, SalesPerformance, StorePerformance, StoreTarget } from '../../shared/models/performance.model';
import { BarChartComponent, BarDatum } from '../../shared/components/bar-chart.component';

import { CalendarModule } from 'primeng/calendar';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';

type Preset = 'today' | 'week' | 'month' | 'lastMonth' | 'custom';
type SortKey = 'rank' | 'netRevenue' | 'achievementPercent' | 'saleCount' | 'averageBasket' | 'changePercent';

/**
 * Tüm mağazaların satış performansı (Admin): net ciro, hedef gerçekleşmesi,
 * hedef temposu (olması gereken ilerlemeye göre önde/geride), sıralama,
 * günlük seyir ve en çok satanlar. Aylık hedefler buradan düzenlenir.
 */
@Component({
  selector: 'app-sales-performance',
  standalone: true,
  imports: [CommonModule, FormsModule, CalendarModule, DialogModule, ToastModule, TooltipModule, BarChartComponent],
  providers: [MessageService],
  template: `
    <p-toast />
    <div class="space-y-6">
      <!-- ── Başlık + dönem ─────────────────────────────────── -->
      <div class="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h2 class="text-2xl font-bold text-gray-800">Satış Performansı</h2>
          <p class="text-gray-400 text-sm mt-1">
            Net ciro = satış − iade (mağaza panelindeki ve X/Z raporundaki tanımla aynı). Hedefler aylıktır; kısa dönemlerde gün sayısına göre orantılanır.
          </p>
        </div>
        <button (click)="openTargets()"
                class="inline-flex items-center gap-2 bg-red-500 hover:bg-red-600 text-white px-5 py-2.5 rounded-xl font-medium text-sm shadow-sm">
          <i class="pi pi-flag"></i> Hedefleri Düzenle
        </button>
      </div>

      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-4 flex items-center gap-2 flex-wrap">
        @for (p of presets; track p.key) {
          <button (click)="setPreset(p.key)"
                  [class]="preset() === p.key ? 'bg-red-500 text-white' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'"
                  class="px-3 py-1.5 rounded-xl text-sm font-medium transition-colors">{{ p.label }}</button>
        }
        <span class="flex-1"></span>
        <p-calendar [(ngModel)]="customRange" selectionMode="range" dateFormat="dd.mm.yy" [showIcon]="true"
                    [readonlyInput]="true" placeholder="Özel aralık" inputStyleClass="text-sm w-56"
                    (onSelect)="customSelected()" />
      </div>

      @if (error()) { <div class="bg-red-50 text-red-700 border border-red-200 rounded-xl px-4 py-3 text-sm">{{ error() }}</div> }

      @if (data(); as d) {
        <p class="text-sm text-gray-500 -mt-2">
          {{ trDate(d.from) }} – {{ trDate(d.to) }} · {{ d.days }} gün
          @if (d.elapsedDays < d.days && d.elapsedDays > 0) { · {{ d.elapsedDays }}. gün }
        </p>

        <!-- ── KPI kartları ──────────────────────────────────── -->
        <div class="grid grid-cols-2 xl:grid-cols-5 gap-4">
          <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100 col-span-2 xl:col-span-1">
            <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">Net Ciro</p>
            <p class="text-3xl font-bold text-gray-800 mt-1">{{ money(d.totals.netRevenue) }}</p>
            <p class="text-xs mt-1" [class]="changeClass(d.totals.changePercent)">
              {{ changeText(d.totals.changePercent) }} <span class="text-gray-400">önceki döneme göre</span>
            </p>
          </div>
          <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
            <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">Hedef Gerçekleşme</p>
            @if (d.totals.achievementPercent !== null) {
              <p class="text-3xl font-bold mt-1" [class]="pctColor(d.totals.achievementPercent)">%{{ d.totals.achievementPercent }}</p>
              <p class="text-xs text-gray-400 mt-1">Hedef {{ money(d.totals.targetAmount!) }}</p>
            } @else {
              <p class="text-xl font-semibold text-gray-300 mt-2">Hedef girilmemiş</p>
            }
          </div>
          <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
            <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">Satış</p>
            <p class="text-3xl font-bold text-gray-800 mt-1">{{ d.totals.saleCount }}</p>
            <p class="text-xs text-gray-400 mt-1">{{ d.totals.itemsSold }} ürün</p>
          </div>
          <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
            <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">Ortalama Sepet</p>
            <p class="text-3xl font-bold text-gray-800 mt-1">{{ money(d.totals.averageBasket) }}</p>
          </div>
          <div class="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
            <p class="text-xs font-semibold text-gray-400 uppercase tracking-wide">İade</p>
            <p class="text-3xl font-bold text-orange-500 mt-1">{{ money(d.totals.refundAmount) }}</p>
            <p class="text-xs text-gray-400 mt-1">{{ d.totals.refundCount }} işlem</p>
          </div>
        </div>

        <!-- ── Mağaza sıralaması ─────────────────────────────── -->
        <div class="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-x-auto">
          <div class="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
            <h3 class="font-semibold text-gray-800">Mağaza Sıralaması</h3>
            <span class="text-xs text-gray-400">Sıra: hedefe ulaşma oranı, sonra net ciro · başlığa tıklayarak sıralayın</span>
          </div>
          <table class="w-full text-sm">
            <thead>
              <tr class="text-xs text-gray-500 uppercase border-b border-gray-100">
                <th class="text-left px-4 py-3 cursor-pointer" (click)="sortBy('rank')">#{{ arrow('rank') }}</th>
                <th class="text-left px-2 py-3">Mağaza</th>
                <th class="text-right px-2 py-3 cursor-pointer" (click)="sortBy('netRevenue')">Net ciro{{ arrow('netRevenue') }}</th>
                <th class="text-left px-4 py-3 w-[30%] cursor-pointer" (click)="sortBy('achievementPercent')">Hedef gerçekleşme{{ arrow('achievementPercent') }}</th>
                <th class="text-left px-2 py-3">Durum</th>
                <th class="text-right px-2 py-3 cursor-pointer" (click)="sortBy('saleCount')">Satış{{ arrow('saleCount') }}</th>
                <th class="text-right px-2 py-3 cursor-pointer" (click)="sortBy('averageBasket')">Ort. sepet{{ arrow('averageBasket') }}</th>
                <th class="text-right px-4 py-3 cursor-pointer" (click)="sortBy('changePercent')">Önceki dön.{{ arrow('changePercent') }}</th>
              </tr>
            </thead>
            <tbody>
              @for (s of sortedStores(); track s.storeId) {
                <tr class="border-b border-gray-50 hover:bg-gray-50">
                  <td class="px-4 py-3 font-bold text-gray-700 whitespace-nowrap">
                    @if (s.rank === 1 && s.netRevenue > 0) { 🥇 }
                    @else if (s.rank === 2 && s.netRevenue > 0) { 🥈 }
                    @else if (s.rank === 3 && s.netRevenue > 0) { 🥉 }
                    @else { {{ s.rank }} }
                  </td>
                  <td class="px-2 py-3">
                    <p class="font-medium text-gray-800">{{ s.storeName }}</p>
                    <p class="text-xs text-gray-400 font-mono">{{ s.storeCode }} · ciro payı %{{ s.shareOfTotal }}</p>
                  </td>
                  <td class="px-2 py-3 text-right font-semibold text-gray-800 whitespace-nowrap">
                    {{ money(s.netRevenue) }}
                    @if (s.refundAmount > 0) { <p class="text-xs font-normal text-orange-500">iade −{{ money(s.refundAmount) }}</p> }
                  </td>
                  <td class="px-4 py-3">
                    @if (s.achievementPercent !== null) {
                      <div class="flex items-center gap-2">
                        <div class="relative flex-1 h-3 bg-gray-100 rounded-full overflow-hidden"
                             [pTooltip]="'Olması gereken: %' + s.expectedPercent + ' · Kalan: ' + money(s.remainingToTarget ?? 0)">
                          <div class="h-full rounded-full" [class]="barColor(s.pace)"
                               [style.width.%]="min(s.achievementPercent, 100)"></div>
                          @if (s.expectedPercent !== null && s.expectedPercent < 100) {
                            <div class="absolute top-0 h-full w-0.5 bg-gray-700" [style.left.%]="s.expectedPercent"></div>
                          }
                        </div>
                        <span class="w-14 text-right font-semibold" [class]="pctColor(s.achievementPercent)">%{{ s.achievementPercent }}</span>
                      </div>
                      <p class="text-xs text-gray-400 mt-1">Hedef {{ money(s.targetAmount!) }}</p>
                    } @else {
                      <span class="text-xs text-gray-300">Hedef girilmemiş</span>
                    }
                  </td>
                  <td class="px-2 py-3"><span class="text-xs font-semibold px-2 py-1 rounded-lg" [class]="paceClass(s.pace)">{{ s.pace }}</span></td>
                  <td class="px-2 py-3 text-right text-gray-700">{{ s.saleCount }}</td>
                  <td class="px-2 py-3 text-right text-gray-700 whitespace-nowrap">{{ money(s.averageBasket) }}</td>
                  <td class="px-4 py-3 text-right whitespace-nowrap" [class]="changeClass(s.changePercent)">{{ changeText(s.changePercent) }}</td>
                </tr>
              } @empty {
                <tr><td colspan="8" class="text-center text-gray-400 py-10">Mağaza yok</td></tr>
              }
            </tbody>
          </table>
          <p class="px-6 py-3 text-xs text-gray-400 border-t border-gray-100">
            Çubuktaki dikey çizgi: dönemin bugüne kadar geçen kısmına göre olması gereken ilerleme.
            Önde/Geride: bu çizginin 5 puan üstü/altı.
          </p>
        </div>

        <!-- ── Günlük seyir + en çok satanlar ───────────────── -->
        <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
          <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-5 xl:col-span-2">
            <h3 class="font-semibold text-gray-800 mb-3">Günlük net ciro (tüm mağazalar)</h3>
            <app-bar-chart [data]="dailyBars()" [height]="180" ariaLabel="Günlük net ciro"
                           emptyText="Bu dönemde satış yok" [formatter]="money" />
          </div>
          <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
            <h3 class="font-semibold text-gray-800 mb-3">En çok satanlar</h3>
            @for (p of d.topProducts; track p.variantId; let i = $index) {
              <div class="flex items-center gap-3 py-2 border-b border-gray-50 last:border-0">
                <span class="w-5 text-xs font-bold text-gray-400">{{ i + 1 }}</span>
                <div class="flex-1 min-w-0">
                  <p class="text-sm font-medium text-gray-800 truncate">{{ p.productName }}</p>
                  <p class="text-xs text-gray-400">{{ p.color }} {{ p.size }}</p>
                </div>
                <div class="text-right">
                  <p class="text-sm font-semibold text-gray-800">{{ p.quantity }} adet</p>
                  <p class="text-xs text-gray-400">{{ money(p.revenue) }}</p>
                </div>
              </div>
            } @empty {
              <p class="text-sm text-gray-300 py-6 text-center">Satış yok</p>
            }
          </div>
        </div>
      } @else if (loading()) {
        <p class="text-gray-400 text-sm">Yükleniyor...</p>
      }
    </div>

    <!-- ── Hedef düzenleme ───────────────────────────────────── -->
    <p-dialog [(visible)]="targetsOpen" [modal]="true" [style]="{ width: '36rem' }" header="Aylık Ciro Hedefleri">
      <div class="flex items-center gap-2 mb-4">
        <button (click)="shiftMonth(-1)" class="px-3 py-1.5 bg-gray-100 rounded-lg"><i class="pi pi-chevron-left"></i></button>
        <span class="font-semibold text-gray-800 w-40 text-center">{{ monthLabel() }}</span>
        <button (click)="shiftMonth(1)" class="px-3 py-1.5 bg-gray-100 rounded-lg"><i class="pi pi-chevron-right"></i></button>
        <span class="flex-1"></span>
        <button (click)="copyPreviousMonth()" class="text-xs text-red-600 underline bg-transparent border-0 cursor-pointer">Önceki ayı kopyala</button>
      </div>
      <div class="max-h-96 overflow-auto">
        @for (t of targets; track t.storeId) {
          <div class="flex items-center gap-3 py-2 border-b border-gray-50">
            <div class="flex-1">
              <p class="text-sm font-medium text-gray-800">{{ t.storeName }}</p>
              <p class="text-xs text-gray-400 font-mono">{{ t.storeCode }}</p>
            </div>
            <div class="relative">
              <input type="number" min="0" step="1000" [(ngModel)]="edits[t.storeId]" placeholder="Hedef yok"
                     class="w-44 px-3 py-2 border border-gray-200 rounded-lg text-sm text-right pr-8" />
              <span class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm">₺</span>
            </div>
          </div>
        }
      </div>
      <p class="text-xs text-gray-400 mt-3">Boş ya da 0: o ayın hedefi kaldırılır. Hedef değişiklikleri işlem kaydına yazılır.</p>
      @if (targetError) { <p class="text-sm text-red-600 mt-2">{{ targetError }}</p> }
      <ng-template pTemplate="footer">
        <button (click)="targetsOpen = false" class="px-4 py-2 rounded-xl text-sm bg-gray-100 text-gray-700 mr-2">Vazgeç</button>
        <button (click)="saveTargets()" [disabled]="savingTargets"
                class="px-5 py-2 rounded-xl text-sm bg-red-500 hover:bg-red-600 text-white font-medium disabled:opacity-60">
          {{ savingTargets ? 'Kaydediliyor...' : 'Kaydet' }}
        </button>
      </ng-template>
    </p-dialog>
  `
})
export class SalesPerformanceComponent implements OnInit {
  private readonly api = inject(PerformanceService);
  private readonly toast = inject(MessageService);

  readonly presets: { key: Preset; label: string }[] = [
    { key: 'today', label: 'Bugün' }, { key: 'week', label: 'Bu hafta' },
    { key: 'month', label: 'Bu ay' }, { key: 'lastMonth', label: 'Geçen ay' }
  ];

  preset = signal<Preset>('month');
  data = signal<SalesPerformance | null>(null);
  loading = signal(false);
  error = signal('');
  customRange: Date[] | null = null;
  sortKey = signal<SortKey>('rank');
  sortDesc = signal(false);

  // Hedef penceresi
  targetsOpen = false;
  targetMonth = signal(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  targets: StoreTarget[] = [];
  edits: Record<number, number | null> = {};
  private original: Record<number, number | null> = {};
  savingTargets = false;
  targetError = '';

  readonly money = (v: number) => '₺' + Math.round(v).toLocaleString('tr-TR');
  readonly min = Math.min;
  /** "2026-11-06" -> "06.11.2026" (yerel gün; Date'e çevirmeden, saat dilimi kayması olmasın) */
  trDate(ymdStr: string): string { return `${ymdStr.slice(8, 10)}.${ymdStr.slice(5, 7)}.${ymdStr.slice(0, 4)}`; }

  readonly dailyBars = computed<BarDatum[]>(() => (this.data()?.daily ?? []).map(d => ({
    label: d.date.slice(8, 10) + '.' + d.date.slice(5, 7),
    value: d.netRevenue,
    detail: `${d.saleCount} satış`
  })));

  readonly sortedStores = computed<StorePerformance[]>(() => {
    const rows = [...(this.data()?.stores ?? [])];
    const key = this.sortKey();
    const dir = this.sortDesc() ? -1 : 1;
    if (key === 'rank') return this.sortDesc() ? rows.reverse() : rows;
    return rows.sort((a, b) => ((a[key] ?? -Infinity) as number) > ((b[key] ?? -Infinity) as number) ? -dir : dir);
  });

  ngOnInit(): void { this.setPreset('month'); }

  setPreset(p: Preset): void {
    this.preset.set(p);
    this.customRange = null;
    const t = new Date(); t.setHours(0, 0, 0, 0);
    let from = new Date(t), to = new Date(t);
    if (p === 'week') { const dow = (t.getDay() + 6) % 7; from.setDate(t.getDate() - dow); to = new Date(from); to.setDate(from.getDate() + 6); }
    if (p === 'month') { from = new Date(t.getFullYear(), t.getMonth(), 1); to = new Date(t.getFullYear(), t.getMonth() + 1, 0); }
    if (p === 'lastMonth') { from = new Date(t.getFullYear(), t.getMonth() - 1, 1); to = new Date(t.getFullYear(), t.getMonth(), 0); }
    this.load(from, to);
  }

  customSelected(): void {
    const r = this.customRange;
    if (r && r[0] && r[1]) { this.preset.set('custom'); this.load(r[0], r[1]); }
  }

  private lastRange: [Date, Date] | null = null;

  load(from: Date, to: Date): void {
    this.lastRange = [from, to];
    this.loading.set(true);
    this.error.set('');
    this.api.get(from, to).subscribe({
      next: d => { this.data.set(d); this.loading.set(false); },
      error: e => { this.loading.set(false); this.error.set(apiErrorMessage(e, 'Rapor yüklenemedi.')); }
    });
  }

  sortBy(key: SortKey): void {
    if (this.sortKey() === key) this.sortDesc.set(!this.sortDesc());
    else { this.sortKey.set(key); this.sortDesc.set(key !== 'rank'); }
  }

  arrow(key: SortKey): string {
    return this.sortKey() !== key ? '' : this.sortDesc() ? ' ▼' : ' ▲';
  }

  // ── Görünüm yardımcıları ─────────────────────────────────
  pctColor(p: number | null): string {
    if (p === null) return 'text-gray-400';
    return p >= 100 ? 'text-green-600' : p >= 75 ? 'text-blue-600' : p >= 50 ? 'text-amber-600' : 'text-red-600';
  }

  barColor(pace: Pace): string {
    return pace === 'Hedef tamam' ? 'bg-green-500' : pace === 'Önde' ? 'bg-emerald-500'
      : pace === 'Hedefte' ? 'bg-blue-500' : pace === 'Geride' ? 'bg-red-500' : 'bg-gray-400';
  }

  paceClass(pace: Pace): string {
    return pace === 'Hedef tamam' ? 'bg-green-50 text-green-700' : pace === 'Önde' ? 'bg-emerald-50 text-emerald-700'
      : pace === 'Hedefte' ? 'bg-blue-50 text-blue-700' : pace === 'Geride' ? 'bg-red-50 text-red-700' : 'bg-gray-100 text-gray-500';
  }

  changeText(p: number | null): string { return p === null ? '—' : (p > 0 ? '▲ %' : p < 0 ? '▼ %' : '%') + Math.abs(p); }
  changeClass(p: number | null): string { return p === null ? 'text-gray-400' : p > 0 ? 'text-green-600' : p < 0 ? 'text-red-600' : 'text-gray-500'; }

  // ── Hedefler ─────────────────────────────────────────────
  monthLabel(): string {
    return this.targetMonth().toLocaleDateString('tr-TR', { month: 'long', year: 'numeric' });
  }

  openTargets(): void {
    this.targetError = '';
    this.targetsOpen = true;
    this.loadTargets();
  }

  shiftMonth(delta: number): void {
    const m = this.targetMonth();
    this.targetMonth.set(new Date(m.getFullYear(), m.getMonth() + delta, 1));
    this.loadTargets();
  }

  private loadTargets(): void {
    const m = this.targetMonth();
    this.api.getTargets(m.getFullYear(), m.getMonth() + 1).subscribe({
      next: list => {
        this.targets = list;
        this.edits = Object.fromEntries(list.map(t => [t.storeId, t.targetAmount]));
        this.original = { ...this.edits };
      },
      error: e => this.targetError = apiErrorMessage(e, 'Hedefler yüklenemedi.')
    });
  }

  copyPreviousMonth(): void {
    const m = this.targetMonth();
    const prev = new Date(m.getFullYear(), m.getMonth() - 1, 1);
    this.api.getTargets(prev.getFullYear(), prev.getMonth() + 1).subscribe(list => {
      for (const t of list) if (t.targetAmount !== null) this.edits[t.storeId] = t.targetAmount;
    });
  }

  saveTargets(): void {
    const m = this.targetMonth();
    const changed = this.targets.filter(t => (this.edits[t.storeId] ?? null) !== (this.original[t.storeId] ?? null));
    if (changed.some(t => (this.edits[t.storeId] ?? 0) < 0)) { this.targetError = 'Hedef negatif olamaz.'; return; }
    if (!changed.length) { this.targetsOpen = false; return; }
    this.savingTargets = true;
    forkJoin(changed.map(t => this.api.setTarget(t.storeId, m.getFullYear(), m.getMonth() + 1, this.edits[t.storeId] || null))).subscribe({
      next: () => {
        this.savingTargets = false;
        this.targetsOpen = false;
        this.toast.add({ severity: 'success', summary: 'Tamam', detail: `${changed.length} mağazanın hedefi kaydedildi.`, life: 3000 });
        if (this.lastRange) this.load(...this.lastRange);
      },
      error: e => { this.savingTargets = false; this.targetError = apiErrorMessage(e, 'Hedefler kaydedilemedi.'); }
    });
  }
}
