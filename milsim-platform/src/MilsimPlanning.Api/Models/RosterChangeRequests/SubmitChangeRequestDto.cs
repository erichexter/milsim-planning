using System.ComponentModel.DataAnnotations;

namespace MilsimPlanning.Api.Models.RosterChangeRequests;

public class SubmitChangeRequestDto
{
    [Required, MinLength(1)]
    public string Note { get; set; } = null!;
}
