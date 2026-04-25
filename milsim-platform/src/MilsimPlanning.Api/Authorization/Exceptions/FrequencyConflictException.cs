namespace MilsimPlanning.Api.Authorization;

/// <summary>
/// Thrown when a frequency assignment conflicts with an existing assignment on the same channel.
/// Maps to HTTP 409 Conflict.
/// </summary>
public class FrequencyConflictException : Exception
{
    public string ConflictingSquadName { get; }
    public decimal ConflictingFrequency { get; }
    public string ConflictType { get; } // "primary" | "alternate"

    public FrequencyConflictException(string conflictingSquadName, decimal conflictingFrequency, string conflictType)
        : base($"Frequency {conflictingFrequency:F3} MHz conflicts with {conflictType} frequency assigned to '{conflictingSquadName}'.")
    {
        ConflictingSquadName = conflictingSquadName;
        ConflictingFrequency = conflictingFrequency;
        ConflictType = conflictType;
    }
}
