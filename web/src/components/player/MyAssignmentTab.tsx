import { useQuery } from '@tanstack/react-query';
import { api } from '../../lib/api';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { ChangeRequestForm } from './ChangeRequestForm';
import { PendingRequestCard } from './PendingRequestCard';

interface AssignmentDto {
  id: string;
  name: string;
  callsign: string | null;
  teamAffiliation: string | null;
  role: string | null;
  platoon: { id: string; name: string } | null;
  squad: { id: string; name: string } | null;
  isAssigned: boolean;
}

interface ChangeRequestDto {
  id: string;
  note: string;
  status: string;
  commanderNote: string | null;
  createdAt: string;
}

interface MyAssignmentTabProps {
  eventId: string;
}

export function MyAssignmentTab({ eventId }: MyAssignmentTabProps) {
  const { data: assignment, isLoading: assignmentLoading, error: assignmentError } = useQuery<AssignmentDto>({
    queryKey: ['events', eventId, 'my-assignment'],
    queryFn: async () => {
      try {
        return await api.get<AssignmentDto>(`/events/${eventId}/my-assignment`);
      } catch (e: unknown) {
        // 404 = no EventPlayer record — treat as unassigned (Pitfall 4)
        const err = e as { status?: number };
          if (err?.status === 404) {
          return { id: '', name: '', callsign: null, teamAffiliation: null, role: null, platoon: null, squad: null, isAssigned: false };
        }
        throw e;
      }
    },
  });

  const { data: myRequest } = useQuery<ChangeRequestDto | null>({
    queryKey: ['events', eventId, 'roster-change-requests', 'mine'],
    queryFn: async () => {
      try {
        const result = await api.get<ChangeRequestDto>(`/events/${eventId}/roster-change-requests/mine`);
        return result;
      } catch (e: unknown) {
        const err = e as { status?: number };
        // 204 is returned as undefined from api.get — handle gracefully
        if (err?.status === 204) return null;
        throw e;
      }
    },
  });

  if (assignmentLoading) {
    return (
      <div className="space-y-3 p-4">
        <div className="h-24 rounded-lg bg-muted animate-pulse" />
        <div className="h-16 rounded-lg bg-muted animate-pulse" />
      </div>
    );
  }

  if (assignmentError && !assignment) {
    return (
      <div className="p-4 text-sm text-destructive">
        Failed to load assignment. Please refresh.
      </div>
    );
  }

  const isUnassigned = !assignment?.isAssigned;

  return (
    <div className="space-y-4">
      {/* Assignment Card */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">My Assignment</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {/* LOCKED: PLAY-06 callsign display — orange monospace */}
          {assignment?.callsign ? (
            <div className="font-mono text-2xl font-bold text-orange-500">
              [{assignment.callsign}]
            </div>
          ) : (
            <div className="font-mono text-2xl font-bold text-muted-foreground">[—]</div>
          )}

          {isUnassigned ? (
            <div className="rounded-md bg-muted p-3">
              <p className="text-sm font-medium">Unassigned</p>
              <p className="text-xs text-muted-foreground mt-1">
                You have not been assigned to a platoon and squad yet.
              </p>
            </div>
          ) : (
            <div className="text-sm space-y-1">
              {assignment?.platoon && (
                <p>
                  <span className="text-muted-foreground">Platoon:</span>{' '}
                  <span className="font-medium">{assignment.platoon.name}</span>
                </p>
              )}
              {assignment?.squad && (
                <p>
                  <span className="text-muted-foreground">Squad:</span>{' '}
                  <span className="font-medium">{assignment.squad.name}</span>
                </p>
              )}
              {assignment?.teamAffiliation && (
                <p>
                  <span className="text-muted-foreground">Team:</span>{' '}
                  <span className="font-medium">{assignment.teamAffiliation}</span>
                </p>
              )}
              {assignment?.role && (
                <p>
                  <span className="text-muted-foreground">Role:</span>{' '}
                  <span className="font-medium">{assignment.role}</span>
                </p>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Change Request Section */}
      <div>
        <h3 className="text-sm font-semibold mb-2 text-muted-foreground uppercase tracking-wide">
          Roster Change Request
        </h3>
        {myRequest?.status === 'Pending' ? (
          <PendingRequestCard eventId={eventId} request={myRequest} />
        ) : (
          <ChangeRequestForm eventId={eventId} isUnassigned={isUnassigned} />
        )}
      </div>
    </div>
  );
}
