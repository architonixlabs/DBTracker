using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;

namespace DBTracker.Infrastructure.Install;

public static class ProtectedValue
{
    public const string Prefix = "dp:";

    public static string Protect(IDataProtector protector, string plaintext) => Prefix + protector.Protect(plaintext);

    public static bool IsProtected(string? value) => value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Unprotect(IDataProtector protector, string value) => protector.Unprotect(value[Prefix.Length..]);
}

/// <summary>A JSON config file whose "dp:"-prefixed values are decrypted with Data Protection as it loads.</summary>
public sealed class ProtectedJsonConfigurationSource : JsonConfigurationSource
{
    public required IDataProtector Protector { get; init; }

    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new ProtectedJsonConfigurationProvider(this);
    }
}

public sealed class ProtectedJsonConfigurationProvider(ProtectedJsonConfigurationSource source) : JsonConfigurationProvider(source)
{
    public override void Load(Stream stream)
    {
        base.Load(stream);
        foreach (var key in Data.Keys.ToList())
        {
            if (!ProtectedValue.IsProtected(Data[key]))
            {
                continue;
            }

            try
            {
                Data[key] = ProtectedValue.Unprotect(source.Protector, Data[key]!);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    $"'{source.Path}' holds encrypted settings that can't be read with the keys in the 'keys' folder next to it. " +
                    "Restore that folder from backup, or delete the config file to run setup again.", ex);
            }
        }
    }
}

public static class ProtectedJsonConfigurationExtensions
{
    /// <summary>Adds the file as an optional, reloading source. Pass <paramref name="insertAt"/> to place it before later sources.</summary>
    public static IConfigurationBuilder AddProtectedJsonFile(this IConfigurationBuilder builder, string filePath, IDataProtector protector, int? insertAt = null)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        Directory.CreateDirectory(directory);

        var source = new ProtectedJsonConfigurationSource
        {
            Protector = protector,
            Path = Path.GetFileName(filePath),
            Optional = true,
            ReloadOnChange = true,
            FileProvider = new PhysicalFileProvider(directory),
        };

        if (insertAt is { } index && index >= 0 && index <= builder.Sources.Count)
        {
            builder.Sources.Insert(index, source);
        }
        else
        {
            builder.Sources.Add(source);
        }

        return builder;
    }
}
