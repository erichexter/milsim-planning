namespace MilsimPlanning.Api.Authorization;

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
