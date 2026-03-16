import { useState } from 'react';
import { useParams } from 'react-router';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api, RosterHierarchyDto } from '../../lib/api';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { Card, CardContent } from '../../components/ui/card';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../../components/ui/dialog';

interface PendingRequestDto {
  id: string;
  note: string;
  createdAt: string;
  player: {
    name: string;
    callsign: string | null;
    platoonId: string | null;
    squadId: string | null;
  };
}

export function ChangeRequestsPage() {
  const { id: eventId } = useParams<{ id: string }>();
  const queryClient = useQueryClient();

  const [approveDialogRequest, setApproveDialogRequest] = useState<PendingRequestDto | null>(null);
  const [denyDialogRequest, setDenyDialogRequest] = useState<PendingRequestDto | null>(null);
  const [selectedPlatoonId, setSelectedPlatoonId] = useState('');
  const [selectedSquadId, setSelectedSquadId] = useState('');
  const [commanderNote, setCommanderNote] = useState('');

  const { data: requests = [], isLoading } = useQuery<PendingRequestDto[]>({
    queryKey: ['events', eventId, 'roster-change-requests'],
    queryFn: () => api.get<PendingRequestDto[]>(`/events/${eventId}/roster-change-requests`),
  });

  const { data: roster } = useQuery<RosterHierarchyDto>({
    queryKey: ['roster', eventId],
    queryFn: () => api.getRoster(eventId!),
  });

  const approveMutation = useMutation({
    mutationFn: (requestId: string) =>
      api.post(`/events/${eventId}/roster-change-requests/${requestId}/approve`, {
        platoonId: selectedPlatoonId,
        squadId: selectedSquadId,
        commanderNote: commanderNote || null,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['events', eventId, 'roster-change-requests'] });
      setApproveDialogRequest(null);
      setSelectedPlatoonId('');
      setSelectedSquadId('');
      setCommanderNote('');
    },
  });

  const denyMutation = useMutation({
    mutationFn: (requestId: string) =>
      api.post(`/events/${eventId}/roster-change-requests/${requestId}/deny`, {
        commanderNote: commanderNote || null,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['events', eventId, 'roster-change-requests'] });
      setDenyDialogRequest(null);
      setCommanderNote('');
    },
  });

  const selectedPlatoon = roster?.platoons.find((p) => p.id === selectedPlatoonId);

  if (isLoading) return <div className="p-6">Loading change requests...</div>;

  return (
    <div className="p-6 max-w-3xl mx-auto space-y-4">
      <h1 className="text-2xl font-bold">Roster Change Requests</h1>

      {requests.length === 0 && (
        <p className="text-muted-foreground">No pending change requests.</p>
      )}

      {requests.map((request) => (
        <Card key={request.id}>
          <CardContent className="pt-4 space-y-3">
            <div className="flex items-start justify-between gap-3">
              <div>
                <span className="font-mono font-bold text-orange-500 text-sm">
                  [{request.player.callsign ?? '—'}]
                </span>
                <span className="ml-2 text-sm font-medium">{request.player.name}</span>
                <Badge variant="secondary" className="ml-2 text-xs">Pending</Badge>
              </div>
              <span className="text-xs text-muted-foreground shrink-0">
                {new Date(request.createdAt).toLocaleDateString()}
              </span>
            </div>
            <p className="text-sm text-muted-foreground">{request.note}</p>
            <div className="flex gap-2">
              <Button
                className="min-h-[44px]"
                onClick={() => {
                  setApproveDialogRequest(request);
                  setCommanderNote('');
                  setSelectedPlatoonId('');
                  setSelectedSquadId('');
                }}
              >
                Approve
              </Button>
              <Button
                variant="outline"
                className="min-h-[44px]"
                onClick={() => {
                  setDenyDialogRequest(request);
                  setCommanderNote('');
                }}
              >
                Deny
              </Button>
            </div>
          </CardContent>
        </Card>
      ))}

      {/* Approve Dialog */}
      <Dialog open={!!approveDialogRequest} onOpenChange={(open) => !open && setApproveDialogRequest(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Approve Change Request</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <p className="text-sm text-muted-foreground">
              Assign player to a platoon and squad:
            </p>
            {/* Platoon Select */}
            <div>
              <label className="text-sm font-medium mb-1 block">Platoon</label>
              <select
                className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={selectedPlatoonId}
                onChange={(e) => {
                  setSelectedPlatoonId(e.target.value);
                  setSelectedSquadId('');
                }}
              >
                <option value="">Select platoon...</option>
                {roster?.platoons.map((p) => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
            </div>
            {/* Squad Select — filtered by selected platoon */}
            <div>
              <label className="text-sm font-medium mb-1 block">Squad</label>
              <select
                className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={selectedSquadId}
                onChange={(e) => setSelectedSquadId(e.target.value)}
                disabled={!selectedPlatoonId}
              >
                <option value="">Select squad...</option>
                {selectedPlatoon?.squads.map((s) => (
                  <option key={s.id} value={s.id}>{s.name}</option>
                ))}
              </select>
            </div>
            {/* Commander note */}
            <div>
              <label className="text-sm font-medium mb-1 block">
                Commander Note <span className="text-muted-foreground">(optional)</span>
              </label>
              <textarea
                className="w-full min-h-[60px] rounded-md border border-input bg-background px-3 py-2 text-sm resize-none"
                placeholder="Add a note..."
                value={commanderNote}
                onChange={(e) => setCommanderNote(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setApproveDialogRequest(null)}>
              Cancel
            </Button>
            <Button
              onClick={() => approveDialogRequest && approveMutation.mutate(approveDialogRequest.id)}
              disabled={!selectedPlatoonId || !selectedSquadId || approveMutation.isPending}
            >
              {approveMutation.isPending ? 'Approving...' : 'Confirm Approve'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Deny Dialog */}
      <Dialog open={!!denyDialogRequest} onOpenChange={(open) => !open && setDenyDialogRequest(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Deny Change Request</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <label className="text-sm font-medium mb-1 block">
                Commander Note <span className="text-muted-foreground">(optional)</span>
              </label>
              <textarea
                className="w-full min-h-[80px] rounded-md border border-input bg-background px-3 py-2 text-sm resize-none"
                placeholder="Reason for denial..."
                value={commanderNote}
                onChange={(e) => setCommanderNote(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDenyDialogRequest(null)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => denyDialogRequest && denyMutation.mutate(denyDialogRequest.id)}
              disabled={denyMutation.isPending}
            >
              {denyMutation.isPending ? 'Denying...' : 'Confirm Deny'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
