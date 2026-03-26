namespace MilsimPlanning.Api.Models.Responses;

public record RegisterResponse(string Token, string UserId, string Email, string DisplayName, string Role);
