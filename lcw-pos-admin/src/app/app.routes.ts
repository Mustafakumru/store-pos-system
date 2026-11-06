import { Routes } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from './core/services/auth.service';
import { Router } from '@angular/router';

// ── Guard'lar ────────────────────────────────────────────────
const authGuard = () => {
  const auth   = inject(AuthService);
  const router = inject(Router);
  if (auth.isLoggedIn()) return true;
  return router.createUrlTree(['/login']);
};

const adminGuard = () => {
  const auth   = inject(AuthService);
  const router = inject(Router);
  if (!auth.isLoggedIn())    return router.createUrlTree(['/login']);
  if (!auth.isAdmin())       return router.createUrlTree(['/store/dashboard']);
  return true;
};

const storeGuard = () => {
  const auth   = inject(AuthService);
  const router = inject(Router);
  if (!auth.isLoggedIn())        return router.createUrlTree(['/login']);
  if (!auth.isStoreManager())    return router.createUrlTree(['/admin/dashboard']);
  return true;
};

// ── Routes ───────────────────────────────────────────────────
export const routes: Routes = [

  // Login
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login.component')
        .then(m => m.LoginComponent)
  },

  // ── Admin Paneli (/admin/*) ──────────────────────────────
  {
    path: 'admin',
    loadComponent: () =>
      import('./layout/main-layout/main-layout.component')
        .then(m => m.MainLayoutComponent),
    canActivate: [adminGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },

      // Dashboard
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component')
            .then(m => m.DashboardComponent)
      },

      // Ürünler
      {
        path: 'products',
        loadComponent: () =>
          import('./features/products/product-list/product-list.component')
            .then(m => m.ProductListComponent)
      },
      {
        path: 'products/new',
        loadComponent: () =>
          import('./features/products/product-form/product-form.component')
            .then(m => m.ProductFormComponent)
      },
      {
        path: 'products/edit/:id',
        loadComponent: () =>
          import('./features/products/product-form/product-form.component')
            .then(m => m.ProductFormComponent)
      },

      // Satış performansı ve hedefler
      {
        path: 'sales-performance',
        loadComponent: () =>
          import('./features/reports/sales-performance.component')
            .then(m => m.SalesPerformanceComponent)
      },

      // Kategoriler
      {
        path: 'categories',
        loadComponent: () =>
          import('./features/categories/category-list.component')
            .then(m => m.CategoryListComponent)
      },

      // Depo
      {
        path: 'warehouse',
        loadComponent: () =>
          import('./features/warehouse/warehouse.component')
            .then(m => m.WarehouseComponent)
      },

      // Sevkiyatlar
      {
        path: 'stock-transfers',
        loadComponent: () =>
          import('./features/stock-transfers/stock-transfer-list.component')
            .then(m => m.StockTransferListComponent)
      },

      // Mağazalar
      {
        path: 'stores',
        loadComponent: () =>
          import('./features/stores/store-list.component')
            .then(m => m.StoreListComponent)
      },

      // Kuponlar
      {
        path: 'coupons',
        loadComponent: () =>
          import('./features/coupons/coupon-list.component')
            .then(m => m.CouponListComponent)
      },

      // Kullanıcılar
      {
        path: 'users',
        loadComponent: () =>
          import('./features/users/user-list.component')
            .then(m => m.UserListComponent)
      },

      // İşlem kaydı ve yedekler
      {
        path: 'audit-log',
        loadComponent: () =>
          import('./features/audit/audit-log.component')
            .then(m => m.AuditLogComponent)
      },
      {
        path: 'backups',
        loadComponent: () =>
          import('./features/backups/backup.component')
            .then(m => m.BackupComponent)
      },
    ]
  },

  // ── Mağaza Paneli (/store/*) ─────────────────────────────
  {
    path: 'store',
    loadComponent: () =>
      import('./layout/main-layout/main-layout.component')
        .then(m => m.MainLayoutComponent),
    canActivate: [storeGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },

      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/store-panel/store-dashboard.component')
            .then(m => m.StoreDashboardComponent)
      },
      {
        path: 'inventory',
        loadComponent: () =>
          import('./features/store-panel/store-inventory.component')
            .then(m => m.StoreInventoryComponent)
      },
      {
        path: 'transfers',
        loadComponent: () =>
          import('./features/store-panel/store-transfers.component')
            .then(m => m.StoreTransfersComponent)
      },
      {
        path: 'cashiers',
        loadComponent: () =>
          import('./features/store-panel/store-cashiers.component')
            .then(m => m.StoreCashiersComponent)
      },
      {
        path: 'sales',
        loadComponent: () =>
          import('./features/store-panel/store-sales.component')
            .then(m => m.StoreSalesComponent)
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./features/store-panel/store-reports.component')
            .then(m => m.StoreReportsComponent)
      },
      {
        path: 'cash-sessions',
        loadComponent: () =>
          import('./features/store-panel/store-cash-sessions.component')
            .then(m => m.StoreCashSessionsComponent)
      },
      {
        path: 'audit-log',
        loadComponent: () =>
          import('./features/audit/audit-log.component')
            .then(m => m.AuditLogComponent)
      },
    ]
  },

  // Kök → login'e yönlendir
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: '**', redirectTo: 'login' }
];