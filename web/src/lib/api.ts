import { getToken, clearToken } from './auth';

// In dev mode the Vite proxy rewrites /api/* to the backend — use a relative path.
// In production builds VITE_API_URL is set at build time to the Container App URL.
const BASE_URL = (import.meta.env.DEV ? '' : (import.meta.env.VITE_API_URL ?? '')) + '/api';

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getToken();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string>),
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const response = await fetch(`${BASE_URL}${path}`, { ...options, headers });

  if (!response.ok) {
    if (response.status === 401) {
      clearToken();
      window.location.href = '/auth/login';
      return undefined as T;   // navigation is in flight; prevent further processing
    }
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
    if (response.status === 401) {
      clearToken();
      window.location.href = '/auth/login';
      return undefined as T;
    }
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
  patch: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PATCH', body: JSON.stringify(body) }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),

  // Event endpoints
  getEvents: () => request<EventDto[]>('/events'),
  createEvent: (req: CreateEventRequest) =>
    request<EventDto>('/events', { method: 'POST', body: JSON.stringify(req) }),
  updateEvent: (id: string, req: UpdateEventRequest) =>
    request<EventDto>(`/events/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
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
  createPlatoon: (eventId: string, name: string, isCommandElement = false) =>
    request<{ id: string; name: string; isCommandElement: boolean }>(`/events/${eventId}/platoons`, { method: 'POST', body: JSON.stringify({ name, isCommandElement }) }),
  createSquad: (platoonId: string, name: string) =>
    request<{ id: string; name: string }>(`/platoons/${platoonId}/squads`, { method: 'POST', body: JSON.stringify({ name }) }),
  assignSquad: (playerId: string, squadId: string | null) =>
    request<void>(`/event-players/${playerId}/squad`, { method: 'PUT', body: JSON.stringify({ squadId }) }),
  setPlayerRole: (playerId: string, role: string | null) =>
    request<void>(`/event-players/${playerId}/role`, { method: 'PATCH', body: JSON.stringify({ role }) }),
  assignPlayerToPlatoon: (playerId: string, platoonId: string | null) =>
    request<void>(`/event-players/${playerId}/platoon`, { method: 'PUT', body: JSON.stringify({ platoonId }) }),
  bulkAssign: (eventId: string, playerIds: string[], destination: string) =>
    request<void>(`/events/${eventId}/players/bulk-assign`, { method: 'POST', body: JSON.stringify({ playerIds, destination }) }),

  // Info sections + attachments
  getInfoSections: (eventId: string) =>
    request<InfoSection[]>(`/events/${eventId}/info-sections`),
  createInfoSection: (eventId: string, payload: { title: string; bodyMarkdown: string | null; order: number }) =>
    request<InfoSection>(`/events/${eventId}/info-sections`, { method: 'POST', body: JSON.stringify(payload) }),
  updateInfoSection: (eventId: string, sectionId: string, payload: { title: string; bodyMarkdown: string | null; order: number }) =>
    request<void>(`/events/${eventId}/info-sections/${sectionId}`, { method: 'PUT', body: JSON.stringify(payload) }),
  deleteInfoSection: (eventId: string, sectionId: string) =>
    request<void>(`/events/${eventId}/info-sections/${sectionId}`, { method: 'DELETE' }),
  reorderInfoSections: (eventId: string, orderedIds: string[]) =>
    request<void>(`/events/${eventId}/info-sections/reorder`, { method: 'PATCH', body: JSON.stringify({ orderedIds }) }),
  getInfoSectionUploadUrl: (eventId: string, sectionId: string) =>
    request<UploadUrlResponse>(`/events/${eventId}/info-sections/${sectionId}/attachments/upload-url`),
  confirmInfoSectionAttachment: (
    eventId: string,
    sectionId: string,
    payload: { r2Key: string; friendlyName: string; contentType: string; fileSizeBytes: number }
  ) => request<SectionAttachment>(`/events/${eventId}/info-sections/${sectionId}/attachments/confirm`, { method: 'POST', body: JSON.stringify(payload) }),
  getInfoSectionAttachmentDownloadUrl: (eventId: string, sectionId: string, attachmentId: string) =>
    request<{ downloadUrl: string }>(`/events/${eventId}/info-sections/${sectionId}/attachments/${attachmentId}/download-url`),

  // Map resources
  getMapResources: (eventId: string) =>
    request<MapResource[]>(`/events/${eventId}/map-resources`),
  createExternalMapResource: (
    eventId: string,
    payload: { externalUrl: string; instructions: string | null; friendlyName: string | null }
  ) => request<MapResource>(`/events/${eventId}/map-resources/external`, { method: 'POST', body: JSON.stringify(payload) }),
  deleteMapResource: (eventId: string, resourceId: string) =>
    request<void>(`/events/${eventId}/map-resources/${resourceId}`, { method: 'DELETE' }),
  getMapResourceUploadUrl: (eventId: string, resourceId: string) =>
    request<UploadUrlResponse>(`/events/${eventId}/map-resources/${resourceId}/upload-url`),
  confirmMapResourceUpload: (
    eventId: string,
    resourceId: string,
    payload: { r2Key: string; friendlyName: string; contentType: string; fileSizeBytes: number }
  ) => request<void>(`/events/${eventId}/map-resources/${resourceId}/confirm`, { method: 'POST', body: JSON.stringify(payload) }),
  getMapResourceDownloadUrl: (eventId: string, resourceId: string) =>
    request<{ downloadUrl: string }>(`/events/${eventId}/map-resources/${resourceId}/download-url`),

  // Notification blasts
  getNotificationBlasts: (eventId: string) =>
    request<NotificationBlast[]>(`/events/${eventId}/notification-blasts`),
  createNotificationBlast: (eventId: string, payload: { subject: string; body: string }) =>
    request<{ blastId: string; recipientCount: number }>(`/events/${eventId}/notification-blasts`, { method: 'POST', body: JSON.stringify(payload) }),

  // Frequency endpoints
  getFrequencies: (eventId: string) =>
    request<FrequencyResponseDto>(`/events/${eventId}/frequencies`),
  updateSquadFrequency: (squadId: string, req: UpdateFrequencyRequest) =>
    request<void>(`/squads/${squadId}/frequencies`, { method: 'PUT', body: JSON.stringify(req) }),
  updatePlatoonFrequency: (platoonId: string, req: UpdateFrequencyRequest) =>
    request<void>(`/platoons/${platoonId}/frequencies`, { method: 'PUT', body: JSON.stringify(req) }),
  updateCommandFrequency: (eventId: string, req: UpdateFrequencyRequest) =>
    request<void>(`/events/${eventId}/command-frequencies`, { method: 'PUT', body: JSON.stringify(req) }),

  // Profile
  getProfile: () => request<UserProfile>('/profile'),
};

export interface UserProfile {
  email: string;
  callsign: string | null;
  displayName: string | null;
  role: string;
}

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

export interface UpdateEventRequest {
  name: string;
  location: string | null;
  description: string | null;
  startDate: string | null;
  endDate: string | null;
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
  isCommandElement: boolean;
  hqPlayers: PlayerDto[];
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
  role: string | null;
}

export interface SectionAttachment {
  id: string;
  friendlyName: string;
  contentType: string;
  fileSizeBytes: number;
}

export interface InfoSection {
  id: string;
  title: string;
  bodyMarkdown: string | null;
  order: number;
  attachments: SectionAttachment[];
}

export interface UploadUrlResponse {
  uploadId: string;
  presignedPutUrl: string;
  r2Key: string;
}

export interface MapResource {
  id: string;
  externalUrl: string | null;
  instructions: string | null;
  r2Key: string | null;
  friendlyName: string | null;
  contentType: string | null;
  isFile: boolean;
  order: number;
}

export interface NotificationBlast {
  id: string;
  subject: string;
  sentAt: string;
  recipientCount: number;
}

// ─── Frequency types ────────────────────────────────────────────────────────

export interface FrequencyResponseDto {
  command: FrequencyBandDto | null;
  platoons: PlatoonFrequencyDto[] | null;
  squads: SquadFrequencyDto[] | null;
}

export interface FrequencyBandDto {
  primary: string | null;
  backup: string | null;
}

export interface PlatoonFrequencyDto {
  platoonId: string;
  platoonName: string;
  primary: string | null;
  backup: string | null;
}

export interface SquadFrequencyDto {
  squadId: string;
  squadName: string;
  platoonId: string;
  primary: string | null;
  backup: string | null;
}

export interface UpdateFrequencyRequest {
  primary: string | null;
  backup: string | null;
}
