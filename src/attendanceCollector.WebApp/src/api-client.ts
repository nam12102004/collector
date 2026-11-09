import type {
  AntiforgeryToken,
  AttendanceDriver,
  AttendanceLog,
  AttendanceSource,
  AttendanceSourceRequest,
  CollectionRun,
  CurrentUser,
  LogFilters,
  PageResult,
} from './models';

export class ApiError extends Error {
  constructor(message: string, readonly status: number) {
    super(message);
  }
}

export class ApiClient {
  private requestToken: string | null = null;

  async currentUser(): Promise<CurrentUser> {
    return this.get<CurrentUser>('/auth/me');
  }

  async initializeAntiforgery(): Promise<void> {
    const token = await this.get<AntiforgeryToken>('/auth/csrf');
    this.requestToken = token.requestToken;
  }

  getDrivers(): Promise<AttendanceDriver[]> {
    return this.get('/api/v1/attendance-sources/drivers');
  }

  getSources(): Promise<AttendanceSource[]> {
    return this.get('/api/v1/attendance-sources');
  }

  createSource(source: AttendanceSourceRequest): Promise<AttendanceSource> {
    return this.send('/api/v1/attendance-sources', 'POST', source);
  }

  setSourceActive(sourceId: string, isActive: boolean): Promise<void> {
    return this.send(`/api/v1/attendance-sources/${sourceId}/active`, 'PATCH', { isActive });
  }

  collect(sourceId: string): Promise<CollectionRun> {
    return this.send(`/api/v1/attendance-sources/${sourceId}/collect`, 'POST', {});
  }

  getLogs(filters: LogFilters): Promise<PageResult<AttendanceLog>> {
    const query = new URLSearchParams({ page: String(filters.page), pageSize: String(filters.pageSize) });
    if (filters.sourceId) query.set('sourceId', filters.sourceId);
    if (filters.employeeCode) query.set('employeeCode', filters.employeeCode);
    if (filters.fromUtc) query.set('fromUtc', filters.fromUtc);
    if (filters.toUtc) query.set('toUtc', filters.toUtc);
    return this.get(`/api/v1/attendance/logs?${query}`);
  }

  getCollectionRuns(sourceId?: string): Promise<PageResult<CollectionRun>> {
    const query = new URLSearchParams({ page: '1', pageSize: '50' });
    if (sourceId) query.set('sourceId', sourceId);
    return this.get(`/api/v1/attendance/collections?${query}`);
  }

  async logout(): Promise<void> {
    if (!this.requestToken) throw new Error('Phiên đăng nhập đã hết hạn.');
    const form = document.createElement('form');
    form.method = 'post';
    form.action = '/auth/logout';
    form.hidden = true;
    const token = document.createElement('input');
    token.type = 'hidden';
    token.name = '__RequestVerificationToken';
    token.value = this.requestToken;
    form.append(token);
    document.body.append(form);
    form.submit();
  }

  private async get<T>(url: string): Promise<T> {
    return this.request<T>(url, { method: 'GET' });
  }

  private async send<T>(url: string, method: 'POST' | 'PATCH', body: unknown): Promise<T> {
    return this.request<T>(url, {
      method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
  }

  private async request<T>(url: string, init: RequestInit): Promise<T> {
    const headers = new Headers(init.headers);
    if (init.method !== 'GET' && this.requestToken) headers.set('X-CSRF-TOKEN', this.requestToken);
    const response = await fetch(url, { ...init, headers, credentials: 'same-origin' });
    if (response.status === 204) return undefined as T;
    if (!response.ok) {
      let message = `Yêu cầu thất bại (HTTP ${response.status}).`;
      try {
        const problem = await response.json() as { detail?: string; title?: string };
        message = problem.detail || problem.title || message;
      } catch {
        // Keep the HTTP status message when the response has no JSON body.
      }
      throw new ApiError(message, response.status);
    }
    return await response.json() as T;
  }
}
