using System.ComponentModel.DataAnnotations;

namespace MilsimPlanning.Api.Models.Requests;

public record PasswordResetRequest(
    [Required] string Email
);
