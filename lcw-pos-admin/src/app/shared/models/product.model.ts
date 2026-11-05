// ── Ana Ürün ─────────────────────────────────────────────────
export interface Product {
  id:           number;
  name:         string;
  description?: string;
  imagePath?:   string;
  categoryId:   number;
  categoryName: string;
  isActive:     boolean;
  variantCount: number;
  variants:     ProductVariant[];
  // Urun (model) kodu: W6ER01Z8 = [Sezon W/S][Yil][Kategori][Model no][Z1/Z4/Z8]
  code?:            string;
  season?:          Season;
  seasonLabel?:     string;
  seasonYear?:      number;
  department?:      Department;
  departmentLabel?: string;
  ageGroup?:        number;
  ageGroupLabel?:   string;
}

/** GET /products/{id} - düzenleme ekranı için ürünün tamamı (ProductDetailDto). */
export interface ProductDetail extends Product {}

// ── Varyant ──────────────────────────────────────────────────
export interface ProductVariant {
  id:           number;
  barcode:      string;
  color?:       string;
  size?:        string;
  imagePath?:   string;
  currentPrice: number;
  isActive:     boolean;
}

// ── Oluşturma ────────────────────────────────────────────────
export interface CreateProductRequest {
  name:        string;
  description?: string;
  categoryId:  number;
  season:      Season;
  seasonYear:  number;
  department:  Department;
  imagePath?:  string;
  variants:    CreateVariantRequest[];
}

export interface CreateProductResult {
  id:       number;
  code:     string;
  variants: { id: number; barcode: string; color?: string; size?: string }[];
}

export interface CreateVariantRequest {
  barcode?:     string;     // bos = sunucu EAN-13 uretir
  color?:       string;
  size?:        string;
  imagePath?:   string;
  initialPrice: number;
}

// ── Güncelleme ───────────────────────────────────────────────
export interface UpdateProductRequest {
  id:          number;
  name:        string;
  description?: string;
  categoryId:  number;
  imagePath?:  string;
  isActive:    boolean;
}

export interface AddVariantRequest {
  productId:    number;
  barcode?:     string;     // bos = sunucu uretir
  color?:       string;
  size?:        string;
  imagePath?:   string;
  initialPrice: number;
}

export interface UpdateVariantRequest {
  id:        number;
  color?:    string;
  size?:     string;
  imagePath?: string;
  isActive:  boolean;
}

export interface UpdateProductPriceRequest {
  variantId: number;   // ✅ productId → variantId
  newPrice:  number;
}
// ── Varyant Grupları ─────────────────────────────────────────
export interface VariantGroup {
  id:        number;
  groupName: string;
  groupType: string;
  images:    GroupImage[];
  variants:  GroupVariant[];
}

export interface GroupImage {
  id:           number;
  imagePath:    string;
  displayOrder: number;
}

export interface GroupVariant {
  variantId: number;
  barcode:   string;
  size?:     string;
  isActive:  boolean;
}
// ── Barkod Sorgu Sonucu ──────────────────────────────────────
export interface ProductBarcodeResult {
  id:           number;
  variantId:    number;   // ✅ Yeni
  barcode:      string;
  name:         string;
  color?:       string;   // ✅ Yeni
  size?:        string;   // ✅ Yeni
  imagePath?:   string;
  categoryName: string;
  categoryId:   number;
  currentPrice: number;
  isActive:     boolean;
}

// ── Kategori ─────────────────────────────────────────────────
export interface Category {
  id:           number;
  name:         string;
  code?:        string;     // urun kodundaki 2 harf (W6[ER]01Z8)
  description?: string;
}

// ── Urun kodu sistemi ────────────────────────────────────────
/** Backend Domain.Enums.Season */
export enum Season { Winter = 1, Summer = 2 }
/** Backend Domain.Enums.Department (yas kitlesini belirler) */
export enum Department { Women = 1, Men = 2, Girl = 3, Boy = 4, Baby = 5 }

export const SEASON_OPTIONS = [
  { value: Season.Winter, label: 'Kış (W)', letter: 'W' },
  { value: Season.Summer, label: 'Yaz (S)', letter: 'S' },
];

export const DEPARTMENT_OPTIONS = [
  { value: Department.Women, label: 'Kadın',       z: 'Z8', hint: 'Yetişkin' },
  { value: Department.Men,   label: 'Erkek',       z: 'Z8', hint: 'Yetişkin' },
  { value: Department.Girl,  label: 'Kız Çocuk',   z: 'Z4', hint: '10-14 yaş' },
  { value: Department.Boy,   label: 'Erkek Çocuk', z: 'Z4', hint: '10-14 yaş' },
  { value: Department.Baby,  label: 'Bebek',       z: 'Z1', hint: 'Bebek' },
];

export interface ProductCodePreview {
  code:            string;
  categoryCode:    string;
  seasonLabel:     string;
  departmentLabel: string;
  ageGroupLabel:   string;
  explanation:     string;
}

export interface GenerateVariantsRequest {
  productId:    number;
  colors:       string[];
  sizes:        string[];
  price:        number;
  colorPrices?: Record<string, number>;
}

export interface GenerateVariantsResult {
  created:  number;
  skipped:  number;
  variants: { id: number; barcode: string; color?: string; size?: string; price: number }[];
}