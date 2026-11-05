export interface SalesSummary {
  totalSales:    number;
  totalItems:    number;
  totalRevenue:  number;
  totalDiscount: number;
  refundCount:   number;
  refundTotal:   number;
  netRevenue:    number;
  cashTotal:     number;
  cardTotal:     number;
  giftCardTotal: number;
  cashRefundTotal?:     number;   // kasadan verilen nakit iade
  cardRefundTotal?:     number;   // karta iade
  exchangeCreditTotal?: number;   // değişimde kredi olarak kullanılan iade
  giftCardSoldTotal?:   number;   // hediye kartı satışı (EMANET - ciroya dahil değil)
  giftCardSoldCash?:    number;
  giftCardSoldCard?:    number;
}

export const EMPTY_SALES_SUMMARY: SalesSummary = {
  totalSales: 0, totalItems: 0, totalRevenue: 0, totalDiscount: 0,
  refundCount: 0, refundTotal: 0, netRevenue: 0,
  cashTotal: 0, cardTotal: 0, giftCardTotal: 0
};

export interface HourlyBucket  { hour: number;  saleCount: number; revenue: number; }
export interface DailyBucket   { date: string;  saleCount: number; revenue: number; } // "yyyy-MM-dd" (yerel gün)

export interface CashierBreakdown {
  cashierId:     string;
  cashierName?:  string | null;
  saleCount:     number;
  itemCount:     number;
  revenue:       number;
  averageBasket: number;
}

export interface SalesBreakdown {
  hourly:   HourlyBucket[];
  daily:    DailyBucket[];
  cashiers: CashierBreakdown[];
}

export interface XReport {
  fromDate:           string;
  toDate:             string;
  totalSalesAmount:   number;
  totalRefundsAmount: number;
  netAmount:          number;
  cashTotal:          number;
  cardTotal:          number;
  giftCardTotal:      number;
  transactionCount:   number;
  refundCount:        number;
  lastZReportNumber?: number | null;
  cashRefundTotal?:   number;
  cardRefundTotal?:   number;
  netCash?:           number;   // kasaya net nakit: nakit satış − nakit iade + h.kart nakit
  giftCardSoldTotal?: number;   // hediye kartı satışı (EMANET)
  giftCardSoldCash?:  number;
  giftCardSoldCard?:  number;
}

export interface CashSessionListItem {
  id:                     number;
  cashierId:              string;
  cashierName?:           string | null;
  status:                 'Open' | 'Closed';
  openedAt:               string;
  closedAt?:              string | null;
  openingFloat:           number;
  cashSalesTotal:         number;
  expectedCashAmount:     number;
  closingAmount?:         number | null;
  difference?:            number | null;   // teslim − beklenen (eksi = açık)
  transactionCount:       number;
  openingApprovedByName?: string | null;
  closingApprovedByName?: string | null;
  cashRefundTotal?:       number;   // oturumda verilen nakit iade (beklenenden düşüldü)
  giftCardSoldCash?:      number;   // oturumda hediye kartı satışı nakdi (beklenene eklendi)
}

export interface ZReportListItem {
  id:                 number;
  reportNumber:       number;
  fromDate:           string;
  toDate:             string;
  totalSalesAmount:   number;
  totalRefundsAmount: number;
  netAmount:          number;
  cashTotal:          number;
  cardTotal:          number;
  giftCardTotal:      number;
  transactionCount:   number;
  refundCount:        number;
  cashierId:          string;
  cashierName?:       string | null;
  cashRefundTotal?:   number;
  cardRefundTotal?:   number;
}
