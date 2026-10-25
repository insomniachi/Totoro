namespace Totoro.Core.Modules.Anime;

/// <summary>
/// A list entry field that only some trackers have (notes, priority...), shown in the tracking dialog below the fields
/// every tracker has. The tracker fills in its current value and reads the edited one back by <see cref="Key"/> from
/// <see cref="Tracking.Fields"/>.
/// </summary>
public abstract class TrackingField(string key, string label)
{
	public string Key { get; } = key;

	public string Label { get; } = label;

	/// <summary>
	/// Whether the user edited it. Trackers save only edited fields, so a value the tracker couldn't read exactly (e.g.
	/// one never set) isn't overwritten by what the field showed.
	/// </summary>
	public abstract bool IsChanged { get; }
}

public sealed class TextField(string key, string label, string? value) : TrackingField(key, label)
{
	private readonly string _initial = value ?? "";

	public string Value { get; set; } = value ?? "";

	public bool IsMultiline { get; init; }

	public override bool IsChanged => Value != _initial;
}

public sealed class NumberField(string key, string label, double value) : TrackingField(key, label)
{
	private readonly double _initial = value;

	// double rather than int: NumberBox works in doubles and uses NaN for an empty box.
	public double Value { get; set; } = value;

	public double Maximum { get; init; } = double.MaxValue;

	public override bool IsChanged => !double.IsNaN(Value) && Value != _initial;
}

public sealed class ToggleField(string key, string label, bool value) : TrackingField(key, label)
{
	private readonly bool _initial = value;

	public bool Value { get; set; } = value;

	public override bool IsChanged => Value != _initial;
}

public sealed class ChoiceField(string key, string label, IReadOnlyList<string> choices, string value) : TrackingField(key, label)
{
	private readonly string _initial = value;

	public IReadOnlyList<string> Choices { get; } = choices;

	public string Value { get; set; } = value;

	public override bool IsChanged => Value != _initial;

	/// <summary>The position of the value in <see cref="Choices"/>.</summary>
	public int Index => Choices.ToList().IndexOf(Value);
}

/// <summary>What the tracking dialog shows for a tracker, beyond the fields every tracker has.</summary>
/// <param name="ScoreMax">
/// The top of the scale the user scores on there, e.g. 100 for an AniList user who scores out of 100. The dialog shows
/// scores on it; <see cref="Tracking.Score"/> is out of 10 regardless.
/// </param>
/// <param name="ScoreStep">The smallest change of score on that scale, e.g. 0.1 for decimal scores.</param>
/// <param name="ScoreStyle">How the score is picked, e.g. as stars.</param>
public sealed record TrackingForm(double ScoreMax, double ScoreStep, IReadOnlyList<TrackingField> Fields, ScoreStyle ScoreStyle = ScoreStyle.Number)
{
	public static TrackingForm Default { get; } = new(10, 1, []);
}

public enum ScoreStyle
{
	Number,

	/// <summary>One star per point, up to <see cref="TrackingForm.ScoreMax"/>.</summary>
	Stars,

	/// <summary>Three faces, from unhappy to happy, for a score out of 3.</summary>
	Smileys,
}
