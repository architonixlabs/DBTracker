using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;

namespace DBTracker.Infrastructure.Install;

public static class EmailProviders
{
    public const string None = "None";
    public const string ArxMail = "ArxMail";
    public const string Smtp = "Smtp";
}

public sealed record InstallEmailSettings
{
    public string Provider { get; init; } = EmailProviders.None;
    public string ArxMailEndpoint { get; init; } = "https://mail.architonixlabs.com/v1/submit";
    public string ArxMailFromAddress { get; init; } = "";
    public string ArxMailApiKey { get; init; } = "";
    public string SmtpHost { get; init; } = "";
    public int SmtpPort { get; init; } = 587;
    public bool SmtpEnableSsl { get; init; } = true;
    public string SmtpUsername { get; init; } = "";
    public string SmtpPassword { get; init; } = "";
    public string SmtpFromAddress { get; init; } = "";
    public string SmtpFromName { get; init; } = "DBTracker";
}

public sealed record InstallConfiguration(
    string ConnectionString,
    bool ConnectionStringHasSecret,
    InstallEmailSettings Email,
    string? ReportsDistributionList,
    DateTime CompletedAtUtc,
    string CompletedBy);

public static class InstallConfigWriter
{
    public const string ProtectorPurpose = "DBTracker.InstallConfig.v1";

    public static string Serialize(InstallConfiguration config, IDataProtector protector)
    {
        string Secret(string value) => string.IsNullOrEmpty(value) ? "" : ProtectedValue.Protect(protector, value);

        var email = new JsonObject { ["Provider"] = config.Email.Provider };
        if (config.Email.Provider == EmailProviders.ArxMail)
        {
            email["ArxMail"] = new JsonObject
            {
                ["Endpoint"] = config.Email.ArxMailEndpoint,
                ["FromAddress"] = config.Email.ArxMailFromAddress,
                ["ApiKey"] = Secret(config.Email.ArxMailApiKey),
            };
        }
        else if (config.Email.Provider == EmailProviders.Smtp)
        {
            email["Smtp"] = new JsonObject
            {
                ["Host"] = config.Email.SmtpHost,
                ["Port"] = config.Email.SmtpPort,
                ["EnableSsl"] = config.Email.SmtpEnableSsl,
                ["Username"] = config.Email.SmtpUsername,
                ["Password"] = Secret(config.Email.SmtpPassword),
                ["FromAddress"] = config.Email.SmtpFromAddress,
                ["FromName"] = config.Email.SmtpFromName,
            };
        }

        var root = new JsonObject
        {
            ["ConnectionStrings"] = new JsonObject
            {
                ["DBTracker"] = config.ConnectionStringHasSecret ? Secret(config.ConnectionString) : config.ConnectionString,
            },
            ["Email"] = email,
            ["Install"] = new JsonObject
            {
                ["CompletedAtUtc"] = config.CompletedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                ["CompletedBy"] = config.CompletedBy,
            },
        };

        if (!string.IsNullOrWhiteSpace(config.ReportsDistributionList))
        {
            root["Reports"] = new JsonObject { ["DistributionList"] = config.ReportsDistributionList.Trim() };
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Writes through a temp file and a replace, so a crash mid-write never leaves half a config behind.</summary>
    public static void Write(string path, InstallConfiguration config, IDataProtector protector)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, Serialize(config, protector));
        File.Move(temp, path, overwrite: true);
    }
}
