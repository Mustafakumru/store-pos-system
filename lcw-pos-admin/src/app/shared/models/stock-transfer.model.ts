export type TransferStatus = 'Draft' | 'Shipped' | 'Received' | 'Cancelled';

export interface StockTransfer {
  id:             number;
  transferNumber: string;
  storeName:      string;
  status:         TransferStatus;
  totalProducts:  number;
  totalQuantity:  number;
  createdAt:      string;
  shippedAt?:     string;
  receivedAt?:    string;
  notes?:         string;
  items:          StockTransferItem[];
}

export interface StockTransferItem {
  variantId:         number;
  productName:       string;
  barcode:           string;
  requestedQuantity: number;
  shippedQuantity:   number;
  receivedQuantity:  number;
   color?:            string;   // ✅ yeni
  size?:             string;   // ✅ yeni
}

export interface CreateTransferRequest {
  toStoreId: number;
  items:     CreateTransferItem[];
  notes?:    string;
}

export interface CreateTransferItem {
  variantId:         number;
  requestedQuantity: number;
}

export interface ShipTransferRequest {
  id:    number;
  items: ShipTransferItem[];
}

export interface ReceiveTransferRequest {
  items?: { variantId: number; receivedQuantity: number }[];
  note?:  string;
}

export interface ShipTransferItem {
  variantId:       number;
  shippedQuantity: number;
}