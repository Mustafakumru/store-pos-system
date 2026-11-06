/** Backend GetSalesPerformanceQuery / SalesPerformanceDto. */
export type Pace = 'Önde' | 'Hedefte' | 'Geride' | 'Hedef tamam' | 'Başlamadı' | 'Hedef yok';

export interface StorePerformance {
  rank:               number;
  storeId:            number;
  storeName:          string;
  storeCode:          string;
  isActive:           boolean;
  netRevenue:         number;
  salesAmount:        number;
  refundAmount:       number;
  saleCount:          number;
  itemsSold:          number;
  averageBasket:      number;
  targetAmount:       number | null;
  achievementPercent: number | null;
  expectedPercent:    number | null;
  pace:               Pace;
  remainingToTarget:  number | null;
  previousNetRevenue: number;
  changePercent:      number | null;
  shareOfTotal:       number;
}

export interface PerformanceTotals {
  netRevenue:         number;
  salesAmount:        number;
  refundAmount:       number;
  saleCount:          number;
  refundCount:        number;
  itemsSold:          number;
  averageBasket:      number;
  targetAmount:       number | null;
  achievementPercent: number | null;
  previousNetRevenue: number;
  changePercent:      number | null;
}

export interface SalesPerformance {
  from:        string;
  to:          string;
  days:        number;
  elapsedDays: number;
  totals:      PerformanceTotals;
  stores:      StorePerformance[];
  daily:       { date: string; netRevenue: number; saleCount: number }[];
  topProducts: { variantId: number; productName: string; color?: string; size?: string; quantity: number; revenue: number }[];
}

export interface StoreTarget {
  storeId:      number;
  storeName:    string;
  storeCode:    string;
  year:         number;
  month:        number;
  targetAmount: number | null;
}
