import { HttpErrorResponse } from '@angular/common/http';
import { environment } from '../../environments/environment';

/**
 * Backend'in döndüğü hata mesajını okunur hale getirir. Sırasıyla:
 *   { message }  → bizim ApiErrorResponse / ExceptionHandlingMiddleware
 *   { errors: { Alan: [...] } } veya { errors: [...] } → doğrulama hataları
 *   { title }    → ASP.NET ProblemDetails
 * Hiçbiri yoksa (ağ hatası vb.) verilen varsayılan mesajı döner.
 */
export function apiErrorMessage(err: unknown, fallback: string): string {
  if (!(err instanceof HttpErrorResponse)) return fallback;
  if (err.status === 0) return 'Sunucuya ulaşılamıyor. Bağlantınızı kontrol edin.';

  const body = err.error;
  if (typeof body === 'string' && body.trim()) return body;
  if (!body || typeof body !== 'object') return fallback;

  const parts: string[] = [];
  if (typeof body.message === 'string' && body.message) parts.push(body.message);

  if (Array.isArray(body.errors)) {
    parts.push(...body.errors.filter((e: unknown) => typeof e === 'string'));
  } else if (body.errors && typeof body.errors === 'object') {
    for (const messages of Object.values(body.errors)) {
      if (Array.isArray(messages)) parts.push(...messages.filter(m => typeof m === 'string'));
    }
  }

  if (parts.length) return parts.join(' ');
  if (typeof body.title === 'string' && body.title) return body.title;
  return fallback;
}

/**
 * Backend'deki statik dosya yolunu (örn. "/images/x.jpg") mutlak URL'e çevirir.
 * apiUrl'in SADECE SONUNDAKİ "/api" kısmı atılır - eskiden
 * `apiUrl.replace('/api', '')` ilk eşleşmeyi siliyordu; production adresi
 * "https://api.lcwaikiki.com/api" olduğunda alan adındaki "//api." bozuluyordu.
 */
export function assetUrl(path?: string | null): string {
  if (!path) return '';
  if (/^(https?:|data:|blob:)/i.test(path)) return path;
  const base = environment.apiUrl.replace(/\/api\/?$/i, '');
  return `${base}${path.startsWith('/') ? '' : '/'}${path}`;
}
