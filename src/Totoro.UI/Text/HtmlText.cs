using System.Net;
using System.Text.RegularExpressions;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI.Text;

namespace Totoro.UI.Text;

/// <summary>
/// Shows HTML, such as AniList descriptions requested with <c>asHtml: true</c>, in a <see cref="RichTextBlock"/>.
/// AniList renders all of its markup (markdown, spoilers, centering, images, youtube...) to a small set of HTML,
/// which is what this covers:
/// <list type="bullet">
/// <item>Paragraphs and line breaks: <c>p</c>, <c>div</c>, <c>br</c>; <c>center</c>; headings <c>h1</c>-<c>h6</c>.</item>
/// <item>Text styles: <c>b</c>/<c>strong</c>, <c>i</c>/<c>em</c>, <c>u</c>/<c>ins</c>, <c>s</c>/<c>del</c>/<c>strike</c>, <c>code</c>.</item>
/// <item>Blocks: <c>ul</c>/<c>ol</c>/<c>li</c> lists, <c>blockquote</c>, <c>pre</c>, <c>hr</c>.</item>
/// <item>Links (<c>a href</c>), images (<c>img</c>), AniList spoilers (<c>span.markdown_spoiler</c>, hidden until clicked),
/// youtube (<c>span.youtube</c>) and <c>video</c>, which become links.</item>
/// </list>
/// Other tags are dropped (their text is kept) and entities decoded. As in HTML, whitespace and raw newlines collapse
/// to a single space except inside <c>pre</c>.
/// </summary>
/// <example><c>&lt;RichTextBlock text:HtmlText.Html="{x:Bind ViewModel.Description, Mode=OneWay}" /&gt;</c></example>
public static class HtmlText
{
	public static readonly DependencyProperty HtmlProperty = DependencyProperty.RegisterAttached(
		"Html", typeof(string), typeof(HtmlText), new PropertyMetadata(null, OnHtmlChanged));

	public static string? GetHtml(RichTextBlock textBlock) => (string?)textBlock.GetValue(HtmlProperty);

	public static void SetHtml(RichTextBlock textBlock, string? value) => textBlock.SetValue(HtmlProperty, value);

	private static void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not RichTextBlock textBlock)
		{
			return;
		}

		textBlock.Blocks.Clear();
		foreach (var block in new HtmlRenderer().Render(e.NewValue as string))
		{
			textBlock.Blocks.Add(block);
		}
	}
}

/// <summary>Turns HTML into RichTextBlock blocks; see <see cref="HtmlText"/> for what's supported. Single use.</summary>
internal sealed partial class HtmlRenderer
{
	private const double ParagraphSpacing = 12;
	private const double ListItemSpacing = 4;
	private const double IndentPerLevel = 20;
	private const double DefaultImageWidth = 400;

	// More than this many line breaks in a row is just empty space.
	private const int MaxConsecutiveBreaks = 2;

	private readonly List<Block> _blocks = [];

	// Where inlines go: the current paragraph's inlines, or those of a link/spoiler being built inside it.
	private readonly Stack<InlineCollection> _containers = new();
	private Paragraph? _paragraph;

	// Open inline styles (counters, since they can nest).
	private int _bold, _italic, _underline, _strike, _code;

	// Open block contexts.
	private int _center, _quote, _pre, _heading;
	private readonly Stack<ListContext> _lists = new();

	// What each open <a> / <span> turned out to be, so their closing tags undo the right thing.
	private readonly Stack<bool> _anchors = new();
	private readonly Stack<SpanKind> _spans = new();
	private readonly Stack<Span> _spoilers = new();

	public List<Block> Render(string? html)
	{
		if (string.IsNullOrEmpty(html))
		{
			return _blocks;
		}

		var position = 0;
		foreach (Match tag in TagRegex().Matches(html))
		{
			AddText(html[position..tag.Index]);
			position = tag.Index + tag.Length;

			if (tag.Groups["name"].Success)
			{
				HandleTag(tag.Groups["name"].Value.ToLowerInvariant(), tag.Groups["close"].Success, tag.Groups["attrs"].Value);
			}
		}

		AddText(html[position..]);
		EndParagraph();
		return _blocks;
	}

