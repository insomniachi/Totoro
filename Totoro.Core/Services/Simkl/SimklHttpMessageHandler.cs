using Microsoft.Extensions.Configuration;

namespace Totoro.Core.Services.Simkl
{
    internal class SimklHttpMessageHandler : DelegatingHandler
    {
        private readonly ILocalSettingsService _localSettingsService;
        private readonly IConfiguration _configuration;

        public SimklHttpMessageHandler(ILocalSettingsService localSettingsService,
                                       IConfiguration configuration)
        {
            _localSettingsService = localSettingsService;
            _configuration = configuration;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add("simkl-api-key", $"{_configuration["ClientIdSimkl"]}");
            request.Headers.Add("Authorization", $"Bearer {_localSettingsService.ReadSetting<string>("SimklToken")}");
            return base.SendAsync(request, cancellationToken);
        }
    }
}
