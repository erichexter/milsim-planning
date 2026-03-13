import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { api } from '../../lib/api';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '../ui/dialog';
import { Button } from '../ui/button';
import { Checkbox } from '../ui/checkbox';

interface InfoSection {
  id: string;
  title: string;
}

interface Props {
  eventId: string;
  infoSections: InfoSection[];
}

export function DuplicateEventDialog({ eventId, infoSections }: Props) {
  const [open, setOpen] = useState(false);
  const [selectedSections, setSelectedSections] = useState<string[]>([]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: () =>
      api.duplicateEvent(eventId, {
        copyInfoSectionIds: selectedSections, // Always sent; [] when no sections
      }),
    onSuccess: (newEvent) => {
      queryClient.invalidateQueries({ queryKey: ['events'] });
      setOpen(false);
      navigate(`/events/${newEvent.id}`);
    },
  });

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button variant="outline" size="sm">
          Duplicate
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Duplicate Event</DialogTitle>
          <DialogDescription>
            Platoon/squad structure will always be copied. Roster, maps, and dates will not be
            copied.
          </DialogDescription>
        </DialogHeader>

        <div>
          <p className="text-sm font-medium mb-2">Information Sections to Copy:</p>
          {infoSections.length === 0 ? (
            <p className="text-sm text-muted-foreground italic">
              No information sections exist yet. They can be added in Phase 3.
            </p>
          ) : (
            infoSections.map((section) => (
              <div key={section.id} className="flex items-center gap-2 mb-2">
                <Checkbox
                  id={`section-${section.id}`}
                  checked={selectedSections.includes(section.id)}
                  onCheckedChange={(checked) =>
                    setSelectedSections((prev) =>
                      checked
                        ? [...prev, section.id]
                        : prev.filter((id) => id !== section.id)
                    )
                  }
                />
                <label htmlFor={`section-${section.id}`} className="text-sm">
                  {section.title}
                </label>
              </div>
            ))
          )}
        </div>

        <Button onClick={() => mutation.mutate()} disabled={mutation.isPending}>
          {mutation.isPending ? 'Duplicating...' : 'Duplicate Event'}
        </Button>
      </DialogContent>
    </Dialog>
  );
}
