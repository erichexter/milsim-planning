using System.ComponentModel.DataAnnotations;

namespace MilsimPlanning.Api.Models.Requests;

public record RegisterRequest(
    [Required] string DisplayName,
    [Required][EmailAddress] string Email,
    [Required][MinLength(6)] string Password
);
