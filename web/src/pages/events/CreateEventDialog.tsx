import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api, type CreateEventRequest } from '../../lib/api';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from '../../components/ui/dialog';
import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';

export function CreateEventDialog() {
  const [open, setOpen] = useState(false);
  const [name, setName] = useState('');
  const [location, setLocation] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: (req: CreateEventRequest) => api.createEvent(req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['events'] });
      setOpen(false);
      setName('');
      setLocation('');
      setStartDate('');
      setEndDate('');
    },
  });

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>New Event</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create Event</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div>
            <Label htmlFor="event-name">Event Name *</Label>
            <Input
              id="event-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Op Thunder"
            />
          </div>
          <div>
            <Label htmlFor="event-location">Location</Label>
            <Input
              id="event-location"
              value={location}
              onChange={(e) => setLocation(e.target.value)}
            />
          </div>
          <div>
            <Label htmlFor="event-start">Start Date</Label>
            <Input
              id="event-start"
              type="date"
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
            />
          </div>
          <div>
            <Label htmlFor="event-end">End Date</Label>
            <Input
              id="event-end"
              type="date"
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
            />
          </div>
        </div>
        <Button
          onClick={() =>
            mutation.mutate({
              name,
              location: location || undefined,
              startDate: startDate || undefined,
              endDate: endDate || undefined,
            })
          }
          disabled={!name.trim() || mutation.isPending}
        >
          {mutation.isPending ? 'Creating...' : 'Create Event'}
        </Button>
      </DialogContent>
    </Dialog>
  );
}
