using Totoro.Core.Settings;
using Xunit;

namespace Totoro.Tests;

public class DataContainerConfigTests
{
	private sealed class Config : DataContainerConfig
	{
		public long Size { get; set; } = 1;
		public int? Limit { get; set; }
		public DayOfWeek Day { get; set; }
		public string Name { get; set; } = "a";
		public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(1);
		[System.ComponentModel.DataAnnotations.Range(0, 10)] public int Level { get; set; } = 5;
		[System.ComponentModel.DataAnnotations.AllowedValues("Sub", "Dub")] public string Audio { get; set; } = "Sub";
		public string ReadOnly => "fixed";
	}

	[Fact]
	public void PropertiesAreBoundToTheSettings()
	{
		var config = new Config();
		var container = config.ToDataContainer();
		var size = container.GetProperty(nameof(Config.Size))!;

		size.Value = 5L;
		Assert.Equal(5L, config.Size);

		config.Size = 9;
		Assert.Equal(9L, size.Value);
	}

	[Fact]
	public void TrySetValueConvertsAndChecks()
	{
		var config = new Config();
		var container = config.ToDataContainer();
		bool Set(string name, object? value) => container.GetProperty(name)!.TrySetValue(value, out _);

		// Converted: another kind of number, and text.
		Assert.True(Set(nameof(Config.Size), 7));
		Assert.Equal(7L, config.Size);
		Assert.True(Set(nameof(Config.Delay), "00:05:00"));
		Assert.Equal(TimeSpan.FromMinutes(5), config.Delay);
		Assert.True(Set(nameof(Config.Day), "friday"));
		Assert.Equal(DayOfWeek.Friday, config.Day);
		Assert.True(Set(nameof(Config.Limit), "3"));
		Assert.Equal(3, config.Limit);

		// Null only where the type takes it.
		Assert.True(Set(nameof(Config.Limit), null));
		Assert.Null(config.Limit);
		Assert.False(Set(nameof(Config.Size), null));

		// Choices matched by name, ignoring case; others refused.
		Assert.True(Set(nameof(Config.Audio), "dub"));
		Assert.Equal("Dub", config.Audio);
		Assert.False(Set(nameof(Config.Audio), "Raw"));

		// Not valid: refused, and the setting keeps its value.
		Assert.False(Set(nameof(Config.Level), 11));
		Assert.False(Set(nameof(Config.Size), "not a number"));
		Assert.False(Set(nameof(Config.Day), "99"));
		Assert.False(Set(nameof(Config.ReadOnly), "x"));
		Assert.Equal(5, config.Level);
		Assert.Equal(7L, config.Size);
	}

	[Fact]
	public void TrySetValueSaysWhyForAnAgent()
	{
		var container = new Config().ToDataContainer();

		Assert.False(container.GetProperty(nameof(Config.Delay))!.TrySetValue("soon", out var error));
		Assert.Contains("h:mm:ss", error);
		Assert.False(container.GetProperty(nameof(Config.Level))!.TrySetValue(20, out error));
		Assert.Contains("between 0 and 10", error);
	}

	[Fact]
	public void PropertiesMadeOnTheFlyHoldTheirValue()
	{
		var name = new DataContainerProperty { Name = "Name", Value = "" };
		var count = new DataContainerPropertyBuilder().WithName("Count").WithType(typeof(int?)).Build();

		Assert.True(name.TrySetValue("Totoro", out _));
		Assert.Equal("Totoro", name.Value);
		Assert.Equal(DataContainerEditors.NumberBox, count.EditorType);
		Assert.True(count.TrySetValue("4", out _));
		Assert.Equal(4, count.Value);
	}

	[Fact]
	public void BuilderCallsCanComeInAnyOrder()
	{
		var choices = new object[] { 1, 2 };
		var first = new DataContainerPropertyBuilder().WithAllowedValues(choices).WithEditorType(DataContainerEditors.Slider).WithType(typeof(int)).Build();
		var second = new DataContainerPropertyBuilder().WithType(typeof(int)).WithEditorType(DataContainerEditors.Slider).WithAllowedValues(choices).Build();

		Assert.Equal(first.EditorType, second.EditorType);
		Assert.Equal(first.AllowedValues, second.AllowedValues);
	}

	[Fact]
	public void TheDialogWritesBackOnlyValidEdits()
	{
		var config = new Config();
		var container = config.ToDataContainer();
		var dialog = new Totoro.Core.Dialogs.DataContainerDialogViewModel(new("Test", container));
		dialog.Properties.GetProperty(nameof(Config.Name))!.Value = "b";
		dialog.Properties.GetProperty(nameof(Config.Level))!.Value = 50;

		// One edit isn't valid: nothing is written, and the dialog stays open.
		Assert.False(dialog.ConfirmAsync().Result);
		Assert.Equal("a", config.Name);

		dialog.Properties.GetProperty(nameof(Config.Level))!.Value = 8;
		Assert.True(dialog.ConfirmAsync().Result);
		Assert.Equal("b", config.Name);
		Assert.Equal(8, config.Level);
	}

	[Flags]
	public enum Days { None = 0, Monday = 1, Tuesday = 2 }

