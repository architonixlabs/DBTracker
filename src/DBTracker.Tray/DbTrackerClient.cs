using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using DBTracker.Tray.Core;

namespace DBTracker.Tray
{
    internal enum SummaryOutcome
    {
        Ok,
        Rejected,
        Unreachable,
        Unreadable,
    }

    internal sealed class DbTrackerClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly TraySettings _settings;

        public DbTrackerClient(TraySettings settings)
        {
            _settings = settings;
            // WebRequestHandler, because .NET Framework 4.6.2's HttpClientHandler has no certificate callback.
            var handler = new WebRequestHandler { AllowAutoRedirect = false, UseProxy = false };
            if (settings.UsesHttps)
            {
                handler.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
                    TrayTls.Accept(errors == SslPolicyErrors.None, certificate?.GetCertHashString(), IsInMachineStore);
            }
            _http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(8),
            };
        }

        private static bool IsInMachineStore(string thumbprint)
        {
            try
            {
                using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                    return store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false).Count > 0;
                }
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        public async Task<HealthProbe> GetHealthAsync()
        {
            try
            {
                using (var response = await _http.GetAsync(_settings.HealthUrl).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return HealthProbe.Response((int)response.StatusCode, body);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is WebException)
            {
                return HealthProbe.Failed(ex.Message);
            }
        }

        public async Task<Tuple<SummaryOutcome, TraySummary>> GetSummaryAsync(string token)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, _settings.SummaryUrl))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenFormat.Normalize(token));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    using (var response = await _http.SendAsync(request).ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            return Tuple.Create(SummaryOutcome.Rejected, (TraySummary)null);
                        }
                        if (!response.IsSuccessStatusCode)
                        {
                            return Tuple.Create(SummaryOutcome.Unreachable, (TraySummary)null);
                        }
                        var summary = TrayJson.ParseSummary(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                        return Tuple.Create(summary == null ? SummaryOutcome.Unreadable : SummaryOutcome.Ok, summary);
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is WebException)
            {
                return Tuple.Create(SummaryOutcome.Unreachable, (TraySummary)null);
            }
        }

        public void Dispose() => _http.Dispose();
    }
}
