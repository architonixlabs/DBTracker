namespace DBTracker.Infrastructure.Notifications;

public class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "DBTracker";
}

public class ArxMailOptions
{
    public string Endpoint { get; set; } = "https://mail.architonixlabs.com/v1/submit";
    public string FromAddress { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
