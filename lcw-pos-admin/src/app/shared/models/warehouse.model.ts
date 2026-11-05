export interface WarehouseStock {
  variantId:         number;   // ✅ productId → variantId
  productId:         number;
  productName:       string;
  barcode:           string;
  color?:            string;   // ✅ Yeni
  size?:             string;   // ✅ Yeni
  quantity:          number;
  reservedQuantity:  number;
  availableQuantity: number;
}

export interface ProductForReceiving {
  variantId:             number;  // ✅ productId → variantId
  productId:             number;
  productName:           string;
  barcode:               string;
  color?:                string;  // ✅ Yeni
  size?:                 string;  // ✅ Yeni
  imagePath?:            string;
  currentWarehouseStock: number;
  reservedQuantity:      number;
  availableQuantity:     number;
}

export interface ReceiveStockRequest {
  items: ReceiveStockItem[];
  notes?: string;
}

export interface ReceiveStockItem {
  variantId: number;   // ✅ productId → variantId
  quantity:  number;
}

export interface ReceiveStockResult {
  totalProducts: number;
  totalQuantity: number;
  message:       string;
}