import { getToken } from './auth';

const BASE_URL = '/api';

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getToken();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string>),
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const response = await fetch(`${BASE_URL}${path}`, { ...options, headers });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ error: response.statusText }));
    throw Object.assign(new Error(error.error ?? 'API error'), { status: response.status });
  }
  if (response.status === 204) return undefined as T;
  return response.json();
}

// Multipart upload — no Content-Type (browser sets boundary)
async function upload<T>(path: string, file: File, fieldName = 'file'): Promise<T> {
  const token = getToken();
  const form = new FormData();
  form.append(fieldName, file);
  const headers: Record<string, string> = {};
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    body: form,
    headers,
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ error: response.statusText }));
    throw Object.assign(new Error(error.error ?? 'API error'), { status: response.status });
  }
  if (response.status === 204) return undefined as T;
  return response.json();
}

export const api = {
  // Generic methods
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: JSON.stringify(body) }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PUT', body: JSON.stringify(body) }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),

  // Event endpoints
  getEvents: () => request<EventDto[]>('/events'),
  createEvent: (req: CreateEventRequest) =>
    request<EventDto>('/events', { method: 'POST', body: JSON.stringify(req) }),
  publishEvent: (id: string) =>
    request<void>(`/events/${id}/publish`, { method: 'PUT' }),
  duplicateEvent: (id: string, req: DuplicateEventRequest) =>
    request<EventDto>(`/events/${id}/duplicate`, { method: 'POST', body: JSON.stringify(req) }),

  // CSV roster endpoints
  validateRoster: (eventId: string, file: File) =>
    upload<CsvValidationResult>(`/events/${eventId}/roster/validate`, file),
  commitRoster: (eventId: string, file: File) =>
    upload<void>(`/events/${eventId}/roster/commit`, file),

  // Hierarchy endpoints
  getRoster: (eventId: string) =>
    request<RosterHierarchyDto>(`/events/${eventId}/roster`),
  createPlatoon: (eventId: string, name: string) =>
    request<{ id: string; name: string }>(`/events/${eventId}/platoons`, { method: 'POST', body: JSON.stringify({ name }) }),
  createSquad: (platoonId: string, name: string) =>
    request<{ id: string; name: string }>(`/platoons/${platoonId}/squads`, { method: 'POST', body: JSON.stringify({ name }) }),
  assignSquad: (playerId: string, squadId: string | null) =>
    request<void>(`/event-players/${playerId}/squad`, { method: 'PUT', body: JSON.stringify({ squadId }) }),
};

// ─── Type exports ───────────────────────────────────────────────────────────

export interface EventDto {
  id: string;
  name: string;
  location: string | null;
  description: string | null;
  startDate: string | null;   // "YYYY-MM-DD"
  endDate: string | null;
  status: 'Draft' | 'Published';
}

export interface CreateEventRequest {
  name: string;
  location?: string;
  description?: string;
  startDate?: string;
  endDate?: string;
}

export interface DuplicateEventRequest {
  copyInfoSectionIds: string[];  // Always sent; [] in Phase 2 (no info sections yet)
}

export interface CsvValidationResult {
  fatalError: string | null;
  validCount: number;
  errorCount: number;
  warningCount: number;
  errors: CsvRowError[];
}

export interface CsvRowError {
  row: number;
  field: string;
  message: string;
  severity: 'Error' | 'Warning';
}

export interface RosterHierarchyDto {
  platoons: PlatoonDto[];
  unassignedPlayers: PlayerDto[];
}

export interface PlatoonDto {
  id: string;
  name: string;
  squads: SquadDto[];
}

export interface SquadDto {
  id: string;
  name: string;
  players: PlayerDto[];
}

export interface PlayerDto {
  id: string;
  name: string;
  callsign: string | null;
  teamAffiliation: string | null;
}
