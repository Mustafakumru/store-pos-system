import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class BarcodeService {

  // LC Waikiki Türkiye GS1 prefix: 8684
  private readonly PREFIX = '8684';

  // Kategori kodları
  private readonly CATEGORY_CODES: Record<number, string> = {
    1: '01', // Kadın Giyim
    2: '02', // Erkek Giyim
    3: '03', // Çocuk Giyim
    4: '04', // Aksesuar
    5: '05', // İç Giyim
    6: '06', // Spor Giyim
    7: '07', // Ayakkabı
    8: '08', // Çanta & Valiz
  };

  generate(categoryId: number): string {
    const categoryCode = this.CATEGORY_CODES[categoryId] ?? '00';
    const randomPart   = this.randomDigits(6);
    const barcode12    = `${this.PREFIX}${categoryCode}${randomPart}`;
    const checkDigit   = this.calculateEAN13CheckDigit(barcode12);

    return `${barcode12}${checkDigit}`;
  }

  // EAN-13 check digit algoritması
  private calculateEAN13CheckDigit(barcode12: string): number {
    const digits = barcode12.split('').map(Number);
    const sum = digits.reduce((acc, digit, index) => {
      return acc + digit * (index % 2 === 0 ? 1 : 3);
    }, 0);

    return (10 - (sum % 10)) % 10;
  }

  private randomDigits(length: number): string {
    return Array.from({ length }, () =>
      Math.floor(Math.random() * 10).toString()
    ).join('');
  }
}