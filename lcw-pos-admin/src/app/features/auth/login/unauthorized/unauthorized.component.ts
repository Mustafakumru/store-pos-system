import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-gray-100">
      <div class="text-center">
        <div class="text-red-500 text-8xl mb-4">
          <i class="pi pi-lock"></i>
        </div>
        <h1 class="text-3xl font-bold text-gray-800 mb-2">Yetkisiz Erişim</h1>
        <p class="text-gray-500 mb-6">
          Bu sayfayı görüntüleme yetkiniz bulunmuyor.
        </p>
        <a
          routerLink="/products"
          class="bg-red-500 text-white px-6 py-2.5 rounded-lg
                 hover:bg-red-600 transition-colors font-medium">
          Ana Sayfaya Dön
        </a>
      </div>
    </div>
  `
})
export class UnauthorizedComponent {}