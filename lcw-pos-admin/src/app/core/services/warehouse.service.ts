import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  WarehouseStock,
  ProductForReceiving,
  ReceiveStockRequest,
  ReceiveStockResult
} from '../../shared/models/warehouse.model';

@Injectable({ providedIn: 'root' })
export class WarehouseService {
  private readonly apiUrl = `${environment.apiUrl}/warehouse`;

  constructor(private http: HttpClient) {}

  // Depodaki tüm stoklar
  getStock(): Observable<WarehouseStock[]> {
    return this.http.get<WarehouseStock[]>(`${this.apiUrl}/stock`);
  }

  // Mal girişi için katalog ürünleri
  getProductsForReceiving(): Observable<ProductForReceiving[]> {
    return this.http.get<ProductForReceiving[]>(`${this.apiUrl}/products`);
  }

  // Mal girişi yap
  receiveStock(request: ReceiveStockRequest): Observable<ReceiveStockResult> {
    return this.http.post<ReceiveStockResult>(`${this.apiUrl}/receive`, request);
  }
}