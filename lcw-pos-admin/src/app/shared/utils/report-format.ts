import { BarDatum } from '../components/bar-chart.component';
import { DailyBucket, HourlyBucket } from '../models/report.model';

const moneyFmt = new Intl.NumberFormat('tr-TR', {
  style: 'currency', currency: 'TRY', maximumFractionDigits: 0
});

export const formatMoney = (v: number): string => moneyFmt.format(v ?? 0);

/**
 * Saatlik kovaları grafiğe çevirir. Boş saatler 0 olarak doldurulur ki
 * eksen sürekli olsun; aralık en az 09:00–21:00 (mağaza saatleri),
 * bunun dışında satış varsa genişler.
 */
export function hourlyToBars(hourly: HourlyBucket[]): BarDatum[] {
  if (!hourly?.length) return [];
  const byHour = new Map(hourly.map(h => [h.hour, h]));
  const first = Math.min(9, ...hourly.map(h => h.hour));
  const last  = Math.max(21, ...hourly.map(h => h.hour));
  const bars: BarDatum[] = [];
  for (let h = first; h <= last; h++) {
    const b = byHour.get(h);
    bars.push({
      label:  `${String(h).padStart(2, '0')}:00`,
      value:  b?.revenue ?? 0,
      detail: `${b?.saleCount ?? 0} satış`
    });
  }
  return bars;
}

/** Günlük kovaları grafiğe çevirir; aradaki satışsız günler 0 ile doldurulur. */
export function dailyToBars(daily: DailyBucket[], from: Date, to: Date): BarDatum[] {
  const byDate = new Map((daily ?? []).map(d => [d.date, d]));
  const bars: BarDatum[] = [];
  const cursor = new Date(from);
  cursor.setHours(0, 0, 0, 0);
  while (cursor < to) {
    const key = `${cursor.getFullYear()}-${String(cursor.getMonth() + 1).padStart(2, '0')}-${String(cursor.getDate()).padStart(2, '0')}`;
    const d = byDate.get(key);
    bars.push({
      label:  cursor.toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit' }),
      value:  d?.revenue ?? 0,
      detail: `${d?.saleCount ?? 0} satış`
    });
    cursor.setDate(cursor.getDate() + 1);
  }
  return bars;
}

/**
 * Excel (Türkçe) uyumlu CSV indirir: ";" ayraç, UTF-8 BOM, virgüllü ondalık.
 */
export function downloadCsv(fileName: string, headers: string[], rows: (string | number | null | undefined)[][]): void {
  const esc = (v: string | number | null | undefined): string => {
    if (v == null) return '';
    const s = typeof v === 'number' ? v.toLocaleString('tr-TR', { useGrouping: false, maximumFractionDigits: 2 }) : String(v);
    return /[";\n\r]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const csv = [headers, ...rows].map(r => r.map(esc).join(';')).join('\r\n');
  const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}
