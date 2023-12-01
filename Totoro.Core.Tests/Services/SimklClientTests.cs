using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Refit;
using Totoro.Core.Services.Simkl;

namespace Totoro.Core.Tests.Services;

public class SimklClientTests
{
    private readonly RefitSettings _settings;
    private readonly string _token;

    public SimklClientTests()
    {
        _settings = new RefitSettings
        {
            HttpMessageHandlerFactory = () => new SimklHandler() { InnerHandler = new HttpClientHandler() }
        };
    }

    [Fact]
    public async Task GetItems()
    {
        var sut = RestService.For<ISimklClient>("https://api.simkl.com", _settings);

        var result = await sut.GetSummary(1812124);
    }
}
