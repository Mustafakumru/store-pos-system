import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  StockTransfer,
  CreateTransferRequest,
  ShipTransferRequest,
  ReceiveTransferRequest
} from '../../shared/models/stock-transfer.model';

@Injectable({ providedIn: 'root' })
export class StockTransferService {
  private readonly apiUrl = `${environment.apiUrl}/stocktransfers`;

  constructor(private http: HttpClient) {}

  getAll(storeId?: number, status?: string): Observable<StockTransfer[]> {
    let params = new HttpParams();
    if (storeId) params = params.set('storeId', storeId);
    if (status)  params = params.set('status', status);
    return this.http.get<StockTransfer[]>(this.apiUrl, { params });
  }

  create(request: CreateTransferRequest): Observable<{ id: number; transferNumber: string; storeName: string }> {
    return this.http.post<{ id: number; transferNumber: string; storeName: string }>(
      this.apiUrl, request);
  }

  ship(id: number, request: ShipTransferRequest): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/${id}/ship`, request);
  }

  /** items verilmezse gönderilenin tamamı teslim alınmış sayılır. */
  receive(id: number, request: ReceiveTransferRequest = {}): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/${id}/receive`, request);
  }

  cancel(id: number, reason?: string): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/${id}/cancel`, { reason });
  }
}