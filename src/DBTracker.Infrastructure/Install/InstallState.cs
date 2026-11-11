using Microsoft.Extensions.Configuration;

namespace DBTracker.Infrastructure.Install;

/// <summary>
/// Single source of truth for "is setup done" and "which database". The DbContext options are built per
/// scope from <see cref="ConnectionString"/>, so finishing the wizard switches the whole app to the new
/// database without a restart.
/// </summary>
public sealed class InstallState
{
    private readonly IConfiguration _configuration;
    private readonly TaskCompletionSource _installed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _isInstalled;
    private volatile string? _pendingConnectionString;

    public InstallState(IConfiguration configuration, bool installFeatureEnabled, bool initiallyInstalled)
    {
        _configuration = configuration;
        InstallFeatureEnabled = installFeatureEnabled;
        if (initiallyInstalled)
        {
            _isInstalled = true;
            _installed.TrySetResult();
        }
    }

    /// <summary>False in Development and Testing, which keep configuring the database from appsettings.</summary>
    public bool InstallFeatureEnabled { get; }

    public bool IsInstalled => _isInstalled;

    public string? ConnectionString => _pendingConnectionString ?? _configuration.GetConnectionString("DBTracker");

    /// <summary>Points data access at the database chosen in the wizard before the config file exists.</summary>
    public void UsePendingConnectionString(string connectionString)
    {
        if (_isInstalled)
        {
            throw new InvalidOperationException("DBTracker is already installed.");
        }
        _pendingConnectionString = connectionString;
    }

    public void MarkInstalled()
    {
        _isInstalled = true;
        _pendingConnectionString = null;
        _installed.TrySetResult();
    }

    public Task WaitUntilInstalledAsync(CancellationToken ct) => _installed.Task.WaitAsync(ct);
}
