using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Tests.Services;

public class RealDebrid
{
    private static readonly string _magnet = "magnet:?xt=urn:btih:SHH6VTPGDLYSHH2GJ4LQXMZLD2HX7VRK&amp;tr=http%3A%2F%2Fnyaa.tracker.wf%3A7777%2Fannounce&amp;tr=http%3A%2F%2Fanidex.moe%3A6969%2Fannounce&amp;tr=http%3A%2F%2Ftracker.anirena.com%3A80%2Fannounce&amp;tr=udp%3A%2F%2Fopen.stealth.si%3A80%2Fannounce&amp;tr=udp%3A%2F%2Ftracker.opentrackr.org%3A1337%2Fannounce&amp;dn=%5BErai-raws%5D%20Sousou%20no%20Frieren%20-%2015%20%5B1080p%5D%5BMultiple%20Subtitle%5D%20%5BENG%5D%5BPOR-BR%5D%5BSPA-LA%5D%5BSPA%5D%5BARA%5D%5BFRE%5D%5BGER%5D%5BITA%5D%5BRUS%5D";

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
        return new RealDebridService(Mock.Of<ISettings>());
    }
}
