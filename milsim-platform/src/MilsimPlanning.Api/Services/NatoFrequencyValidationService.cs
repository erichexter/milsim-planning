using MilsimPlanning.Api.Data.Entities;

namespace MilsimPlanning.Api.Services;

/// <summary>
/// Validates NATO radio frequencies for VHF (30.0–69.975 MHz) and UHF (225.0–400.0 MHz)
/// with mandatory 25 kHz channel spacing.
/// </summary>
public class NatoFrequencyValidationService
{
    // VHF: 30.000–69.975 MHz (25 kHz spacing)
    private const decimal VhfMin = 30.000m;
    private const decimal VhfMax = 69.975m;

    // UHF: 225.000–400.000 MHz (25 kHz spacing)
    private const decimal UhfMin = 225.000m;
    private const decimal UhfMax = 400.000m;

    /// <summary>
    /// Validates that a frequency is within NATO range for the given scope and aligns to 25 kHz.
    /// Throws <see cref="ArgumentException"/> if invalid and <paramref name="overrideValidation"/> is false.
    /// If <paramref name="overrideValidation"/> is true, validation is skipped.
    /// </summary>
    public void Validate(decimal frequency, ChannelScope scope, bool overrideValidation = false)
    {
        if (overrideValidation) return;

        var (min, max, label) = scope switch
        {
            ChannelScope.VHF => (VhfMin, VhfMax, "VHF (30.0–69.975 MHz)"),
            ChannelScope.UHF => (UhfMin, UhfMax, "UHF (225.0–400.0 MHz)"),
            _ => throw new ArgumentException($"Unknown channel scope: {scope}")
        };

        if (frequency < min || frequency > max)
            throw new ArgumentException(
                $"Frequency {frequency} MHz is out of range for {label}.");

        // 25 kHz alignment check: (freq * 1000) % 25 == 0
        var thousandths = Math.Round(frequency * 1000m);
        if (thousandths % 25 != 0)
            throw new ArgumentException(
                $"Frequency {frequency} MHz does not align to 25 kHz spacing. " +
                "Must be a multiple of 0.025 MHz (e.g. 36.500, 36.525).");
    }
}
