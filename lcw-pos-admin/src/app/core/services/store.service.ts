import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import {  HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import {
  Store,
  CreateStoreRequest,
  UpdateStoreRequest,
  StockAlert,
  StoreInventoryResult,
  stockAlertLevelName
} from '../../shared/models/store.model';

@Injectable({ providedIn: 'root' })
export class StoreService {
  private readonly apiUrl = `${environment.apiUrl}/stores`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<Store[]> {
    return this.http.get<Store[]>(this.apiUrl);
  }

  // ✅ Tüm mağazalardaki stok uyarıları
  getStockAlerts(): Observable<StockAlert[]> {
    return this.http.get<StockAlert[]>(`${this.apiUrl}/stock-alerts`);
  }
  getInventory(storeId: number, onlyAlerts?: boolean): Observable<StoreInventoryResult> {
    let params = new HttpParams();
    if (onlyAlerts !== undefined)
      params = params.set('onlyAlerts', onlyAlerts);
    // Backend alertLevel'ı SAYI olarak döner (0-3); ekranlar isimle
    // ('Critical', 'Low'...) filtrelediği için burada isme çevrilir.
    // Eskiden filtreler hiç eşleşmiyor, tükenen ürünler "Normal" görünüyordu.
    return this.http.get<any>(`${this.apiUrl}/${storeId}/inventory`, { params }).pipe(
      map(result => ({
        ...result,
        items: (result.items ?? []).map((i: any) => ({
          ...i,
          alertLevel: stockAlertLevelName(i.alertLevel)
        }))
      }) as StoreInventoryResult)
    );
  }
  create(request: CreateStoreRequest): Observable<{ id: number; name: string; code: string }> {
    return this.http.post<{ id: number; name: string; code: string }>(
      this.apiUrl, request);
  }

  update(id: number, request: UpdateStoreRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, request);
  }

  toggleStatus(id: number): Observable<{ isActive: boolean; message: string }> {
    return this.http.patch<{ isActive: boolean; message: string }>(
      `${this.apiUrl}/${id}/toggle-status`, {});
  }
  getById(id: number): Observable<Store> {
    return this.http.get<Store>(`${this.apiUrl}/${id}`);
  }

  /** Varyantların düşük stok eşiğini günceller (tek veya toplu). */
  updateMinStock(storeId: number, items: { variantId: number; minStockAlert: number }[])
    : Observable<{ updated: number; message: string }> {
    return this.http.put<{ updated: number; message: string }>(
      `${this.apiUrl}/${storeId}/inventory/min-stock`, items);
  }
}