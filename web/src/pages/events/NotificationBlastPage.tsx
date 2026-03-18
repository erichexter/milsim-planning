import { useState, type FormEvent } from 'react';
import { useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';
import { api } from '@/lib/api';
import { useAuth } from '@/hooks/useAuth';
import { getToken } from '@/lib/auth';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { EventBreadcrumb } from '@/components/EventBreadcrumb';

function formatDate(value: string) {
  return new Date(value).toLocaleString();
}

export function NotificationBlastPage() {
  const { eventId, id } = useParams<{ eventId: string; id: string }>();
  const resolvedEventId = eventId ?? id;
  const { user } = useAuth();
  const isCommander = user?.role === 'faction_commander';

  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');

  const { data: blasts = [], isLoading, refetch } = useQuery({
    queryKey: ['notification-blasts', resolvedEventId],
    queryFn: () => api.getNotificationBlasts(resolvedEventId!),
    enabled: Boolean(resolvedEventId && isCommander),
  });

  if (!resolvedEventId) return <div className="p-6">Event id missing.</div>;
  if (!isCommander) return <div className="p-6">You are not authorized to send notifications.</div>;
  if (isLoading) return <div className="p-6">Loading notifications...</div>;

  const canSend = subject.trim().length > 0 && body.trim().length > 0;

  const handleSend = async (event: FormEvent) => {
    event.preventDefault();
    if (!canSend) return;

    const token = getToken();
    const apiBase = (import.meta.env.DEV ? '' : (import.meta.env.VITE_API_URL ?? '')) + '/api';
    const response = await fetch(`${apiBase}/events/${resolvedEventId}/notification-blasts`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify({
        subject: subject.trim(),
        body: body.trim(),
      }),
    });

    if (!response.ok) {
      throw new Error('Failed to queue notification blast');
    }

    if (response.status === 202) {
      toast.success('Notification queued, emails sending...');
      setSubject('');
      setBody('');
      await refetch();
    }
  };

  return (
    <div className="mx-auto max-w-4xl lg:max-w-5xl space-y-6 p-6">
      <EventBreadcrumb eventId={resolvedEventId} page="Notifications" />
      <h1 className="text-2xl font-bold">Notification Blast</h1>

      <form onSubmit={handleSend} className="space-y-3 rounded border p-4">
        <Input
          placeholder="Subject"
          value={subject}
          onChange={(e) => setSubject(e.target.value)}
        />
        <textarea
          className="min-h-[140px] w-full rounded border p-2"
          placeholder="Message body"
          value={body}
          onChange={(e) => setBody(e.target.value)}
        />
        <Button type="submit" disabled={!canSend}>
          Send
        </Button>
      </form>

      <div className="space-y-3 rounded border p-4">
        <h2 className="font-semibold">Blast History</h2>
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b">
              <th className="py-2">Subject</th>
              <th className="py-2">Date Sent</th>
              <th className="py-2">Recipients</th>
            </tr>
          </thead>
          <tbody>
            {blasts
              .slice()
              .sort((a, b) => b.sentAt.localeCompare(a.sentAt))
              .map((blast) => (
                <tr key={blast.id} className="border-b">
                  <td className="py-2">{blast.subject}</td>
                  <td className="py-2">{formatDate(blast.sentAt)}</td>
                  <td className="py-2">{blast.recipientCount}</td>
                </tr>
              ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
