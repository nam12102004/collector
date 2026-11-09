import { ApiClient, ApiError } from './api-client';
import type { AttendanceSource, AttendanceSourceRequest, CollectionRun } from './models';
import './styles.css';

const api = new ApiClient();
const byId = <T extends HTMLElement>(id: string): T => {
  const element = document.getElementById(id);
  if (!element) throw new Error(`Không tìm thấy thành phần giao diện: ${id}`);
  return element as T;
};
const input = (id: string) => byId<HTMLInputElement>(id);
const select = (id: string) => byId<HTMLSelectElement>(id);
const text = (value: unknown) => {
  const span = document.createElement('span');
  span.textContent = value == null ? '' : String(value);
  return span.innerHTML;
};
let currentPage = 1;
let totalLogs = 0;
let activeSources: AttendanceSource[] = [];
const collectingSources = new Set<string>();

function notify(message: string, kind: 'info' | 'success' | 'error' = 'info'): void {
  const notice = byId<HTMLDivElement>('message');
  notice.textContent = message;
  notice.className = `notice notice-${kind}`;
  window.clearTimeout(Number(notice.dataset.timer));
  notice.dataset.timer = String(window.setTimeout(() => notice.classList.add('hidden'), 6500));
}

function show(id: string): void {
  document.body.classList.toggle('auth-mode', id === 'signed-out' || id === 'denied');
  byId(id).classList.remove('hidden');
}

function formatUtc(value: string | null): string {
  if (!value) return '';
  const date = new Date(value.endsWith('Z') ? value : `${value}Z`);
  return `${new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short', timeStyle: 'medium', timeZone: 'UTC',
  }).format(date)} UTC`;
}

function setLoginButton(): void {
  byId<HTMLDivElement>('auth-actions').innerHTML = '<a class="button button-small" href="/auth/login?returnUrl=%2F">Đăng nhập</a>';
}

async function initialize(): Promise<void> {
  try {
    const user = await api.currentUser();
    await api.initializeAntiforgery();
    if (!user.isBootstrapAdmin) {
      show('denied');
      const authActions = byId<HTMLDivElement>('auth-actions');
      authActions.innerHTML = `<span class="user-email">${text(user.email || user.subject || 'User')}</span><button class="button button-small button-outline" id="logout" type="button">Đăng xuất</button>`;
      byId<HTMLButtonElement>('logout').addEventListener('click', () => void api.logout());
      return;
    }
    show('admin');
    const authActions = byId<HTMLDivElement>('auth-actions');
    authActions.innerHTML = `<span class="user-email">${text(user.email || user.subject || 'Admin')}</span><button class="button button-small button-outline" id="logout" type="button">Đăng xuất</button>`;
    byId<HTMLButtonElement>('logout').addEventListener('click', () => void api.logout());

    const drivers = await api.getDrivers();
    select('src-driver').innerHTML = drivers
      .map(driver => `<option value="${text(driver.code)}">${text(driver.name)} — ${text(driver.description)}</option>`)
      .join('');
    await loadSources();
    await Promise.all([loadLogs(), loadRuns()]);
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      show('signed-out');
      setLoginButton();
      return;
    }
    notify(errorMessage(error), 'error');
  }
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'Đã xảy ra lỗi không xác định.';
}

function createSourceCode(name: string): string {
  const normalized = name.normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .toUpperCase()
    .replace(/[^A-Z0-9]+/g, '-')
    .replace(/^-|-$/g, '')
    .slice(0, 32);
  return normalized || `DEVICE-${crypto.randomUUID().slice(0, 8).toUpperCase()}`;
}

