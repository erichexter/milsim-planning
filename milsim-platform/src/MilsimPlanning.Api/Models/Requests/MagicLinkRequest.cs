using System.ComponentModel.DataAnnotations;

namespace MilsimPlanning.Api.Models.Requests;

public record MagicLinkRequest(
    [Required] string Email
);
