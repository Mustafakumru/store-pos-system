import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AppUser, CreateUserRequest } from '../../shared/models/user.model';

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly apiUrl = `${environment.apiUrl}/auth`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<AppUser[]> {
    return this.http.get<AppUser[]>(`${this.apiUrl}/users`);
  }

  createUser(request: CreateUserRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/create-user`, request);
  }
}