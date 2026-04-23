import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QrScanner } from '../components/kiosk/QrScanner';

// Mock jsQR module
vi.mock('jsqr', () => ({
  default: vi.fn(),
}));

import jsQR from 'jsqr';

const mockJsQR = jsQR as ReturnType<typeof vi.fn>;

describe('QrScanner', () => {
  let mockGetUserMedia: ReturnType<typeof vi.fn>;
  let mockVideoTrack: any;
  let originalMediaDevices: any;

  beforeEach(() => {
    // Reset all mocks
    vi.clearAllMocks();

    // Save original navigator.mediaDevices
    originalMediaDevices = navigator.mediaDevices;

    // Setup mock video track
    mockVideoTrack = {
      stop: vi.fn(),
      kind: 'video',
      enabled: true,
      readyState: 'live',
    };

    // Create a proper mock stream object
    const mockStream = {
      getTracks: vi.fn(() => [mockVideoTrack]),
      getVideoTracks: vi.fn(() => [mockVideoTrack]),
      getAudioTracks: vi.fn(() => []),
    };

    // Setup getUserMedia mock
    mockGetUserMedia = vi.fn().mockResolvedValue(mockStream);

    // Mock navigator.mediaDevices
    Object.defineProperty(navigator, 'mediaDevices', {
      value: { getUserMedia: mockGetUserMedia },
      writable: true,
      configurable: true,
    });

    // Mock jsQR by default to return null
    mockJsQR.mockReturnValue(null);
  });

  afterEach(() => {
    // Restore original navigator.mediaDevices
    Object.defineProperty(navigator, 'mediaDevices', {
      value: originalMediaDevices,
      writable: true,
      configurable: true,
    });
  });

  it('displays camera permission denied UI when camera access is denied', async () => {
    mockGetUserMedia.mockRejectedValue(
      new DOMException('Permission denied', 'NotAllowedError')
    );

    render(<QrScanner onQrDetected={vi.fn()} />);

    // Wait for permission denied message to display
    const permissionDeniedContainer = await waitFor(() =>
      screen.getByTestId('camera-permission-denied')
    );

    expect(permissionDeniedContainer).toBeInTheDocument();
    expect(
      screen.getByText('Camera permission required for QR scanning')
    ).toBeInTheDocument();
    expect(
      screen.getByText('Please enable camera access to use the QR scanner.')
    ).toBeInTheDocument();
  });

  it('renders all required elements for QR scanning when camera is available', async () => {
    const { container } = render(<QrScanner onQrDetected={vi.fn()} />);

    // Wait for camera to be requested
    await waitFor(() => {
      expect(mockGetUserMedia).toHaveBeenCalled();
    });

    // Video element should be visible (fullscreen scanner)
    const video = container.querySelector('video[data-testid="qr-video"]');
    expect(video).toBeTruthy();
    expect(video).toHaveClass('absolute', 'inset-0', 'w-full', 'h-full');

    // Canvas should be present for frame processing (hidden)
    const canvas = container.querySelector('canvas[data-testid="qr-canvas"]');
    expect(canvas).toBeTruthy();
    expect(canvas).toHaveClass('hidden');

    // Audio element should be present for beep sound
    const audio = container.querySelector('audio[data-testid="qr-beep"]');
    expect(audio).toBeTruthy();
    expect(audio).toHaveAttribute('src');
  });

  it('requests camera with environment (rear) facing mode for tablet scanning', async () => {
    render(<QrScanner onQrDetected={vi.fn()} />);

    await waitFor(() => {
      expect(mockGetUserMedia).toHaveBeenCalledWith(
        expect.objectContaining({
          video: expect.objectContaining({
            facingMode: 'environment',
          }),
        })
      );
    });
  });

  it('initializes camera on component mount and handles permission flow', async () => {
    render(<QrScanner onQrDetected={vi.fn()} />);

    // Wait for camera to be initialized
    await waitFor(() => {
      expect(mockGetUserMedia).toHaveBeenCalledTimes(1);
    });

    // Verify the component is in the DOM
    expect(document.querySelector('video')).toBeTruthy();
  });
});
