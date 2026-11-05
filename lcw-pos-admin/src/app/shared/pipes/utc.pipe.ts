import { Pipe, PipeTransform } from '@angular/core';

/**
 * Backend tarihleri UTC saklar ama JSON'da saat dilimi eki ("Z") olmadan
 * döner ("2026-11-03T09:15:00"). Tarayıcı bunu YEREL saat sanır ve Türkiye'de
 * saatler 3 saat geri görünür. Bu pipe eksik eki ekleyip doğru anı üretir:
 *   {{ sale.saleDate | utc | date:'dd.MM.yyyy HH:mm' }}
 */
@Pipe({ name: 'utc', standalone: true })
export class UtcPipe implements PipeTransform {
  transform(value: string | Date | null | undefined): Date | null {
    if (value == null || value === '') return null;
    if (value instanceof Date) return value;
    const hasZone = /([zZ]|[+-]\d{2}:?\d{2})$/.test(value);
    const date = new Date(hasZone ? value : `${value}Z`);
    return isNaN(date.getTime()) ? null : date;
  }
}
