import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Product,
  CreateProductRequest,
  UpdateProductRequest,
  AddVariantRequest,
  UpdateVariantRequest,
  UpdateProductPriceRequest,
  ProductBarcodeResult,
  ProductDetail,
  VariantGroup,
  CreateProductResult,
  ProductCodePreview,
  GenerateVariantsRequest,
  GenerateVariantsResult,
  Season,
  Department
} from '../../shared/models/product.model';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly apiUrl = `${environment.apiUrl}/products`;

  constructor(private http: HttpClient) {}

  // ── Ürün ─────────────────────────────────────────────────
  getAll(): Observable<Product[]> {
    return this.http.get<Product[]>(this.apiUrl);
  }

  getById(id: number): Observable<ProductDetail> {
    return this.http.get<ProductDetail>(`${this.apiUrl}/${id}`);
  }

  getByBarcode(barcode: string): Observable<ProductBarcodeResult> {
    return this.http.get<ProductBarcodeResult>(
      `${this.apiUrl}/barcode/${barcode}`
    );
  }

  create(request: CreateProductRequest): Observable<CreateProductResult> {
    return this.http.post<CreateProductResult>(this.apiUrl, request);
  }

  // ── Urun kodu ────────────────────────────────────────────
  /** Secilen sezon/yil/kategori/bolum icin olusacak kod (sayac tuketmez). */
  previewCode(season: Season, seasonYear: number, categoryId: number, department: Department)
    : Observable<ProductCodePreview> {
    return this.http.get<ProductCodePreview>(`${this.apiUrl}/code-preview`, {
      params: { season, seasonYear, categoryId, department } as any
    });
  }

  /** Renk x Beden matrisi: eksik kombinasyonlari sunucu uretir (barkod dahil). */
  generateVariants(productId: number, request: GenerateVariantsRequest): Observable<GenerateVariantsResult> {
    return this.http.post<GenerateVariantsResult>(`${this.apiUrl}/${productId}/variants/generate`, request);
  }

  update(id: number, request: UpdateProductRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  // ── Varyant ──────────────────────────────────────────────
  addVariant(
    productId: number,
    request:   AddVariantRequest
  ): Observable<{ variantId: number }> {
    return this.http.post<{ variantId: number }>(
      `${this.apiUrl}/${productId}/variants`, request
    );
  }

  updateVariant(
    variantId: number,
    request:   UpdateVariantRequest
  ): Observable<void> {
    return this.http.put<void>(
      `${this.apiUrl}/variants/${variantId}`, request
    );
  }

  updateVariantPrice(
    variantId: number,
    request:   UpdateProductPriceRequest
  ): Observable<void> {
    return this.http.put<void>(
      `${this.apiUrl}/variants/${variantId}/price`, request
    );
  }

  deleteVariant(variantId: number): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/variants/${variantId}`
    );
  }
// ── Varyant Grupları ─────────────────────────────────────────
getVariantGroups(productId: number): Observable<VariantGroup[]> {
  return this.http.get<VariantGroup[]>(
    `${this.apiUrl}/${productId}/groups`);
}

createVariantGroup(
  productId: number,
  groupName: string,
  groupType = 'Color'
): Observable<{ groupId: number }> {
  return this.http.post<{ groupId: number }>(
    `${this.apiUrl}/${productId}/groups`,
    { groupName, groupType }
  );
}

assignVariantToGroup(
  variantId: number,
  groupId:   number
): Observable<void> {
  return this.http.patch<void>(
    `${this.apiUrl}/variants/${variantId}/group/${groupId}`, {});
}

addImageToGroup(
  groupId:      number,
  file:         File,
  displayOrder = 0
): Observable<{ imageId: number; imagePath: string }> {
  const formData = new FormData();
  formData.append('file', file);
  return this.http.post<{ imageId: number; imagePath: string }>(
    `${this.apiUrl}/groups/${groupId}/images?displayOrder=${displayOrder}`,
    formData
  );
}

deleteGroupImage(imageId: number): Observable<void> {
  return this.http.delete<void>(
    `${this.apiUrl}/groups/images/${imageId}`);
}
  // ── Görsel ───────────────────────────────────────────────
  uploadImage(file: File): Observable<{ imagePath: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ imagePath: string }>(
      `${this.apiUrl}/upload-image`, formData
    );
  }
}