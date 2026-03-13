namespace MilsimPlanning.Api.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string htmlBody)
    {
        _logger.LogInformation("Email stub: to={To} subject={Subject}", to, subject);
        return Task.CompletedTask;
    }
}
