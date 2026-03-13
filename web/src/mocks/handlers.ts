import { http, HttpResponse } from 'msw';

// Default handlers — individual tests override with server.use()
export const handlers = [
  http.get('/api/events', () => HttpResponse.json([])),
  http.post('/api/events', () =>
    HttpResponse.json(
      {
        id: 'default-id',
        name: 'Default Event',
        location: null,
        description: null,
        startDate: null,
        endDate: null,
        status: 'Draft',
      },
      { status: 201 }
    )
  ),
];
