import { Component, computed } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../core/services/auth.service';

interface NavItem {
  label:   string;
  icon:    string;
  route:   string;
  exact?:  boolean;
  badge?:  string;
}

interface NavSection {
  title: string;
  items: NavItem[];
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, CommonModule],
  template: `
    <aside class="w-64 bg-gray-900 text-white flex flex-col h-screen
                  shadow-xl flex-shrink-0">

      <!-- Logo -->
      <div class="px-5 py-5 border-b border-gray-800">
        <div class="flex items-center gap-3">
          <div class="w-9 h-9 bg-red-500 rounded-xl flex items-center
                      justify-center shadow-lg shadow-red-500/30">
            <i class="pi pi-shopping-cart text-white text-sm"></i>
          </div>
          <div>
            <p class="text-white font-bold text-sm leading-tight">LC Waikiki</p>
            <p class="text-gray-500 text-xs mt-0.5">
              {{ isAdmin() ? 'Admin Paneli' : 'Mağaza Paneli' }}
            </p>
          </div>
        </div>
      </div>

      <!-- Nav -->
      <nav class="flex-1 px-3 py-4 overflow-y-auto space-y-5">
        @for (section of navSections(); track section.title) {
          <div>
            <p class="text-gray-600 text-xs font-semibold uppercase
                       tracking-widest px-3 mb-2">
              {{ section.title }}
            </p>
            <ul class="space-y-0.5">
              @for (item of section.items; track item.route) {
                <li>
                  <a [routerLink]="item.route"
                     routerLinkActive="bg-red-600/90 text-white shadow-sm"
                     [routerLinkActiveOptions]="{ exact: item.exact ?? false }"
                     class="flex items-center gap-3 px-3 py-2.5 rounded-xl
                            text-gray-400 hover:bg-gray-800 hover:text-white
                            transition-all duration-150 text-sm font-medium group">
                    <i [class]="'pi ' + item.icon + ' text-base w-4 text-center'"></i>
                    <span class="flex-1">{{ item.label }}</span>
                    @if (item.badge) {
                      <span class="text-xs bg-red-500 text-white
                                   px-1.5 py-0.5 rounded-full">
                        {{ item.badge }}
                      </span>
                    }
                  </a>
                </li>
              }
            </ul>
          </div>
        }
      </nav>

      <!-- Kullanıcı Bilgisi -->
      <div class="px-4 py-4 border-t border-gray-800">
        <div class="flex items-center gap-3 px-2 py-2 rounded-xl
                    hover:bg-gray-800 transition-colors">
          <div class="w-8 h-8 bg-gradient-to-br from-red-500 to-red-700
                      rounded-lg flex items-center justify-center flex-shrink-0">
            <span class="text-white text-xs font-bold">
              {{ initials() }}
            </span>
          </div>
          <div class="flex-1 min-w-0">
            <p class="text-white text-xs font-semibold truncate">
              {{ fullName() }}
            </p>
            <p class="text-gray-500 text-xs truncate">
              {{ roleLabel() }}
            </p>
          </div>
        </div>
        <p class="text-gray-700 text-xs text-center mt-3">
          Faz 2 — v2.0.0
        </p>
      </div>

    </aside>
  `
})
export class SidebarComponent {

  constructor(private authService: AuthService) {}

  readonly isAdmin        = this.authService.isAdmin;
  readonly isStoreManager = this.authService.isStoreManager;
  readonly fullName       = this.authService.fullName;

  readonly initials = computed(() => {
    const name = this.fullName();
    return name.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
  });

  readonly roleLabel = computed(() => {
    if (this.isAdmin()) return 'Sistem Yöneticisi';
    if (this.isStoreManager()) return 'Mağaza Müdürü';
    return 'Kullanıcı';
  });

  // Role göre dinamik menü
  readonly navSections = computed((): NavSection[] => {
    if (this.isAdmin()) return this.adminSections;
    if (this.isStoreManager()) return this.storeSections;
    return [];
  });

  private adminSections: NavSection[] = [
    {
      title: 'Genel',
      items: [
        { label: 'Dashboard',     icon: 'pi-home',       route: '/admin/dashboard', exact: true },
      ]
    },
    {
      title: 'Katalog',
      items: [
        { label: 'Ürünler',       icon: 'pi-box',        route: '/admin/products' },
        { label: 'Yeni Ürün',     icon: 'pi-plus-circle', route: '/admin/products/new' },
        { label: 'Kategoriler',   icon: 'pi-tags',       route: '/admin/categories' },
      ]
    },
    {
      title: 'Depo & Lojistik',
      items: [
        { label: 'Depo Stokları', icon: 'pi-warehouse',  route: '/admin/warehouse' },
        { label: 'Sevkiyatlar',   icon: 'pi-truck',      route: '/admin/stock-transfers' },
      ]
    },
    {
      title: 'Mağaza',
      items: [
        { label: 'Mağazalar',     icon: 'pi-shop',       route: '/admin/stores' },
        { label: 'Kullanıcılar',  icon: 'pi-users',      route: '/admin/users' },
      ]
    },
    {
      title: 'Satış & Kampanya',
      items: [
        { label: 'Kuponlar',      icon: 'pi-ticket',     route: '/admin/coupons' },
      ]
    },
    {
      title: 'Güvenlik & Sistem',
      items: [
        { label: 'İşlem Kaydı',   icon: 'pi-history',    route: '/admin/audit-log' },
        { label: 'Yedekler',      icon: 'pi-database',   route: '/admin/backups' },
      ]
    }
  ];

 private storeSections: NavSection[] = [
  {
    title: 'Mağazam',
    items: [
      { label: 'Dashboard',    icon: 'pi-home',      route: '/store/dashboard', exact: true },
      { label: 'Stok Durumu',  icon: 'pi-chart-bar', route: '/store/inventory' },
      { label: 'Satışlar',     icon: 'pi-receipt',   route: '/store/sales'     },
      { label: 'X / Z Raporları', icon: 'pi-file',   route: '/store/reports'   },
      { label: 'Kasa Oturumları', icon: 'pi-wallet', route: '/store/cash-sessions' },
      { label: 'Sevkiyatlar',  icon: 'pi-truck',     route: '/store/transfers' },
    ]
  },
  {
    title: 'Yönetim',
    items: [
      { label: 'Kasiyerler',   icon: 'pi-users',     route: '/store/cashiers'  },
      { label: 'İşlem Kaydı',  icon: 'pi-history',   route: '/store/audit-log' },
    ]
  }
];
}