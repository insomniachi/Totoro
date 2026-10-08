namespace Totoro.Core.Settings;

public class DataContainer(IEnumerable<DataContainerProperty> items) : List<DataContainerProperty>([.. items])
{
	public DataContainer() : this([]) { }

	public DataContainer WithProperty(Action<DataContainerPropertyBuilder> creator)
	{
		var builder = new DataContainerPropertyBuilder();
		creator(builder);
		Add(builder.Build());
		return this;
	}

	public DataContainerProperty? GetProperty(string name)
	{
		return this.FirstOrDefault(x => x.Name == name);
	}
}

