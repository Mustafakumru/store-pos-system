/** İşlem kaydı satırı - backend GetAuditLogsQuery / AuditLogDto. */
export interface AuditLog {
  id:           number;
  occurredAt:   string;
  category:     'İşlem' | 'Değişiklik';
  action:       string;
  actionLabel:  string;
  succeeded:    boolean;
  statusCode?:  number;
  personelId?:  string;
  userName?:    string;
  roles?:       string;
  storeId?:     number;
  storeName?:   string;
  entityType?:  string;
  entityId?:    string;
  summary?:     string;
  details?:     string;
  httpMethod?:  string;
  path?:        string;
  ipAddress?:   string;
  durationMs?:  number;
  correlationId?: string;
}

export interface AuditLogPage {
  items:      AuditLog[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

export interface AuditActionOption { key: string; label: string; }

export interface AuditLogFilter {
  from?:      Date | null;
  to?:        Date | null;
  storeId?:   number | null;
  user?:      string;
  action?:    string | null;
  entityId?:  string;
  succeeded?: boolean | null;
  page:       number;
  pageSize:   number;
}

/** Yedek geçmişi satırı - backend BackupRecord. */
export interface BackupRecord {
  id:              number;
  startedAt:       string;
  finishedAt?:     string;
  trigger:         string;
  requestedBy?:    string;
  filePath:        string;
  sizeBytes?:      number;
  succeeded:       boolean;
  verified:        boolean;
  error?:          string;
  deletedOldFiles: number;
}

export interface BackupStatus {
  enabled:        boolean;
  directory?:     string;
  dailyAt:        string;
  retentionDays:  number;
  lastSuccessAt?: string;
  nextRunAt?:     string;
  isOverdue:      boolean;
  history:        BackupRecord[];
}
