import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Category } from '../../shared/models/product.model';

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private readonly apiUrl = `${environment.apiUrl}/categories`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<Category[]> {
    return this.http.get<Category[]>(this.apiUrl);
  }

  /** Urun kodundaki 2 harfli kategori kodunu degistirir (kodlu urun varsa backend reddeder). */
  updateCode(id: number, code: string): Observable<Category> {
    return this.http.put<Category>(`${this.apiUrl}/${id}/code`, { code });
  }
}