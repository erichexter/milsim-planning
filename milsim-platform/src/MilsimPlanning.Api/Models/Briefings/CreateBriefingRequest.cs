using System.ComponentModel.DataAnnotations;

namespace MilsimPlanning.Api.Models.Briefings;

public class CreateBriefingRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Title is required.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }
}