async function loadSources(): Promise<void> {
  const sources = await api.getSources();
  activeSources = sources.filter(source => source.isActive);
  select('selected-source').innerHTML = activeSources
    .map(source => `<option value="${text(source.id)}">${text(source.code)} — ${text(source.name)}</option>`)
    .join('');
  const filterSource = select('filter-source');
  const oldFilter = filterSource.value;
  filterSource.innerHTML = '<option value="">Tất cả máy</option>' + sources
    .map(source => `<option value="${text(source.id)}">${text(source.code)} — ${text(source.name)}</option>`)
    .join('');
  if (sources.some(source => source.id === oldFilter)) filterSource.value = oldFilter;
  updateCollectAvailability();

  byId<HTMLTableSectionElement>('sources-body').innerHTML = sources.map(source => `
    <tr>
      <td><strong>${text(source.code)}</strong></td><td>${text(source.name)}</td>
      <td>${text(source.driverName)}</td><td>${text(source.host)}:${source.port}</td>
      <td>${text(source.timeZoneId)}</td>
      <td><span class="status-pill ${source.isActive ? 'status-active' : 'status-inactive'}">${source.isActive ? 'Hoạt động' : 'Đã tắt'}</span></td>
      <td class="source-actions">
        <button class="button button-small source-collect" data-id="${text(source.id)}" type="button"
          aria-label="Lấy dữ liệu máy ${text(source.name)}" ${!source.isActive || source.driverCode === 'mock-device-push' || collectingSources.has(source.id) ? 'disabled' : ''}>
          ${collectingSources.has(source.id) ? 'Đang lấy…' : 'Lấy dữ liệu'}
        </button>
        <button class="button button-small button-muted source-toggle" data-id="${text(source.id)}" data-active="${!source.isActive}" type="button">${source.isActive ? 'Tắt' : 'Bật'}</button>
      </td>
    </tr>`).join('') || '<tr><td colspan="7" class="empty-state">Chưa đăng ký máy nào.</td></tr>';

  document.querySelectorAll<HTMLButtonElement>('.source-collect').forEach(button => {
    button.addEventListener('click', () => void collectSource(button.dataset.id || '', button, true));
  });

  document.querySelectorAll<HTMLButtonElement>('.source-toggle').forEach(button => {
    button.addEventListener('click', async () => {
      button.disabled = true;
      try {
        await api.setSourceActive(button.dataset.id || '', button.dataset.active === 'true');
        await loadSources();
      } catch (error) {
        notify(errorMessage(error), 'error');
      } finally {
        button.disabled = false;
      }
    });
  });
}

function updateCollectAvailability(): void {
  const sourceId = select('selected-source').value;
  const source = activeSources.find(candidate => candidate.id === sourceId);
  const isPushSource = source?.driverCode === 'mock-device-push';
  byId<HTMLButtonElement>('collect').disabled = !source || isPushSource || collectingSources.has(sourceId);
  byId<HTMLElement>('collect-hint').textContent = isPushSource
    ? 'Nguồn mockDevice gửi dữ liệu từ dịch vụ chạy riêng.'
    : 'Thu thập dữ liệu trực tiếp từ máy đã chọn.';
}

async function collectSource(sourceId: string, rowButton?: HTMLButtonElement, focusSource = false): Promise<void> {
  if (!sourceId || collectingSources.has(sourceId)) return;
  const source = activeSources.find(candidate => candidate.id === sourceId);
  if (!source || source.driverCode === 'mock-device-push')
    return notify('Máy không hoạt động hoặc không thể pull dữ liệu.', 'error');

  if (focusSource) {
    select('filter-source').value = sourceId;
    currentPage = 1;
  }

  collectingSources.add(sourceId);
  if (rowButton) {
    rowButton.disabled = true;
    rowButton.textContent = 'Đang lấy…';
  }
  updateCollectAvailability();
  try {
    const result = await api.collect(sourceId);
    notify(result.status === 'Succeeded'
      ? `Đã lấy dữ liệu máy ${source.name}: nhận ${result.recordsReceived}, thêm mới ${result.recordsAdded}.`
      : `Lấy dữ liệu máy ${source.name} thất bại: ${result.errorMessage || 'xem lịch sử lấy dữ liệu.'}`,
    result.status === 'Succeeded' ? 'success' : 'error');
    await Promise.all([loadLogs(), loadRuns()]);
  } catch (error) {
    notify(errorMessage(error), 'error');
  } finally {
    collectingSources.delete(sourceId);
    if (rowButton?.isConnected) {
      rowButton.disabled = false;
      rowButton.textContent = 'Lấy dữ liệu';
    }
    updateCollectAvailability();
  }
}

async function loadLogs(): Promise<void> {
  const from = input('filter-from').value;
  const to = input('filter-to').value;
  const result = await api.getLogs({
    sourceId: select('filter-source').value || undefined,
    employeeCode: input('filter-employee').value.trim() || undefined,
    fromUtc: from ? new Date(`${from}T00:00:00Z`).toISOString() : undefined,
    toUtc: to ? new Date(`${to}T23:59:59Z`).toISOString() : undefined,
    page: currentPage,
    pageSize: 50,
  });
  totalLogs = result.totalCount;
  byId<HTMLTableSectionElement>('logs-body').innerHTML = result.items.map(log => `
    <tr><td>${text(formatUtc(log.occurredAtUtc))}</td><td>${text(log.sourceCode)} — ${text(log.sourceName)}</td>
    <td>${text(log.employeeCode)}</td><td>${text(log.employeeName)}</td><td>${log.status}</td><td>${log.punch}</td>
    <td>${log.sourceRecordUid == null ? '' : log.sourceRecordUid}</td></tr>`).join('') || '<tr><td colspan="7" class="empty-state">Không có log phù hợp.</td></tr>';
  byId('log-count').textContent = `${totalLogs} bản ghi`;
  byId('page-label').textContent = `Trang ${currentPage}`;
  byId<HTMLButtonElement>('prev-page').disabled = currentPage <= 1;
  byId<HTMLButtonElement>('next-page').disabled = currentPage * 50 >= totalLogs;
}

