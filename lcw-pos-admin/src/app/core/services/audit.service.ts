import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AuditActionOption, AuditLogFilter, AuditLogPage, BackupRecord, BackupStatus
} from '../../shared/models/audit.model';

/** İşlem kaydı (Admin + mağaza müdürü) ve yedekleme (yalnızca Admin). */
@Injectable({ providedIn: 'root' })
export class AuditService {
  private readonly auditUrl  = `${environment.apiUrl}/audit-logs`;
  private readonly backupUrl = `${environment.apiUrl}/backups`;

  constructor(private http: HttpClient) {}

  getLogs(f: AuditLogFilter): Observable<AuditLogPage> {
    let params = new HttpParams().set('page', f.page).set('pageSize', f.pageSize);
    if (f.from)       params = params.set('from', startOfDay(f.from).toISOString());
    if (f.to)         params = params.set('to', nextDay(f.to).toISOString());
    if (f.storeId)    params = params.set('storeId', f.storeId);
    if (f.user?.trim())     params = params.set('user', f.user.trim());
    if (f.action)     params = params.set('action', f.action);
    if (f.entityId?.trim()) params = params.set('entityId', f.entityId.trim());
    if (f.succeeded !== null && f.succeeded !== undefined) params = params.set('succeeded', f.succeeded);
    return this.http.get<AuditLogPage>(this.auditUrl, { params });
  }

  getActions(): Observable<AuditActionOption[]> {
    return this.http.get<AuditActionOption[]>(`${this.auditUrl}/actions`);
  }

  getBackupStatus(): Observable<BackupStatus> {
    return this.http.get<BackupStatus>(this.backupUrl);
  }

  runBackup(): Observable<BackupRecord> {
    return this.http.post<BackupRecord>(`${this.backupUrl}/run`, {});
  }
}

function startOfDay(d: Date): Date {
  const x = new Date(d); x.setHours(0, 0, 0, 0); return x;
}

/** "Bitiş" günü dahil: ertesi günün başına kadar. */
function nextDay(d: Date): Date {
  const x = startOfDay(d); x.setDate(x.getDate() + 1); return x;
}
