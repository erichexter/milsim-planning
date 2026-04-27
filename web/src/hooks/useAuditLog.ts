import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';

export interface FrequencyAuditLogDto {
  id: string;
  eventId: string;
  timestamp: string;
  unitName: string;
  unitType: string; // "Squad", "Platoon", or "Faction"
  primaryFrequency: string | null;
  alternateFrequency: string | null;
  actionType: string; // "created", "updated", "deleted", "conflict_detected", "conflict_overridden"
  userName: string;
  conflictingUnitName: string | null;
}

export interface AuditLogResponse {
  entries: FrequencyAuditLogDto[];
  total: number;
  limit: number;
  offset: number;
}

export interface AuditLogQuery {
  limit?: number;
  offset?: number;
  unitName?: string;
  startDate?: string;
  endDate?: string;
  sortBy?: 'timestamp' | 'unitName' | 'actionType';
  sortOrder?: 'asc' | 'desc';
}

/**
 * Fetch frequency assignment audit log entries for an event.
 * Supports filtering, sorting, and pagination.
 */
export function useAuditLog(eventId: string, query?: AuditLogQuery) {
  const queryParams = new URLSearchParams();

  if (query) {
    if (query.limit) queryParams.append('limit', query.limit.toString());
    if (query.offset) queryParams.append('offset', query.offset.toString());
    if (query.unitName) queryParams.append('unitName', query.unitName);
    if (query.startDate) queryParams.append('startDate', query.startDate);
    if (query.endDate) queryParams.append('endDate', query.endDate);
    if (query.sortBy) queryParams.append('sortBy', query.sortBy);
    if (query.sortOrder) queryParams.append('sortOrder', query.sortOrder);
  }

  const queryString = queryParams.toString();

  return useQuery<AuditLogResponse>({
    queryKey: ['audit-logs', eventId, queryString],
    queryFn: () =>
      api.get<AuditLogResponse>(
        `/events/${eventId}/audit-logs${queryString ? `?${queryString}` : ''}`
      ),
    enabled: !!eventId,
  });
}
