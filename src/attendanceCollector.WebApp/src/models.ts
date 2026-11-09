export interface CurrentUser {
  subject: string | null;
  email: string | null;
  isAuthenticated: boolean;
  isBootstrapAdmin: boolean;
}

export interface AntiforgeryToken {
  requestToken: string | null;
}

export interface AttendanceDriver {
  code: string;
  name: string;
  description: string;
}

export interface AttendanceSource {
  id: string;
  code: string;
  name: string;
  driverCode: string;
  driverName: string;
  host: string;
  port: number;
  timeoutSeconds: number;
  timeZoneId: string;
  isActive: boolean;
}

export interface AttendanceSourceRequest {
  code: string;
  name: string;
  driverCode: string;
  host: string;
  port: number;
  communicationKey: number;
  timeoutSeconds: number;
  timeZoneId: string;
}

export interface AttendanceLog {
  id: string;
  sourceId: string;
  sourceCode: string;
  sourceName: string;
  employeeCode: string;
  employeeName: string;
  occurredAtUtc: string;
  status: number;
  punch: number;
  sourceRecordUid: number | null;
}

export interface CollectionRun {
  id: string;
  sourceId: string;
  sourceCode: string;
  sourceName: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  status: string;
  recordsReceived: number;
  recordsAdded: number;
  errorMessage: string | null;
}

export interface PageResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface LogFilters {
  sourceId?: string;
  employeeCode?: string;
  fromUtc?: string;
  toUtc?: string;
  page: number;
  pageSize: number;
}
