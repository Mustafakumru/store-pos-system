import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../core/services/auth.service';
import { apiErrorMessage } from '../../../core/utils/api.util';

// PrimeNG
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    InputTextModule,
    PasswordModule,
    ButtonModule,
    MessageModule
  ],
  template: `
    <div class="min-h-screen bg-gray-100 flex items-center justify-center">
      <div class="bg-white rounded-2xl shadow-lg p-10 w-full max-w-md">

        <!-- Logo -->
        <div class="text-center mb-8">
          <div class="w-16 h-16 bg-red-500 rounded-2xl flex items-center
                      justify-center mx-auto mb-4">
            <i class="pi pi-shopping-cart text-white text-2xl"></i>
          </div>
          <h2 class="text-2xl font-bold text-gray-800">LC Waikiki POS</h2>
          <p class="text-gray-500 text-sm mt-1">Yönetim Paneline Giriş</p>
        </div>

        <!-- Hata mesajı -->
        @if (errorMessage) {
          <div class="mb-4 p-3 bg-red-50 border border-red-200
                      rounded-lg text-red-600 text-sm">
            <i class="pi pi-exclamation-triangle mr-2"></i>
            {{ errorMessage }}
          </div>
        }

        <!-- Form -->
        <form [formGroup]="loginForm" (ngSubmit)="onSubmit()">

          <!-- Email -->
          <div class="mb-4">
            <label class="block text-sm font-medium text-gray-700 mb-1">
              E-Posta
            </label>
            <input
              pInputText
              type="email"
              formControlName="email"
              placeholder="admin@lcwaikiki.com"
              class="w-full" />
            @if (loginForm.get('email')?.invalid &&
                 loginForm.get('email')?.touched) {
              <p class="text-red-500 text-xs mt-1">
                Geçerli bir e-posta giriniz.
              </p>
            }
          </div>

          <!-- Şifre -->
          <div class="mb-6">
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Şifre
            </label>
            <p-password
              formControlName="password"
              [feedback]="false"
              [toggleMask]="true"
              styleClass="w-full"
              inputStyleClass="w-full"
              placeholder="••••••••" />
            @if (loginForm.get('password')?.invalid &&
                 loginForm.get('password')?.touched) {
              <p class="text-red-500 text-xs mt-1">
                Şifre en az 8 karakter olmalıdır.
              </p>
            }
          </div>

          <!-- Giriş butonu -->
          <p-button
            type="submit"
            label="Giriş Yap"
            icon="pi pi-sign-in"
            styleClass="w-full"
            [loading]="isLoading"
            [disabled]="loginForm.invalid || isLoading" />

        </form>
      </div>
    </div>
  `
})
export class LoginComponent {
  loginForm: FormGroup;
  isLoading  = false;
  errorMessage = '';

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.loginForm = this.fb.group({
      email:    ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]]
    });
  }

 onSubmit(): void {
  if (this.loginForm.invalid) return;

  this.isLoading    = true;
  this.errorMessage = '';

  this.authService.login(this.loginForm.value).subscribe({
    next: () => {
      this.isLoading = false;
      // ✅ Role göre yönlendir
      this.authService.navigateByRole();
    },
    error: (err) => {
      this.isLoading    = false;
      // 401: backend'in mesajı ("Geçersiz email veya şifre." / "Bu kullanıcı
      // pasif durumda.") - diğer hatalarda bağlantı/sunucu sorunu göster.
      this.errorMessage = err?.status === 401
        ? apiErrorMessage(err, 'E-posta veya şifre hatalı.')
        : apiErrorMessage(err, 'Giriş yapılamadı. Lütfen tekrar deneyin.');
    }
  });
}
}