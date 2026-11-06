import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Category } from '../../shared/models/product.model';

/** Yönetim listesi satırı (GET categories/manage). */
export interface CategoryRow extends Category {
  productCount:      number;
  codedProductCount: number;
  canChangeCode:     boolean;
  canDelete:         boolean;
}

export interface CategoryRequest { name: string; description?: string | null; code?: string | null; }

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private readonly apiUrl = `${environment.apiUrl}/categories`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<Category[]> {
    return this.http.get<Category[]>(this.apiUrl);
  }

  getForManagement(): Observable<CategoryRow[]> {
    return this.http.get<CategoryRow[]>(`${this.apiUrl}/manage`);
  }

  suggestCode(name: string): Observable<{ code: string }> {
    return this.http.get<{ code: string }>(`${this.apiUrl}/suggest-code`, { params: { name } });
  }

  create(request: CategoryRequest): Observable<Category> {
    return this.http.post<Category>(this.apiUrl, request);
  }

  update(id: number, request: CategoryRequest): Observable<Category> {
    return this.http.put<Category>(`${this.apiUrl}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  /** Urun kodundaki 2 harfli kategori kodunu degistirir (kodlu urun varsa backend reddeder). */
  updateCode(id: number, code: string): Observable<Category> {
    return this.http.put<Category>(`${this.apiUrl}/${id}/code`, { code });
  }
}