using System.ComponentModel.DataAnnotations;

namespace MilsimPlanning.Api.Models.Requests;

public record InviteUserRequest(
    [Required] string Email,
    [Required] string Callsign,
    [Required] string DisplayName,
    string Role = "player"
);
