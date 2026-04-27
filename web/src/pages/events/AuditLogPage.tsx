import { useParams } from 'react-router';
import { AuditLogTable } from '../../components/AuditLogTable';
import { EventBreadcrumb } from '../../components/EventBreadcrumb';
import { ArrowLeft } from 'lucide-react';
import { Button } from '../../components/ui/button';
import { useNavigate } from 'react-router';

export function AuditLogPage() {
  const { id: eventId } = useParams<{ id: string }>();
  const navigate = useNavigate();

  if (!eventId) {
    return <div className="p-6">Event not found.</div>;
  }

  return (
    <div className="h-full flex flex-col">
      {/* Header */}
      <div className="border-b px-6 py-4 bg-background">
        <div className="flex items-center gap-2 mb-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate(-1)}
            className="gap-2"
          >
            <ArrowLeft className="h-4 w-4" />
            Back
          </Button>
        </div>
        <div className="flex items-center justify-between">
          <div>
            <EventBreadcrumb eventId={eventId} page="Audit Log" />
            <h1 className="text-2xl font-bold mt-2">Frequency Assignment Audit Log</h1>
            <p className="text-sm text-muted-foreground mt-1">
              View all frequency assignments, changes, and conflict resolution history
            </p>
          </div>
        </div>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-auto bg-background">
        <AuditLogTable eventId={eventId} />
      </div>
    </div>
  );
}
