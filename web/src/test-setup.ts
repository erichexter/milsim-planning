import '@testing-library/jest-dom';
import { beforeAll, beforeEach, afterEach, afterAll } from 'vitest';
import { server } from './mocks/server';

// Ensure a root element exists for React rendering in tests
beforeEach(() => {
  if (!document.getElementById('root')) {
    const root = document.createElement('div');
    root.id = 'root';
    document.body.appendChild(root);
  }
});

afterEach(() => {
  const root = document.getElementById('root');
  if (root) {
    root.remove();
  }
  server.resetHandlers();
});

beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }));
afterAll(() => server.close());
