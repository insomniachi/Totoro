using System.Text.Json;
using Xunit.Abstractions;

namespace Totoro.Plugins.Manga.MangaDex.Tests
{
    public class ChapterProviderTests
    {
        private readonly ITestOutputHelper _output;
        private readonly JsonSerializerOptions _searializerOption = new() { WriteIndented = true };

        public ChapterProviderTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("3d27afe8-bac8-4ff8-b9f7-09f4c6c7b607")]
        public async Task GetImages(string id)
        {
            var sut = new ChapterProvider();

            await foreach (var item in sut.GetImages(new Contracts.ChapterModel { Id = id }))
            {
                _output.WriteLine(item);
            }
        }
    }
}
