namespace MilsimPlanning.Api.Models.Rsvp;

public class RsvpDto
{
    public Guid EventId { get; set; }
    public string UserId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime RespondedAt { get; set; }
}

public class SetRsvpRequest
{
    public string Status { get; set; } = null!;
}
