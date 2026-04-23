namespace MilsimPlanning.Api.Models.CheckIn;

/// <summary>
/// Request body for syncing offline check-in records.
/// Contains an array of check-in records queued on the client during offline mode.
/// </summary>
public record SyncOfflineCheckInRequest(
    /// <summary>
    /// Array of offline check-in records to sync.
    /// Each record contains the QR code and the time it was queued.
    /// </summary>
    OfflineCheckInRecord[] Records
);

/// <summary>
/// Single offline check-in record from the frontend queue.
/// </summary>
public record OfflineCheckInRecord(
    /// <summary>
    /// The QR code value (participant ID).
    /// </summary>
    string QrCode,
    /// <summary>
    /// The UTC timestamp when the record was queued on the client.
    /// </summary>
    DateTime QueuedAtUtc
);