async function loadRuns(): Promise<void> {
  const result = await api.getCollectionRuns(select('filter-source').value || undefined);
  byId<HTMLTableSectionElement>('runs-body').innerHTML = result.items.map((run: CollectionRun) => `
    <tr><td>${text(formatUtc(run.startedAtUtc))}</td><td>${text(run.sourceCode)} — ${text(run.sourceName)}</td>
    <td><span class="status-pill ${run.status === 'Succeeded' ? 'status-active' : 'status-inactive'}">${text(run.status)}</span></td>
    <td>${run.recordsReceived}</td><td>${run.recordsAdded}</td><td class="error-detail">${text(run.errorMessage || '')}</td></tr>`)
    .join('') || '<tr><td colspan="6" class="empty-state">Chưa có lần lấy dữ liệu nào.</td></tr>';
}

byId<HTMLFormElement>('source-form').addEventListener('submit', async event => {
  event.preventDefault();
  const form = event.currentTarget as HTMLFormElement;
  const request: AttendanceSourceRequest = {
    code: createSourceCode(input('src-name').value.trim()),
    name: input('src-name').value.trim(),
    driverCode: select('src-driver').value,
    host: input('src-host').value.trim(),
    port: Number(input('src-port').value),
    communicationKey: 0,
    timeoutSeconds: 10,
    timeZoneId: 'Asia/Bangkok',
  };
  const submit = form.querySelector<HTMLButtonElement>('button[type="submit"]');
  if (submit) submit.disabled = true;
  try {
    await api.createSource(request);
    form.reset();
    input('src-host').value = select('src-driver').value === 'mock-device-http' ? 'mock-device' : 'host.docker.internal';
    input('src-port').value = select('src-driver').value === 'mock-device-http' ? '8080' : '4370';
    await loadSources();
    notify('Đã lưu máy chấm công.', 'success');
  } catch (error) {
    notify(errorMessage(error), 'error');
  } finally {
    if (submit) submit.disabled = false;
  }
});

select('src-driver').addEventListener('change', () => {
  const isMockDevice = select('src-driver').value === 'mock-device-http';
  if (['mock-device', 'host.docker.internal'].includes(input('src-host').value)) {
    input('src-host').value = isMockDevice ? 'mock-device' : 'host.docker.internal';
  }
  if (['8080', '4300', '4370'].includes(input('src-port').value)) {
    input('src-port').value = isMockDevice ? '8080' : '4370';
  }
});

byId<HTMLButtonElement>('collect').addEventListener('click', async () => {
  const sourceId = select('selected-source').value;
  if (!sourceId) return notify('Hãy đăng ký và chọn một máy đang hoạt động trước.', 'error');
  await collectSource(sourceId);
});

byId<HTMLButtonElement>('refresh-sources').addEventListener('click', () => void loadSources().catch(error => notify(errorMessage(error), 'error')));
select('selected-source').addEventListener('change', updateCollectAvailability);
byId<HTMLButtonElement>('refresh-logs').addEventListener('click', () => void loadLogs().catch(error => notify(errorMessage(error), 'error')));
byId<HTMLButtonElement>('refresh-runs').addEventListener('click', () => void loadRuns().catch(error => notify(errorMessage(error), 'error')));
byId<HTMLButtonElement>('apply-filter').addEventListener('click', () => {
  currentPage = 1;
  void Promise.all([loadLogs(), loadRuns()]).catch(error => notify(errorMessage(error), 'error'));
});
byId<HTMLButtonElement>('prev-page').addEventListener('click', () => {
  currentPage = Math.max(1, currentPage - 1);
  void loadLogs().catch(error => notify(errorMessage(error), 'error'));
});
byId<HTMLButtonElement>('next-page').addEventListener('click', () => {
  currentPage++;
  void loadLogs().catch(error => notify(errorMessage(error), 'error'));
});

void initialize();
