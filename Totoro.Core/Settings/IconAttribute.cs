namespace Totoro.Core.Settings;

[AttributeUsage(AttributeTargets.Property)]
public class IconAttribute(string iconKey) : Attribute
{
	public string IconKey { get; } = iconKey;
}