	private void HandleTag(string name, bool closing, string attributes)
	{
		switch (name)
		{
			case "b" or "strong": Count(ref _bold, closing); break;
			case "i" or "em": Count(ref _italic, closing); break;
			case "u" or "ins": Count(ref _underline, closing); break;
			case "s" or "del" or "strike": Count(ref _strike, closing); break;
			case "code": Count(ref _code, closing); break;

			case "br": AddBreak(); break;
			case "p" or "div": EndParagraph(); break;

			case "center":
				EndParagraph();
				Count(ref _center, closing);
				break;

			case "blockquote":
				EndParagraph();
				Count(ref _quote, closing);
				break;

			case "pre":
				EndParagraph();
				Count(ref _pre, closing);
				Count(ref _code, closing);
				break;

			case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
				EndParagraph();
				_heading = closing ? 0 : name[1] - '0';
				break;

			case "ul" or "ol":
				EndParagraph();
				if (closing)
				{
					_lists.TryPop(out _);

					// Items sit close together; the list as a whole gets the usual gap after it.
					if (_blocks.Count > 0 && _blocks[^1] is Paragraph last)
					{
						last.Margin = last.Margin with { Bottom = ParagraphSpacing };
					}
				}
				else
				{
					_lists.Push(new ListContext(ordered: name == "ol"));
				}

				break;

			case "li":
				EndParagraph();
				if (!closing)
				{
					var marker = _lists.TryPeek(out var list) && list.Ordered ? $"{++list.Counter}. " : "• ";
					AddRun(marker);
				}

				break;

			case "hr":
				EndParagraph();
				AddRule();
				break;

			case "a": HandleAnchor(closing, attributes); break;
			case "span": HandleSpan(closing, attributes); break;
			case "img" when !closing: AddImage(attributes); break;
			case "video" or "source" when !closing && Attribute(attributes, "src") is { } src: AddLink("▶ Watch video", src); break;
		}
	}

	private static void Count(ref int counter, bool closing)
	{
		counter = Math.Max(0, counter + (closing ? -1 : 1));
	}

	private void HandleAnchor(bool closing, string attributes)
	{
		if (closing)
		{
			if (_anchors.TryPop(out var wasLink) && wasLink)
			{
				_containers.Pop();
			}

			return;
		}

		// Hyperlinks can't nest, and only web links are followed.
		var isWebUri = TryGetWebUri(Attribute(attributes, "href"), out var uri);
		var isLink = isWebUri && !InsideLink;
		_anchors.Push(isLink);

		if (isLink)
		{
			var link = new Hyperlink { NavigateUri = uri };
			Current.Add(link);
			_containers.Push(link.Inlines);
		}
	}

	private void HandleSpan(bool closing, string attributes)
	{
		if (closing)
		{
			if (_spans.TryPop(out var kind) && kind == SpanKind.Spoiler)
			{
				EndSpoiler();
			}

			return;
		}

		var classes = Attribute(attributes, "class") ?? "";
		if (classes.Contains("markdown_spoiler") && !InsideLink)
		{
			_spans.Push(SpanKind.Spoiler);
			StartSpoiler();
		}
		else if (classes.Contains("youtube") && Attribute(attributes, "id") is { Length: > 0 } videoId)
		{
			_spans.Push(SpanKind.Plain);
			AddLink("▶ Watch on YouTube", $"https://www.youtube.com/watch?v={Uri.EscapeDataString(videoId)}");
		}
		else
		{
			_spans.Push(SpanKind.Plain);
		}
	}

	/// <summary>Spoiler text is collected into a span that stays hidden behind a "Spoiler" link until it's clicked.</summary>
	private void StartSpoiler()
	{
		var content = new Span();
		_spoilers.Push(content);
		_containers.Push(content.Inlines);
	}

	private void EndSpoiler()
	{
		_containers.Pop();
		var content = _spoilers.Pop();
		if (content.Inlines.Count == 0)
		{
			return;
		}

		var target = Current;
		var reveal = new Hyperlink { UnderlineStyle = UnderlineStyle.None };
		reveal.Inlines.Add(new Run { Text = "⚠ Spoiler (click to show)", FontStyle = FontStyle.Italic });
		reveal.Click += (_, _) =>
		{
			var index = target.IndexOf(reveal);
			if (index >= 0)
			{
				target.RemoveAt(index);
				target.Insert(index, content);
			}
		};
		target.Add(reveal);
	}

	private bool InsideLink => _anchors.Contains(true);

	private InlineCollection Current
	{
		get
		{
			EnsureParagraph();
			return _containers.Peek();
		}
	}

	private void EnsureParagraph()
	{
		if (_paragraph is not null)
		{
			return;
		}

		var indent = (_lists.Count + _quote) * IndentPerLevel;
		_paragraph = new Paragraph
		{
			Margin = new Thickness(indent, 0, 0, _lists.Count > 0 ? ListItemSpacing : ParagraphSpacing),
			TextAlignment = _center > 0 ? TextAlignment.Center : TextAlignment.Left,
		};

		if (_heading > 0)
		{
			_paragraph.FontWeight = FontWeights.SemiBold;
			_paragraph.FontSize = _heading switch { 1 => 24, 2 => 20, 3 => 18, _ => 16 };
		}

		if (_quote > 0)
		{
			_paragraph.FontStyle = FontStyle.Italic;
		}

		_blocks.Add(_paragraph);
		_containers.Clear();
		_containers.Push(_paragraph.Inlines);
	}

	private void EndParagraph()
	{
		if (_paragraph is null)
		{
			return;
		}

		// A link or spoiler left open across a paragraph boundary is closed with it.
		while (_spoilers.Count > 0)
		{
			EndSpoiler();
		}

		TrimTrailing(_paragraph.Inlines);
		if (_paragraph.Inlines.Count == 0)
		{
			_blocks.Remove(_paragraph);
		}

		_paragraph = null;
		_containers.Clear();
	}

