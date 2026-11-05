import { ApplicationConfig, LOCALE_ID } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors, withFetch } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { registerLocaleData } from '@angular/common';
import localeTr from '@angular/common/locales/tr';
import { routes } from './app.routes';
import { authInterceptor } from './core/services/interceptors/auth.interceptor';
import { errorInterceptor } from './core/services/interceptors/error.interceptor';

// ✅ Türkçe locale'i kaydet
registerLocaleData(localeTr);

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(
      withFetch(),
      withInterceptors([authInterceptor, errorInterceptor])
    ),
    provideAnimations(),
    { provide: LOCALE_ID, useValue: 'tr-TR' } // ✅ eklendi
  ]
};