import { Component, Input, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface BarDatum {
  label:   string;   // x ekseni etiketi (örn. "14:00", "03.11")
  value:   number;   // sütun yüksekliği
  detail?: string;   // ipucunda değerin altına yazılan ek bilgi
}

/**
 * Tek serili, bağımlılıksız sütun grafiği.
 * - İnce sütunlar, 4px yuvarlak uç (tabana oturur), sütunlar arası boşluk
 * - Üzerine gelince ipucu (isabet alanı sütunun tüm yüksekliği)
 * - Sadece en yüksek sütunun değeri doğrudan etiketlenir
 * - Soluk ızgara: sadece taban çizgisi + üst referans değeri
 */
@Component({
  selector: 'app-bar-chart',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (bars().length === 0) {
      <div class="flex items-center justify-center text-sm text-gray-400"
           [style.height.px]="height">
        {{ emptyText }}
      </div>
    } @else {
      <div class="relative select-none" role="img" [attr.aria-label]="ariaLabel">

        <div class="relative flex items-end gap-[2px] border-b border-gray-200 mt-4"
             [style.height.px]="height">
          <!-- Üst referans çizgisi -->
          <div class="absolute left-0 right-0 top-0 border-t border-dashed border-gray-100"></div>

          @for (b of bars(); track b.label; let i = $index) {
            <div class="relative flex-1 h-full flex justify-center items-end cursor-default"
                 (mouseenter)="hover.set(i)" (mouseleave)="hover.set(null)">
              @if (b.isMax && b.value > 0 && hover() === null) {
                <span class="absolute text-[11px] font-semibold text-gray-600 whitespace-nowrap mb-0.5"
                      [style.bottom.%]="b.pct">
                  {{ format(b.value) }}
                </span>
              }
              <div class="w-full max-w-[28px] rounded-t transition-opacity"
                   [style.height.%]="b.pct"
                   [style.min-height.px]="b.value > 0 ? 2 : 0"
                   [style.background]="color"
                   [style.opacity]="hover() === null || hover() === i ? 1 : 0.45"></div>

              @if (hover() === i) {
                <div class="absolute bottom-full mb-1 z-10 bg-gray-900 text-white
                            text-xs rounded-lg px-2.5 py-1.5 shadow-lg whitespace-nowrap
                            pointer-events-none"
                     [class.left-0]="i < bars().length / 2"
                     [class.right-0]="i >= bars().length / 2">
                  <p class="text-gray-300">{{ b.label }}</p>
                  <p class="font-semibold">{{ format(b.value) }}</p>
                  @if (b.detail) { <p class="text-gray-300">{{ b.detail }}</p> }
                </div>
              }
            </div>
          }
        </div>

        <!-- X ekseni etiketleri (kalabalıksa seyreltilir) -->
        <div class="flex gap-[2px] mt-1">
          @for (b of bars(); track b.label; let i = $index) {
            <div class="flex-1 text-center text-[10px] text-gray-400 truncate">
              {{ i % labelStep() === 0 ? b.label : '' }}
            </div>
          }
        </div>
      </div>
    }
  `
})
export class BarChartComponent {
  private readonly _data = signal<BarDatum[]>([]);
  @Input() set data(value: BarDatum[] | null | undefined) { this._data.set(value ?? []); }

  @Input() height    = 180;
  @Input() color     = '#3b82f6';
  @Input() emptyText = 'Bu dönemde veri yok';
  @Input() ariaLabel = 'Sütun grafiği';
  @Input() formatter: (v: number) => string = v => v.toLocaleString('tr-TR');

  readonly hover = signal<number | null>(null);

  readonly maxValue = computed(() => Math.max(0, ...this._data().map(d => d.value)));

  readonly bars = computed(() => {
    const max = this.maxValue();
    let maxIdx = -1;
    this._data().forEach((d, i) => { if (maxIdx < 0 || d.value > this._data()[maxIdx].value) maxIdx = i; });
    return this._data().map((d, i) => ({
      ...d,
      pct:   max > 0 ? Math.max(0, d.value) / max * 100 : 0,
      isMax: i === maxIdx
    }));
  });

  readonly labelStep = computed(() => Math.max(1, Math.ceil(this._data().length / 12)));

  format(v: number): string { return this.formatter(v); }
}
