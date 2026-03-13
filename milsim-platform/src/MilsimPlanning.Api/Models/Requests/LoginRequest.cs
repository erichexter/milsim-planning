using System.ComponentModel.DataAnnotations;

namespace MilsimPlanning.Api.Models.Requests;

public record LoginRequest(
    [Required] string Email,
    [Required] string Password
);
