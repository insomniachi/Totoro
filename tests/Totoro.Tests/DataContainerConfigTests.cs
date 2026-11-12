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
		public string ReadOnly => "fixed";
	}

	[Fact]
	public void UpdateValuesSavesEveryTypeTheEditorWrites()
	{
		var config = new Config();
		var container = config.ToDataContainer();
		container.GetProperty(nameof(Config.Size))!.Value = 5L;
		container.GetProperty(nameof(Config.Limit))!.Value = 3;
		container.GetProperty(nameof(Config.Day))!.Value = DayOfWeek.Friday;
		container.GetProperty(nameof(Config.Name))!.Value = null; // not a string: keeps the current value
		container.Add(new DataContainerProperty { Name = "Missing", Value = 1 });

		config.UpdateValues(container);

		Assert.Equal(5L, config.Size);
		Assert.Equal(3, config.Limit);
		Assert.Equal(DayOfWeek.Friday, config.Day);
		Assert.Equal("a", config.Name);
	}

	private sealed class TextConfig : DataContainerConfig
	{
		public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(1);
		public int? Limit { get; set; } = 2;
	}

	[Fact]
	public void UpdateValuesReadsTextBoxTextForOtherTypes()
	{
		var config = new TextConfig();
		var container = config.ToDataContainer();
		container.GetProperty(nameof(TextConfig.Delay))!.Value = "00:05:00";
		container.GetProperty(nameof(TextConfig.Limit))!.Value = "not a number";

		config.UpdateValues(container);

		Assert.Equal(TimeSpan.FromMinutes(5), config.Delay);
		Assert.Equal(2, config.Limit);

		container.GetProperty(nameof(TextConfig.Limit))!.Value = "7";
		config.UpdateValues(container);
		Assert.Equal(7, config.Limit);
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
			settings.Value.UpdateValues(container);
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
}