	private void AddText(string raw)
	{
		if (raw.Length == 0)
		{
			return;
		}

		var text = WebUtility.HtmlDecode(raw);

		if (_pre > 0)
		{
			// Keep the text as written, line breaks included.
			var lines = text.Replace("\r\n", "\n").Split('\n');
			for (var i = 0; i < lines.Length; i++)
			{
				if (i > 0)
				{
					Current.Add(new LineBreak());
				}

				if (lines[i].Length > 0)
				{
					AddRun(lines[i]);
				}
			}

			return;
		}

		text = WhitespaceRegex().Replace(text, " ");
		if (AtLineStart())
		{
			text = text.TrimStart();
		}

		if (text.Length > 0)
		{
			AddRun(text);
		}
	}

	private void AddRun(string text)
	{
		var run = new Run { Text = text };
		if (_bold > 0)
		{
			run.FontWeight = FontWeights.SemiBold;
		}

		if (_italic > 0)
		{
			run.FontStyle = FontStyle.Italic;
		}

		if (_underline > 0 || _strike > 0)
		{
			run.TextDecorations = (_underline > 0 ? TextDecorations.Underline : TextDecorations.None)
								  | (_strike > 0 ? TextDecorations.Strikethrough : TextDecorations.None);
		}

		if (_code > 0)
		{
			run.FontFamily = new FontFamily("Consolas");
		}

		Current.Add(run);
	}

	private void AddBreak()
	{
		// No leading breaks, and no trailing space before one.
		if (_paragraph is null || _paragraph.Inlines.Count == 0)
		{
			return;
		}

		var inlines = Current;
		if (inlines.Count > 0 && inlines[^1] is Run run)
		{
			run.Text = run.Text.TrimEnd();
		}

		var consecutive = 0;
		for (var i = inlines.Count - 1; i >= 0 && inlines[i] is LineBreak; i--)
		{
			consecutive++;
		}

		if (consecutive < MaxConsecutiveBreaks)
		{
			inlines.Add(new LineBreak());
		}
	}

	private bool AtLineStart()
	{
		if (_paragraph is null)
		{
			return true;
		}

		var inlines = _containers.Peek();
		return inlines.Count == 0 ? _paragraph.Inlines.Count == 0 || _paragraph.Inlines[^1] is LineBreak : inlines[^1] is LineBreak;
	}

	private void AddLink(string text, string url)
	{
		if (!TryGetWebUri(url, out var uri))
		{
			return;
		}

		if (InsideLink)
		{
			AddRun(text);
			return;
		}

		var link = new Hyperlink { NavigateUri = uri };
		link.Inlines.Add(new Run { Text = text });
		Current.Add(link);
	}

	private void AddImage(string attributes)
	{
		if (!TryGetWebUri(Attribute(attributes, "src"), out var uri))
		{
			return;
		}

		var width = double.TryParse(Attribute(attributes, "width")?.TrimEnd('%', 'p', 'x'), out var w) && w > 0 ? w : DefaultImageWidth;
		Current.Add(new InlineUIContainer
		{
			Child = new Image
			{
				Source = new BitmapImage(uri),
				MaxWidth = width,
				Stretch = Stretch.Uniform,
			},
		});
	}

	private void AddRule()
	{
		EnsureParagraph();
		Current.Add(new InlineUIContainer
		{
			Child = new Rectangle
			{
				Height = 1,
				Width = 600,
				Fill = new SolidColorBrush(Colors.Gray) { Opacity = 0.4 },
			},
		});
		EndParagraph();
	}

	private static void TrimTrailing(InlineCollection inlines)
	{
		while (inlines.Count > 0)
		{
			switch (inlines[^1])
			{
				case LineBreak:
					inlines.RemoveAt(inlines.Count - 1);
					continue;
				case Run run:
					run.Text = run.Text.TrimEnd();
					if (run.Text.Length == 0)
					{
						inlines.RemoveAt(inlines.Count - 1);
						continue;
					}

					break;
			}

			break;
		}
	}

	private static bool TryGetWebUri(string? url, out Uri? uri)
	{
		uri = Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https" ? parsed : null;
		return uri is not null;
	}

	private static string? Attribute(string attributes, string name)
	{
		foreach (Match match in AttributeRegex().Matches(attributes))
		{
			if (string.Equals(match.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase))
			{
				return WebUtility.HtmlDecode(match.Groups["value"].Value);
			}
		}

		return null;
	}

	private sealed class ListContext(bool ordered)
	{
		public bool Ordered { get; } = ordered;
		public int Counter { get; set; }
	}

	private enum SpanKind
	{
		Plain,
		Spoiler,
	}

	// Comments match without a name, so they're skipped.
	[GeneratedRegex(@"<!--.*?-->|<(?<close>/)?(?<name>[a-zA-Z][a-zA-Z0-9]*)(?<attrs>[^>]*)>", RegexOptions.Singleline)]
	private static partial Regex TagRegex();

	[GeneratedRegex("""(?<name>[a-zA-Z-]+)\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)'|(?<value>[^\s>]+))""")]
	private static partial Regex AttributeRegex();

	[GeneratedRegex(@"\s+")]
	private static partial Regex WhitespaceRegex();
}
