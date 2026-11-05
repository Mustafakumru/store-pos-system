import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  SalesSummary, EMPTY_SALES_SUMMARY, SalesBreakdown, XReport, ZReportListItem,
  CashSessionListItem
} from '../../shared/models/report.model';

/** Yerel takvim gününün başı (00:00) ile ertesi günün başı arası. */
export function localDayRange(start: Date = new Date(), end: Date = start): { from: Date; to: Date } {
  const from = new Date(start);
  from.setHours(0, 0, 0, 0);
  const to = new Date(end);
  to.setHours(0, 0, 0, 0);
  to.setDate(to.getDate() + 1);
  return { from, to };
}

@Injectable({ providedIn: 'root' })
export class ReportService {
  private readonly posUrl = `${environment.apiUrl}/pos`;

  constructor(private http: HttpClient) {}

  /** Dönem özeti - satış geçmişi ucunun summary alanı (liste istenmez). */
  getSalesSummary(from: Date, to: Date): Observable<SalesSummary> {
    const params = new HttpParams()
      .set('from', from.toISOString())
      .set('to', to.toISOString())
      .set('pageSize', 1);
    return this.http.get<any>(`${this.posUrl}/sales`, { params }).pipe(
      map(r => ({ ...EMPTY_SALES_SUMMARY, ...(r?.summary ?? {}) }))
    );
  }

  /** Saatlik / günlük / kasiyer kırılımı (yerel saat dilimine göre gruplanır). */
  getSalesBreakdown(from: Date, to: Date): Observable<SalesBreakdown> {
    const params = new HttpParams()
      .set('from', from.toISOString())
      .set('to', to.toISOString())
      .set('tzOffsetMinutes', -new Date().getTimezoneOffset());
    return this.http.get<SalesBreakdown>(`${this.posUrl}/sales-breakdown`, { params });
  }

  /** Son Z'den bu yana açık dönem (sayaç sıfırlanmaz). */
  getXReport(): Observable<XReport> {
    return this.http.get<XReport>(`${this.posUrl}/reports/x-report`);
  }

  getCashSessions(from: Date, to: Date): Observable<CashSessionListItem[]> {
    const params = new HttpParams()
      .set('from', from.toISOString())
      .set('to', to.toISOString());
    return this.http.get<CashSessionListItem[]>(`${this.posUrl}/cash-sessions`, { params });
  }

  getZReports(from?: Date, to?: Date): Observable<ZReportListItem[]> {
    let params = new HttpParams();
    if (from) params = params.set('from', from.toISOString());
    if (to)   params = params.set('to', to.toISOString());
    return this.http.get<ZReportListItem[]>(`${this.posUrl}/z-reports`, { params });
  }
}
