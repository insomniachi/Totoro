using Microsoft.UI.Xaml;
using Totoro.Core.Settings;

namespace Totoro.UI.Controls;

/// <summary>
/// A settings editor, for the settings asking for <see cref="EditorType"/>: each of the app's own (see
/// <see cref="DataContainerEditors"/>) is one, and a plugin adds its own for a name of its own (with
/// <c>[Editor("MyPlugin.Gauge", "")]</c>), registered in the module's ConfigureServices with
/// <c>services.AddSingleton&lt;IPropertyEditor, GaugeEditor&gt;()</c> or
/// <c>services.AddSingleton&lt;IPropertyEditor&gt;(new PropertyEditor("MyPlugin.Gauge", CreateGauge))</c>. One registered
/// for a name of <see cref="DataContainerEditors"/> replaces the app's; the last registered wins.
/// </summary>
public interface IPropertyEditor
{
	string EditorType { get; }

	/// <summary>
	/// The control editing <paramref name="property"/>: it writes <see cref="DataContainerProperty.Value"/> back as
	/// <see cref="DataContainerProperty.ValueType"/> (a value of another type isn't saved), always as a new object (the
	/// dialog's copies share the settings' until saved), and follows changes to it with
	/// <see cref="DataContainerEditor.Sync"/>. The card around it shows the setting's name and description.
	/// </summary>
	FrameworkElement Create(DataContainerProperty property);
}

/// <summary>An editor made by a function: how the app's own are made, and the short way to make one.</summary>
public sealed class PropertyEditor(string editorType, Func<DataContainerProperty, FrameworkElement> create) : IPropertyEditor
{
	public string EditorType { get; } = editorType;

	public FrameworkElement Create(DataContainerProperty property) => create(property);
}
