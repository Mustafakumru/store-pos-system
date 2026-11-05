export interface LoginRequest {
  email:    string;
  password: string;
}

export interface AuthResponse {
  token:    string;
  email:    string;
  fullName: string;
  roles:    string[];
  storeId:  number | null; // ✅ eklendi
}

export interface TokenPayload {
  nameid:      string;
  email:       string;
  unique_name: string;
  role:        string | string[];
  storeId?:    string;   // ✅ eklendi
  personelId?: string;   // POS personel numarası (kasiyer / müdür)
  pinIsDefault?: boolean; // POS PIN'i hâlâ varsayılan (1234) - panelde uyarı gösterilir
  exp:         number;
}