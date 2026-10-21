namespace Totoro.Core.Settings;

/// <summary>Shows a setting under another one's card, named by its property, e.g. the options of what a button downloads.</summary>
[AttributeUsage(AttributeTargets.Property)]
public class ParentAttribute(string name) : Attribute
{
	public string Name { get; } = name;
}
