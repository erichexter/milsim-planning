import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import { ImageUploadZone } from '../components/briefings/ImageUploadZone';
import type { ImageUploadDto, ImageUploadStatusDto } from '../lib/api';

const BRIEFING_ID = 'aaaaaaaa-0000-0000-0000-000000000001';
const UPLOAD_ID = 'cccccccc-0000-0000-0000-000000000001';

const mockUploadResponse: ImageUploadDto = {
  uploadId: UPLOAD_ID,
  status: 'Pending',
  createdAt: '2026-04-23T10:00:00Z',
};

const mockStatusResponse: ImageUploadStatusDto = {
  uploadId: UPLOAD_ID,
  uploadStatus: 'Pending',
  resizeJobs: [
    { jobId: 'job-1', dimensions: '1280x720', resizeStatus: 'Queued', completedAt: null },
    { jobId: 'job-2', dimensions: '640x480', resizeStatus: 'Queued', completedAt: null },
    { jobId: 'job-3', dimensions: '320x240', resizeStatus: 'Queued', completedAt: null },
  ],
};

function renderWithProviders(ui: React.ReactElement) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        {ui}
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('ImageUploadZone', () => {
  beforeEach(() => {
    // Override XMLHttpRequest with a fetch-compatible mock for upload tests
    server.use(
      http.post(`/api/v1/briefings/${BRIEFING_ID}/images`, () =>
        HttpResponse.json(mockUploadResponse, { status: 202 })
      ),
      http.get(`/api/v1/briefings/${BRIEFING_ID}/images/${UPLOAD_ID}`, () =>
        HttpResponse.json(mockStatusResponse)
      )
    );
  });

  it('renders the drop zone with instructions', () => {
    renderWithProviders(<ImageUploadZone briefingId={BRIEFING_ID} />);

    expect(screen.getByText(/Drag & drop a map image here/i)).toBeInTheDocument();
    expect(screen.getByText(/JPEG, PNG, WebP, AVIF/i)).toBeInTheDocument();
  });

  it('renders a hidden file input that accepts image types', () => {
    renderWithProviders(<ImageUploadZone briefingId={BRIEFING_ID} />);

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    expect(input).toBeInTheDocument();
    expect(input.accept).toContain('.png');
    expect(input.accept).toContain('.jpg');
    expect(input.accept).toContain('.webp');
    expect(input.accept).toContain('.avif');
  });

  it('shows drag-over styles when a file is dragged over the zone', () => {
    renderWithProviders(<ImageUploadZone briefingId={BRIEFING_ID} />);

    const zone = screen.getByRole('button', { name: /Drop image here/i });
    fireEvent.dragOver(zone);

    // After drag over, the zone should have updated border styling
    expect(zone.className).toContain('border-primary');
  });

  it('removes drag-over style on drag leave', () => {
    renderWithProviders(<ImageUploadZone briefingId={BRIEFING_ID} />);

    const zone = screen.getByRole('button', { name: /Drop image here/i });
    fireEvent.dragOver(zone);
    fireEvent.dragLeave(zone);

    expect(zone.className).not.toContain('border-primary bg-primary/5');
  });

  it('shows file size validation error for oversized files', async () => {
    renderWithProviders(<ImageUploadZone briefingId={BRIEFING_ID} />);

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    // 60 MB file — uses a small byte array but with size property overridden
    const bigFile = new File(['x'], 'huge.png', { type: 'image/png' });
    Object.defineProperty(bigFile, 'size', { value: 60 * 1024 * 1024 });

    fireEvent.change(input, { target: { files: [bigFile] } });

    await waitFor(() =>
      expect(screen.getByText(/File is too large/i)).toBeInTheDocument()
    );
  });

  it('shows file type validation error for disallowed extensions', async () => {
    renderWithProviders(<ImageUploadZone briefingId={BRIEFING_ID} />);

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    // Use fireEvent.change to bypass the accept attribute filter
    const badFile = new File(['content'], 'malware.exe', { type: 'application/octet-stream' });
    fireEvent.change(input, { target: { files: [badFile] } });

    await waitFor(() =>
      expect(screen.getByText(/not allowed/i)).toBeInTheDocument()
    );
  });

  it('shows "try again" button after a validation error', async () => {
    renderWithProviders(<ImageUploadZone briefingId={BRIEFING_ID} />);

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    const badFile = new File(['content'], 'malware.exe', { type: 'application/octet-stream' });
    fireEvent.change(input, { target: { files: [badFile] } });

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument()
    );
  });

  it('resets to idle state when "try again" is clicked', async () => {
    renderWithProviders(<ImageUploadZone briefingId={BRIEFING_ID} />);

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    const badFile = new File(['content'], 'malware.exe', { type: 'application/octet-stream' });
    fireEvent.change(input, { target: { files: [badFile] } });

    const tryAgainBtn = await screen.findByRole('button', { name: /try again/i });
    fireEvent.click(tryAgainBtn);

    await waitFor(() =>
      expect(screen.getByText(/Drag & drop a map image here/i)).toBeInTheDocument()
    );
  });
});

describe('BriefingEditorPage – routing to ImageUploadZone', () => {
  it('renders the correct accepting file types in the zone instructions', () => {
    renderWithProviders(<ImageUploadZone briefingId={BRIEFING_ID} />);
    // AC-01: zone must advertise the four accepted image types
    expect(screen.getByText(/JPEG, PNG, WebP, AVIF/i)).toBeInTheDocument();
    expect(screen.getByText(/50 MB/i)).toBeInTheDocument();
  });
});
