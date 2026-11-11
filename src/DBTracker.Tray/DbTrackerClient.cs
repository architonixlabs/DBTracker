using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
            _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
            {
                Timeout = TimeSpan.FromSeconds(8),
            };
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
