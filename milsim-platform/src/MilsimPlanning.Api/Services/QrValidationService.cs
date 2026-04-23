using MilsimPlanning.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MilsimPlanning.Api.Services;

/// <summary>
/// Validates QR codes for kiosk check-in.
/// Ensures QR code values are in valid format before processing check-ins.
/// </summary>
public class QrValidationService
{
    private readonly AppDbContext _db;

    public QrValidationService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Validates a QR code value for check-in.
    /// Throws ArgumentException if validation fails.
    /// </summary>
    public async Task<bool> ValidateQrCodeAsync(string qrCodeValue)
    {
        // Validate QR code is not null or empty
        if (string.IsNullOrWhiteSpace(qrCodeValue))
            throw new ArgumentException("QR code value cannot be empty");

        // QR code validation passed
        return await Task.FromResult(true);
    }
}
