import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      switch (error.status) {
        case 401:
          // Token geçersiz veya süresi dolmuş → oturumu kapat.
          // Login isteğinin kendi 401'i (hatalı şifre) oturum kapatmaz;
          // hata login ekranında gösterilir.
          if (!req.url.endsWith('/auth/login')) authService.logout();
          break;
        case 403:
          // Yetkisiz işlem: sayfadan atmak yerine hatayı çağıran ekrana bırak
          // (ekran backend'in mesajını toast ile gösterir). Eskiden tanımsız
          // '/unauthorized' rotasına gidip kullanıcıyı login sayfasına düşürüyordu.
          break;
        default:
          console.error('API Hatası:', error.status, error.message);
      }
      return throwError(() => error);
    })
  );
};
