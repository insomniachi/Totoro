using System;
using System.Collections.Generic;
using System.Linq;

namespace Totoro.Core.Navigation;

public interface IViewRegistry
{
	ViewMap? FindByView(Type viewType);
	ViewMap? FindByViewModel(Type vmType);
	ViewMap? FindByData(Type dataType);
	ViewMap? FindByKey(string key);
}

public class ViewRegistry(IEnumerable<ViewMap> map) : IViewRegistry
{
	public ViewMap? FindByView(Type viewType)
	{
		return map.FirstOrDefault(x => x.View == viewType);
	}

	public ViewMap? FindByViewModel(Type vmType)
	{
		return map.FirstOrDefault(x => x.ViewModel == vmType);
	}

	public ViewMap? FindByData(Type dataType)
	{
		return map.OfType<DataViewMap>().FirstOrDefault(x => x.Data == dataType);
	}

	public ViewMap? FindByKey(string key)
	{
		return map.OfType<KeyedViewMap>().FirstOrDefault(x => x.Key == key);
	}
}