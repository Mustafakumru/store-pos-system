export interface Store {
  id:                 number;
  name:               string;
  code:               string;
  address?:           string;
  city?:              string;
  phone?:             string;
  isActive:           boolean;
  criticalStockCount: number;
  lowStockCount:      number;
}

export interface CreateStoreRequest {
  name:     string;
  code:     string;
  address?: string;
  city?:    string;
  phone?:   string;
}

export interface UpdateStoreRequest {
  id:       number;
  name:     string;
  address?: string;
  city?:    string;
  phone?:   string;
}

/** Backend Domain.Enums.StockAlertLevel - JSON'da SAYI olarak gelir. */
export enum StockAlertLevel {
  Normal     = 0,
  Low        = 1,
  OutOfStock = 2,
  Critical   = 3   // negatif stok
}

/** Ekranların filtre/rozet için kullandığı isim karşılığı. */
export type StockAlertLevelName = 'Normal' | 'Low' | 'OutOfStock' | 'Critical';

export function stockAlertLevelName(level: number | string): StockAlertLevelName {
  if (typeof level === 'string') return level as StockAlertLevelName;
  return (StockAlertLevel[level] as StockAlertLevelName) ?? 'Normal';
}

export interface StockAlert {
  storeId:        number;
  storeName:      string;
  city:           string;
  variantId:      number;
  productId:      number;
  productName:    string;
  barcode:        string;
  color?:         string;
  size?:          string;
  quantity:       number;
  alertLevel:     StockAlertLevel;
  alertLevelText: string;
}

export interface StoreInventoryResult {
  storeId:         number;
  storeName:       string;
  totalProducts:   number;
  criticalCount:   number;
  outOfStockCount: number;
  lowStockCount:   number;
  items:           StoreInventoryItem[];
}

export interface StoreInventoryItem {
  variantId:      number;
  productId:      number;
  productName:    string;
  barcode:        string;
  color?:         string;
  size?:          string;
  imagePath?:     string;
  quantity:       number;
  minStockAlert:  number;
  /** StoreService.getInventory sayısal değeri isme çevirir. */
  alertLevel:     StockAlertLevelName;
  alertLevelText: string;
  currentPrice:   number;
}
