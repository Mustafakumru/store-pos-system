import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Coupon, CreateCouponRequest } from '../../shared/models/coupon.model';

/** Kupon yönetimi (yalnızca Admin) - backend CouponsController. */
@Injectable({ providedIn: 'root' })
export class CouponService {
  private readonly apiUrl = `${environment.apiUrl}/coupons`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<Coupon[]> {
    return this.http.get<Coupon[]>(this.apiUrl);
  }

  create(request: CreateCouponRequest): Observable<Coupon> {
    return this.http.post<Coupon>(this.apiUrl, request);
  }

  setActive(id: number, isActive: boolean): Observable<Coupon> {
    return this.http.put<Coupon>(`${this.apiUrl}/${id}/active`, { isActive });
  }
}