	public sealed class TypedConfig : DataContainerConfig
	{
		public DateOnly Date { get; set; }
		public DateTimeOffset? Since { get; set; }
		public TimeOnly Time { get; set; }
		public TimeSpan Delay { get; set; }
		public System.Drawing.Color Accent { get; set; }
		public List<string> Tags { get; set; } = [];
		public string[] Names { get; set; } = [];
		public Days Days { get; set; }
		public bool? Maybe { get; set; }
	}

	[Fact]
	public void PicksEditorsByType()
	{
		var editors = new TypedConfig().ToDataContainer().ToDictionary(x => x.Name, x => x.EditorType);

		Assert.Equal(DataContainerEditors.DatePicker, editors[nameof(TypedConfig.Date)]);
		Assert.Equal(DataContainerEditors.DatePicker, editors[nameof(TypedConfig.Since)]);
		Assert.Equal(DataContainerEditors.TimePicker, editors[nameof(TypedConfig.Time)]);
		Assert.Equal(DataContainerEditors.Duration, editors[nameof(TypedConfig.Delay)]);
		Assert.Equal(DataContainerEditors.Color, editors[nameof(TypedConfig.Accent)]);
		Assert.Equal(DataContainerEditors.Tokens, editors[nameof(TypedConfig.Tags)]);
		Assert.Equal(DataContainerEditors.Tokens, editors[nameof(TypedConfig.Names)]);
		Assert.Equal(DataContainerEditors.Flags, editors[nameof(TypedConfig.Days)]);
		Assert.Equal(DataContainerEditors.ToggleSwitch, editors[nameof(TypedConfig.Maybe)]);
	}

	[Fact]
	public void SavesAndReadsBackEveryType()
	{
		var descriptor = new Totoro.Core.Modules.Descriptor { Id = Guid.NewGuid(), Name = "Test", DataFolder = $"Tests-{Guid.NewGuid():N}" };
		var settings = new ModuleSettings<TypedConfig>(descriptor);
		try
		{
			var container = settings.Value.ToDataContainer();
			container.GetProperty(nameof(TypedConfig.Date))!.Value = new DateOnly(2026, 11, 12);
			container.GetProperty(nameof(TypedConfig.Since))!.Value = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(5.5));
			container.GetProperty(nameof(TypedConfig.Time))!.Value = new TimeOnly(7, 30);
			container.GetProperty(nameof(TypedConfig.Delay))!.Value = TimeSpan.FromMinutes(90);
			container.GetProperty(nameof(TypedConfig.Accent))!.Value = System.Drawing.Color.FromArgb(128, 10, 20, 30);
			container.GetProperty(nameof(TypedConfig.Tags))!.Value = new List<string> { "a", "b" };
			container.GetProperty(nameof(TypedConfig.Names))!.Value = new[] { "x" };
			container.GetProperty(nameof(TypedConfig.Days))!.Value = Days.Monday | Days.Tuesday;
			settings.Save();

			// A new instance reads the file, as after restarting the app.
			var read = new ModuleSettings<TypedConfig>(descriptor).Value;
			Assert.Equal(new DateOnly(2026, 11, 12), read.Date);
			Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(5.5)), read.Since);
			Assert.Equal(new TimeOnly(7, 30), read.Time);
			Assert.Equal(TimeSpan.FromMinutes(90), read.Delay);
			Assert.Equal(System.Drawing.Color.FromArgb(128, 10, 20, 30).ToArgb(), read.Accent.ToArgb());
			Assert.Equal(["a", "b"], read.Tags);
			Assert.Equal(["x"], read.Names);
			Assert.Equal(Days.Monday | Days.Tuesday, read.Days);
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(Totoro.Core.Helpers.FileHelper.GetModulePath(descriptor, "settings.json"))!, recursive: true);
		}
	}

	[Theory]
	[InlineData("#FF102030", unchecked((int)0xFF102030))]
	[InlineData("#102030", unchecked((int)0xFF102030))]
	[InlineData("#80102030", unchecked((int)0x80102030))]
	public void ReadsHexColors(string text, int argb)
	{
		Assert.Equal(argb, HexColorJsonConverter.FromHex(text)!.Value.ToArgb());
		Assert.Equal(text.Length == 7 ? $"#FF{text[1..]}" : text, HexColorJsonConverter.ToHex(System.Drawing.Color.FromArgb(argb)));
	}

	[Theory]
	[InlineData("red")]
	[InlineData("#12345")]
	[InlineData("#GG0000")]
	public void RejectsOtherColorText(string text) => Assert.Null(HexColorJsonConverter.FromHex(text));

	[Theory]
	[InlineData(typeof(DateOnly), "2026-11-12")]
	[InlineData(typeof(TimeOnly), "07:30:00")]
	[InlineData(typeof(TimeSpan), "01:30:00")]
	[InlineData(typeof(int), "42")]
	[InlineData(typeof(bool), "True")]
	[InlineData(typeof(Days), "Monday, Tuesday")]
	[InlineData(typeof(System.Drawing.Color), "#FF102030")]
	[InlineData(typeof(string[]), "a, b")]
	public void SettingTextReadsWhatItWrites(Type type, string text)
	{
		Assert.True(SettingText.TryParse(text, type, out var value));
		Assert.Equal(text, SettingText.Format(value));
	}
}
