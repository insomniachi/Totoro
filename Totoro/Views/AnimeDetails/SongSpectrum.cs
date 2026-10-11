using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Totoro.Core.Services;

namespace Totoro.Views.AnimeDetails;

/// <summary>
/// A song's spectrum as a row of bars, low notes on the left, redrawn every frame while <see cref="IsActive"/>: each bar
/// eases up towards its level and falls back slowly, so it reads as calm movement rather than flicker.
/// </summary>
public sealed partial class SongSpectrum : Grid
{
	// Enough that the bars are slim.
	private const int BandCount = 48;
	// How much of the way up to its level a bar goes each frame (most of it in about a sixth of a second), and how far it
	// falls each frame, as a share of the height (all of it in about a second and a half).
	private const float Rise = 0.2f;
	private const float Falloff = 0.012f;

	private readonly float[] _levels = new float[BandCount];
	private readonly float[] _shown = new float[BandCount];
	private readonly Rectangle[] _bars = new Rectangle[BandCount];
	private bool _isRendering;

	public SongSpectrum()
	{
		ColumnSpacing = 3;
		for (var i = 0; i < BandCount; i++)
		{
			ColumnDefinitions.Add(new ColumnDefinition());
			var bar = new Rectangle { Height = 2, RadiusX = 1.5, RadiusY = 1.5, VerticalAlignment = VerticalAlignment.Bottom };
			SetColumn(bar, i);
			Children.Add(bar);
			_bars[i] = bar;
		}

		Loaded += (_, _) => UpdateRendering();
		Unloaded += (_, _) => StopRendering();
	}

	public static readonly DependencyProperty SourceProperty =
		DependencyProperty.Register(nameof(Source), typeof(ISongPlayer), typeof(SongSpectrum), new PropertyMetadata(null));

	public static readonly DependencyProperty IsActiveProperty =
		DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(SongSpectrum),
									new PropertyMetadata(false, (d, _) => ((SongSpectrum)d).UpdateRendering()));

	public static readonly DependencyProperty BarBrushProperty =
		DependencyProperty.Register(nameof(BarBrush), typeof(Brush), typeof(SongSpectrum),
									new PropertyMetadata(null, (d, e) => ((SongSpectrum)d).ApplyBrush((Brush?)e.NewValue)));

	/// <summary>What's playing, whose levels are drawn.</summary>
	public ISongPlayer? Source
	{
		get => (ISongPlayer?)GetValue(SourceProperty);
		set => SetValue(SourceProperty, value);
	}

	/// <summary>Whether to draw (while the song plays); the bars stay as they are while it isn't.</summary>
	public bool IsActive
	{
		get => (bool)GetValue(IsActiveProperty);
		set => SetValue(IsActiveProperty, value);
	}

	public Brush? BarBrush
	{
		get => (Brush?)GetValue(BarBrushProperty);
		set => SetValue(BarBrushProperty, value);
	}

	private void ApplyBrush(Brush? brush)
	{
		foreach (var bar in _bars)
		{
			bar.Fill = brush;
		}
	}

	private void UpdateRendering()
	{
		if (IsActive && IsLoaded)
		{
			if (!_isRendering)
			{
				_isRendering = true;
				CompositionTarget.Rendering += OnRendering;
			}
		}
		else
		{
			StopRendering();
		}
	}

	private void StopRendering()
	{
		if (_isRendering)
		{
			_isRendering = false;
			CompositionTarget.Rendering -= OnRendering;
		}
	}

	private void OnRendering(object? sender, object e)
	{
		Source?.GetLevels(_levels);
		var height = ActualHeight;
		for (var i = 0; i < BandCount; i++)
		{
			_shown[i] = _levels[i] > _shown[i] ? _shown[i] + (_levels[i] - _shown[i]) * Rise : Math.Max(_levels[i], _shown[i] - Falloff);
			_bars[i].Height = Math.Max(2, _shown[i] * height);
		}
	}
}
