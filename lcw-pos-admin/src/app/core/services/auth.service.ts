import { Injectable, signal, computed, PLATFORM_ID, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { isPlatformBrowser } from '@angular/common';
import { tap } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { LoginRequest, AuthResponse, TokenPayload } from '../../shared/models/auth.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly TOKEN_KEY = 'lcw_pos_token';
  private readonly apiUrl    = `${environment.apiUrl}/auth`;
  private readonly isBrowser: boolean;

  private _token = signal<string | null>(null);

  // Süresi dolmuş token ile oturum açık sayılmaz (computed değil: süre
  // dolması bir sinyal değişikliği olmadığı için her çağrıda kontrol edilir).
  readonly isLoggedIn = (): boolean => {
    const token = this._token();
    if (!token) return false;
    const exp = this.decodeToken(token)?.exp;
    return !exp || exp * 1000 > Date.now();
  };
  readonly currentUser = computed(() => {
    const token = this._token();
    if (!token) return null;
    return this.decodeToken(token);
  });

  readonly isAdmin = computed(() => {
    const user = this.currentUser();
    if (!user) return false;
    const roles = Array.isArray(user.role) ? user.role : [user.role];
    return roles.includes('Admin');
  });

  // ✅ StoreManager kontrolü
  readonly isStoreManager = computed(() => {
    const user = this.currentUser();
    if (!user) return false;
    const roles = Array.isArray(user.role) ? user.role : [user.role];
    return roles.includes('StoreManager');
  });

  // ✅ StoreId — JWT claim'den okur
  readonly storeId = computed(() => {
    const user = this.currentUser();
    if (!user?.storeId) return null;
    return parseInt(user.storeId, 10);
  });

  // ✅ Kullanıcı adı
  readonly fullName = computed(() => {
    const user = this.currentUser();
    return user?.unique_name ?? user?.email ?? 'Kullanıcı';
  });

  // ✅ Rol listesi
  readonly roles = computed(() => {
    const user = this.currentUser();
    if (!user) return [];
    return Array.isArray(user.role) ? user.role : [user.role];
  });

  constructor(
    private http:     HttpClient,
    private router:   Router,
    @Inject(PLATFORM_ID) private platformId: object
  ) {
    this.isBrowser = isPlatformBrowser(this.platformId);
    if (this.isBrowser) {
      const token = localStorage.getItem(this.TOKEN_KEY);
      this._token.set(token);
    }
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, request).pipe(
      tap(response => {
        if (this.isBrowser)
          localStorage.setItem(this.TOKEN_KEY, response.token);
        this._token.set(response.token);
      })
    );
  }

  logout(): void {
    if (this.isBrowser)
      localStorage.removeItem(this.TOKEN_KEY);
    this._token.set(null);
    this.router.navigate(['/login']);
  }

  // ✅ Login sonrası role göre yönlendirme
  navigateByRole(): void {
    if (this.isAdmin())
      this.router.navigate(['/admin/dashboard']);
    else if (this.isStoreManager())
      this.router.navigate(['/store/dashboard']);
    else
      this.router.navigate(['/login']);
  }

  getToken(): string | null { return this._token(); }

  /** Sunucunun verdiği yeni token'ı oturuma yazar (örn. PIN değiştirdikten sonra). */
  setToken(token: string): void {
    if (this.isBrowser) localStorage.setItem(this.TOKEN_KEY, token);
    this._token.set(token);
  }

  /** POS PIN'ini değiştirir; başarılıysa dönen yeni token oturuma yazılır. */
  changePin(oldPin: string, newPin: string): Observable<{ message: string; token: string }> {
    const personelId = this.currentUser()?.personelId ?? '';
    return this.http
      .post<{ message: string; token: string }>(
        `${environment.apiUrl}/pos/auth/change-pin`, { personelId, oldPin, newPin })
      .pipe(tap(res => { if (res?.token) this.setToken(res.token); }));
  }

  /**
   * JWT gövdesi base64url + UTF-8'dir. Eskiden düz atob() kullanılıyordu:
   * Türkçe karakterli adlar bozuk görünüyordu ("Müdür" -> "MÃ¼dÃ¼r") ve
   * '-' / '_' içeren token'larda çözme tamamen başarısız olabiliyordu.
   */
  private static decodeJwtPart(part: string): string {
    const b64 = part.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(part.length / 4) * 4, '=');
    const binary = atob(b64);
    const bytes = Uint8Array.from(binary, c => c.charCodeAt(0));
    return new TextDecoder('utf-8').decode(bytes);
  }

private decodeToken(token: string): TokenPayload | null {
  try {
    const raw = JSON.parse(AuthService.decodeJwtPart(token.split('.')[1]));

    // ASP.NET Core ClaimTypes tam URI ile yazıyor
    // Her iki format da destekleniyor
    const roleValue =
      raw['role'] ??
      raw['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];

    const nameValue =
      raw['unique_name'] ??
      raw['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'];

    const nameIdValue =
      raw['nameid'] ??
      raw['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ??
      raw['sub'];

    return {
      nameid:      nameIdValue ?? '',
      email:       raw['email'] ?? '',
      unique_name: nameValue ?? '',
      role:        roleValue ?? '',
      storeId:     raw['storeId'] ?? null,
      personelId:  raw['personelId'] ?? undefined,
      pinIsDefault: raw['pinIsDefault'] === 'true' || raw['mustChangePin'] === 'true',
      exp:         raw['exp']
    } as TokenPayload;
  } catch {
    return null;
  }
}
}