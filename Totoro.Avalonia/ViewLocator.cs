using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Totoro.Avalonia.ViewModels;

namespace Totoro.Avalonia
{
    public class ViewLocator : IDataTemplate
    {
        public Control Build(object? data)
        {
            if (data is null)
            {
                return new TextBlock { Text = "Not Found: " };
            }
            
            var name = data.GetType().FullName!.Replace("ViewModel", "View");
            var type = Type.GetType(name);

            if (type is null)
            {
                return new TextBlock { Text = "Not Found: " + name };
            }

            return (Control)Activator.CreateInstance(type)!;
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}