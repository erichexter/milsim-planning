namespace MilsimPlanning.Api.Models.CheckIn;

/// <summary>
/// Response from syncing offline check-in records.
/// AC-06: Backend endpoint returns { synced: int, failed: int, errors: [] }
/// </summary>
public record SyncOfflineCheckInResponse(
    /// <summary>
    /// Number of records successfully synced to the database.
    /// </summary>
    int Synced,
    /// <summary>
    /// Number of records that failed to sync (e.g., invalid QR code, already checked in).
    /// </summary>
    int Failed,
    /// <summary>
    /// Array of errors for records that failed to sync.
    /// Each error includes the QR code and error message.
    /// </summary>
    SyncError[] Errors
);

/// <summary>
/// Single error from a failed sync attempt.
/// </summary>
public record SyncError(
    /// <summary>
    /// The QR code value that failed to sync.
    /// </summary>
    string QrCode,
    /// <summary>
    /// The error message explaining why the sync failed.
    /// </summary>
    string Error
);
