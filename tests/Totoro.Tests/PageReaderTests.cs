using System.Text.Json;
using Totoro.Core.Web;
using Xunit;

namespace Totoro.Tests;

public class PageReaderTests
{
	[Fact]
	public void Parse_ReadsTheScriptsResult()
	{
		var result = JsonSerializer.Serialize("""{"url":"https://example.com/","type":"text/html","title":"Just a moment...","check":true,"blocked":false,"content":"<html></html>"}""");
		Assert.Equal(new PageState("https://example.com/", "text/html", true, false, "<html></html>"), PageReader.Parse(result));
	}

	[Fact]
	public void Parse_ReadsAPageStillLoadingAsACheck()
	{
		Assert.True(PageReader.Parse("null").Check);
	}
}
