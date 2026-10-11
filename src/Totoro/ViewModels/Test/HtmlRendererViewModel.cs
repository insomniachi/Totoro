using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Totoro.ViewModels.Test;

/// <summary>A playground for <c>Totoro.UI.Text.HtmlText</c>: edit HTML on one side, see it rendered on the other.</summary>
public partial class HtmlRendererViewModel : ObservableObject
{
	[ObservableProperty]
	public partial string Html { get; set; } = Sample;

	[RelayCommand]
	private void Reset() => Html = Sample;

	/// <summary>Everything the renderer supports, written the way AniList sends it (description(asHtml: true)).</summary>
	public const string Sample = """
		<h1>HTML renderer showcase</h1>
		<p>Everything here is HTML, as AniList returns descriptions with <code>asHtml: true</code>. Edit it on the left to try things out.</p>

		<h2>Text styles</h2>
		<p><strong>Bold</strong> (<code>__bold__</code>), <b>also bold</b>, <em>italic</em> (<code>_italic_</code>), <i>also italic</i>,
		<del>strikethrough</del> (<code>~~strike~~</code>), <u>underline</u> and <code>inline code</code>.
		Styles <strong>nest: <em>bold italic, <del>and struck</del></em></strong>.</p>

		<h2>Line breaks and paragraphs</h2>
		<p>A line,<br>a line break,<br><br>and a blank line. Raw newlines
		like this one are just spaces, as in HTML; extra breaks<br><br><br><br>are capped at one blank line.</p>

		<h2>Links and spoilers</h2>
		<p>A <a href="https://anilist.co/anime/150672">link to [Oshi no Ko] on AniList</a>. Only http and https links are followed.</p>
		<p>A spoiler, from <code>~!spoiler!~</code>: <span class='markdown_spoiler'><span>Hidden until clicked, and it can hold <strong>styles</strong> and <a href="https://anilist.co">links</a>.</span></span></p>

		<h2>Headings</h2>
		<h3>Heading 3</h3>
		<h4>Heading 4</h4>
		<h5>Heading 5</h5>

		<h2>Alignment and blocks</h2>
		<center><p>Centered, from <code>~~~centered~~~</code></p></center>
		<blockquote><p>A quote, from <code>&gt; quote</code>.</p></blockquote>
		<ul>
		  <li>A bullet list</li>
		  <li>With a <strong>styled</strong> item</li>
		  <li>And a nested list
		    <ol><li>First</li><li>Second</li></ol>
		  </li>
		</ul>
		<ol><li>A numbered list</li><li>Counts by itself</li></ol>
		<hr />
		<pre><code>A code block keeps its
		    indentation and
		line breaks.</code></pre>

		<h2>Media</h2>
		<p>An image, from <code>img220(url)</code>:</p>
		<p><img width="120" src="https://s4.anilist.co/file/anilistcdn/media/anime/cover/small/bx150672-WqmmwZ4nMzAy.png"></p>
		<p>A video, from <code>youtube(id)</code>: <span class='youtube' id='gKWEUJ4r5do'></span></p>

		<h2>Everything else</h2>
		<p>Entities are decoded: &amp; &lt;tag&gt; &quot;quotes&quot; &#9733;.
		Unknown tags like <marquee>this one</marquee> are dropped but keep their text, and comments<!-- like this --> are hidden.</p>
		""";
}
