using System.ComponentModel.DataAnnotations;

namespace MilsimPlanning.Api.Models.Requests;

public record ConfirmPasswordResetRequest(
    [Required] string UserId,
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword
);
