namespace Totoro.Core.Settings;

public static class DataContainerExtensions
{
	extension(DataContainerProperty item)
	{
		public string? GetString(string defaultValue = "")
		{
			return item.GetValue(defaultValue);
		}

		public bool GetBool(bool defaultValue = false)
		{
			return item.GetValue(defaultValue);
		}

		public int GetInt32(int defaultValue = 0)
		{
			return item.GetValue(defaultValue);
		}

		public double GetDouble(double defaultValue = 0)
		{
			return item.GetValue(defaultValue);
		}
	}

	extension(DataContainer options)
	{
		public string GetString(string name, string defaultValue = "")
		{
			return options.GetProperty(name)?.GetValue(defaultValue) ?? defaultValue;
		}

		public bool GetBool(string name, bool defaultValue = false)
		{
			return options.GetProperty(name)?.GetValue(defaultValue) ?? defaultValue;
		}

		public int GetInt32(string name, int defaultValue = 0)
		{
			return options.GetProperty(name)?.GetValue(defaultValue) ?? defaultValue;
		}

		public double GetDouble(string name, double defaultValue = 0)
		{
			return options.GetProperty(name)?.GetValue(defaultValue) ?? defaultValue;
		}

		public T? GetValue<T>(string name, T? defaultValue = default)
		{
			return options.GetProperty(name) is not { } option ? defaultValue : option.GetValue(defaultValue);
		}
	}
}

