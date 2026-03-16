import { Link } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { ChevronRight } from 'lucide-react';
import { api } from '../lib/api';

interface EventBreadcrumbProps {
  eventId: string;
  page: string;
}

export function EventBreadcrumb({ eventId, page }: EventBreadcrumbProps) {
  const { data: events } = useQuery({
    queryKey: ['events'],
    queryFn: () => api.getEvents(),
  });

  const eventName = events?.find((e) => e.id === eventId)?.name ?? 'Event';

  return (
    <nav className="flex items-center gap-1.5 text-sm text-muted-foreground mb-6">
      <Link to="/dashboard" className="hover:text-foreground transition-colors">
        Dashboard
      </Link>
      <ChevronRight className="h-3.5 w-3.5 shrink-0" />
      <Link
        to={`/events/${eventId}`}
        className="hover:text-foreground transition-colors max-w-[200px] truncate"
      >
        {eventName}
      </Link>
      <ChevronRight className="h-3.5 w-3.5 shrink-0" />
      <span className="text-foreground font-medium">{page}</span>
    </nav>
  );
}
