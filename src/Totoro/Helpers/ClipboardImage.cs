using Windows.ApplicationModel.DataTransfer;

namespace Totoro.Helpers;

/// <summary>The image copied to the clipboard, e.g. a screenshot. Read from the UI thread.</summary>
public static class ClipboardImage
{
	/// <summary>The image and its type (e.g. "image/png"); null if the clipboard has none.</summary>
	public static async Task<(Stream Image, string ContentType)?> OpenAsync()
	{
		var content = Clipboard.GetContent();
		if (!content.Contains(StandardDataFormats.Bitmap))
		{
			return null;
		}

		using var image = await (await content.GetBitmapAsync()).OpenReadAsync();
		// In memory, so its length is known: uploaded as it is, the clipboard's stream goes chunked, which trace.moe waits on.
		var copy = new MemoryStream();
		await image.AsStreamForRead().CopyToAsync(copy);
		copy.Position = 0;
		return (copy, string.IsNullOrEmpty(image.ContentType) ? "image/png" : image.ContentType);
	}
}
