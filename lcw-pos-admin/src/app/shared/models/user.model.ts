/** GET /api/auth/users yanıtı (AuthController.GetUsers ile birebir). */
export interface AppUser {
  id:           string;
  firstName:    string;
  lastName:     string;
  fullName:     string;
  email:        string;
  personelId?:  string;
  storeId?:     number;
  isActive:     boolean;
  isFirstLogin: boolean;
  role?:        string;
  roles:        string[];
}

export interface CreateUserRequest {
  email:     string;
  password?: string;   // Kasiyer için opsiyonel (POS'a PIN ile girer)
  firstName: string;
  lastName:  string;
  role:      string;
  storeId?:  number;
}
