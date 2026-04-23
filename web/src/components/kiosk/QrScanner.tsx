import { useEffect, useRef, useState } from 'react';
import jsQR from 'jsqr';

interface QrScannerProps {
  onQrDetected: (qrCode: string) => void;
}

export function QrScanner({ onQrDetected }: QrScannerProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [permissionDenied, setPermissionDenied] = useState(false);
  const [successState, setSuccessState] = useState(false);
  const [errorState, setErrorState] = useState(false);
  const audioRef = useRef<HTMLAudioElement>(null);
  const scanTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const errorTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  // UUID v4 regex pattern
  const uuidRegex =
    /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

  const isValidUUID = (value: string): boolean => {
    return uuidRegex.test(value);
  };

  useEffect(() => {
    const startCamera = async () => {
      try {
        const stream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: 'environment' },
        });

        if (videoRef.current) {
          try {
            videoRef.current.srcObject = stream;
            videoRef.current.play().catch(() => {
              // Ignore play errors in test environments
            });
          } catch (error) {
            // Handle case where srcObject is not supported (in some test environments)
            console.error('Failed to set video source:', error);
            // Still store the stream for cleanup
            (videoRef.current as any)._stream = stream;
          }
        }
      } catch (error) {
        console.error('Camera permission denied:', error);
        setPermissionDenied(true);
      }
    };

    startCamera();

    return () => {
      if (videoRef.current) {
        // Try to stop tracks from srcObject first
        if (videoRef.current.srcObject) {
          const stream = videoRef.current.srcObject as MediaStream;
          stream.getTracks().forEach((track) => track.stop());
        }
        // Also try fallback stored stream (for test environments)
        const stream = (videoRef.current as any)._stream;
        if (stream && typeof stream.getTracks === 'function') {
          stream.getTracks().forEach((track: MediaStreamTrack) => track.stop());
        }
      }
    };
  }, []);

  useEffect(() => {
    if (permissionDenied) return;

    const scanFrame = () => {
      const video = videoRef.current;
      const canvas = canvasRef.current;

      if (!video || !canvas) return;

      if (video.readyState === video.HAVE_ENOUGH_DATA) {
        const context = canvas.getContext('2d');
        if (!context) return;

        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;

        context.drawImage(video, 0, 0, canvas.width, canvas.height);
        const imageData = context.getImageData(0, 0, canvas.width, canvas.height);

        const code = jsQR(imageData.data, imageData.width, imageData.height);

        if (code && code.data) {
          const qrValue = code.data;

          // Validate UUID format
          if (isValidUUID(qrValue)) {
            // Clear any pending error state
            if (errorTimeoutRef.current) {
              clearTimeout(errorTimeoutRef.current);
              errorTimeoutRef.current = null;
            }
            setErrorState(false);

            // Show success state
            setSuccessState(true);

            // Play beep sound
            if (audioRef.current) {
              audioRef.current.play().catch((err) => {
                console.warn('Failed to play sound:', err);
              });
            }

            // Call parent callback
            onQrDetected(qrValue);

            // Reset success state after 1 second
            if (scanTimeoutRef.current) {
              clearTimeout(scanTimeoutRef.current);
            }
            scanTimeoutRef.current = setTimeout(() => {
              setSuccessState(false);
            }, 1000);

            return;
          } else {
            // Invalid QR format (non-UUID)
            setErrorState(true);

            // Clear error state after 1 second
            if (errorTimeoutRef.current) {
              clearTimeout(errorTimeoutRef.current);
            }
            errorTimeoutRef.current = setTimeout(() => {
              setErrorState(false);
            }, 1000);
          }
        }
      }

      requestAnimationFrame(scanFrame);
    };

    const frameId = requestAnimationFrame(scanFrame);

    return () => {
      cancelAnimationFrame(frameId);
      if (scanTimeoutRef.current) {
        clearTimeout(scanTimeoutRef.current);
      }
      if (errorTimeoutRef.current) {
        clearTimeout(errorTimeoutRef.current);
      }
    };
  }, [permissionDenied, onQrDetected]);

  if (permissionDenied) {
    return (
      <div
        className="w-full h-screen flex items-center justify-center bg-background"
        data-testid="camera-permission-denied"
      >
        <div className="text-center">
          <h2 className="text-2xl font-bold text-foreground mb-2">
            Camera permission required for QR scanning
          </h2>
          <p className="text-muted-foreground">
            Please enable camera access to use the QR scanner.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="relative w-full h-screen bg-black overflow-hidden">
      {/* Video element - fullscreen */}
      <video
        ref={videoRef}
        className="absolute inset-0 w-full h-full object-cover"
        data-testid="qr-video"
      />

      {/* Hidden canvas for frame processing */}
      <canvas ref={canvasRef} className="hidden" data-testid="qr-canvas" />

      {/* Success overlay - green */}
      {successState && (
        <div
          className="absolute inset-0 bg-green-500 opacity-40"
          data-testid="success-overlay"
        />
      )}

      {/* Error overlay - red */}
      {errorState && (
        <div
          className="absolute inset-0 bg-red-500 opacity-40"
          data-testid="error-overlay"
        />
      )}

      {/* Hidden audio element for beep sound */}
      <audio
        ref={audioRef}
        src="data:audio/wav;base64,UklGRiYAAABXQVZFZm10IBAAAAABAAEAQB8AAAB9AAACABAAZGF0YQIAAAAAAA=="
        data-testid="qr-beep"
      />
    </div>
  );
}
