using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Tests.Services
{
    public class PremiumizeServiceTests
    {
        private static readonly string _apiKey = "";
        private static readonly HttpClient _httpClient = new();
        private static readonly string _magnet = "magnet:?xt=urn:btih:7b556cf6bf0fe445043a841ce149a92c5be4a62e%26dn=%" +
                                                 "5BHorribleSubs%5D%20One%20Punch%20Man%20S2%20-%2009%20%5B1080p%5D.mk" +
                                                 "v%26tr=http%3A%2F%2Fnyaa.tracker.wf%3A7777%2Fannounce%26tr=udp%3A%2F%2" +
                                                 "Fopen.stealth.si%3A80%2Fannounce%26tr=udp%3A%2F%2Ftracker.opentrackr." +
                                                 "org%3A1337%2Fannounce%26tr=udp%3A%2F%2Fexodus.desync.com%3A6969%2Fannoun" +
                                                 "ce%26tr=udp%3A%2F%2Ftracker.torrent.eu.org%3A451%2Fannounce";

        [Fact]
        public async Task Check()
        {
            // arrange
            var service = GetService();

            // act 
            var result = await service.Check(_magnet);

            // assert
            Assert.True(result);
        }

        [Fact]
        public async Task DirectDownload()
        {
            // arrange
            var service = GetService();

            // act 
            var result = await service.GetDirectDownloadLinks(_magnet);
        }

        [Fact]
        public async Task Transfers()
        {
            // arrange
            var service = GetService();

            // act 
            var result = await service.GetTransfers();
        }

        [Fact]
        public async Task CreateTransfer()
        {
            // arrange
            var service = GetService();
            var magneticLink = "";

            // act 
            var result = await service.CreateTransfer(magneticLink);
        }


        private static IDebridService GetService()
        {
            var settingsMock = new Mock<ILocalSettingsService>();
            settingsMock.Setup(x => x.ReadSetting(PremiumizeService.Key, string.Empty)).Returns(_apiKey);
            return new PremiumizeService(_httpClient, settingsMock.Object);
        }
    }
}
