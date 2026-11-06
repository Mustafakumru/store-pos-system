import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { SalesPerformance, StoreTarget } from '../../shared/models/performance.model';

/** Tüm mağazaların satış performansı ve aylık hedefler (yalnızca Admin). */
@Injectable({ providedIn: 'root' })
export class PerformanceService {
  private readonly base = environment.apiUrl;

  constructor(private http: HttpClient) {}

  /** from/to: yerel gün, iki uç dahil. */
  get(from: Date, to: Date): Observable<SalesPerformance> {
    return this.http.get<SalesPerformance>(`${this.base}/sales-performance`, {
      params: { from: ymd(from), to: ymd(to) }
    });
  }

  getTargets(year: number, month: number): Observable<StoreTarget[]> {
    return this.http.get<StoreTarget[]>(`${this.base}/sales-targets`, { params: { year, month } });
  }

  /** targetAmount null/0: o ayın hedefi kaldırılır. */
  setTarget(storeId: number, year: number, month: number, targetAmount: number | null): Observable<StoreTarget> {
    return this.http.put<StoreTarget>(`${this.base}/sales-targets`, { storeId, year, month, targetAmount });
  }
}

export function ymd(d: Date): string {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}
