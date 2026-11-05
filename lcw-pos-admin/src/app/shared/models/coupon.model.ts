/** Backend Domain.Enums.DiscountType - JSON'da SAYI olarak gelir. */
export enum CouponDiscountType {
  Amount     = 0,   // sabit tutar (TL)
  Percentage = 1    // yüzde
}

/** GET/POST /api/coupons yanıtı (CouponDto ile birebir). */
export interface Coupon {
  id:                 number;
  code:               string;
  description?:       string | null;
  discountType:       CouponDiscountType;
  value:              number;
  minPurchaseAmount?: number | null;
  maxDiscountAmount?: number | null;
  startDate:          string;   // UTC ISO
  endDate?:           string | null;
  usageLimit?:        number | null;
  usedCount:          number;
  isActive:           boolean;
}

/** POST /api/coupons gövdesi (CreateCouponCommand). */
export interface CreateCouponRequest {
  code:               string;
  description?:       string | null;
  discountType:       CouponDiscountType;
  value:              number;
  minPurchaseAmount?: number | null;
  maxDiscountAmount?: number | null;
  startDate?:         string | null;
  endDate?:           string | null;
  usageLimit?:        number | null;
}

/** Ekranda gösterilen, tarih/limit/aktiflikten türetilen durum. */
export type CouponStatus = 'active' | 'inactive' | 'expired' | 'scheduled' | 'exhausted';

export function couponStatus(c: Coupon, now = new Date()): CouponStatus {
  if (!c.isActive) return 'inactive';
  if (new Date(c.startDate) > now) return 'scheduled';
  if (c.endDate && new Date(c.endDate) < now) return 'expired';
  if (c.usageLimit != null && c.usedCount >= c.usageLimit) return 'exhausted';
  return 'active';
}
