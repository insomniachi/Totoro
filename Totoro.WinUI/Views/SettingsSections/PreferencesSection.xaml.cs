// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views.SettingsSections;

public sealed partial class PreferencesSection : Page
{
    private CompositeDisposable _disposables = [];

    public SettingsViewModel ViewModel
    {
        get { return (SettingsViewModel)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register("ViewModel", typeof(SettingsViewModel), typeof(PreferencesSection), new PropertyMetadata(null));


    public PreferencesSection()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = e.Parameter as SettingsViewModel;
        var settings = App.GetService<ISettings>().StartupOptions;

        settings
            .ObservableForProperty(x => x.MinimizeToTrayOnClose, x => x)
            .Subscribe(value => App.HandleClosedEvents = value);

        settings
            .ObservableForProperty(x => x.RunOnStartup, x => x)
            .Subscribe(value =>
            {
                if (value)
                {
                    Task.Run(AddToStartup);
                }
                else
                {
                    Task.Run(RemoveFromStartup);
                }
            });

    }

    private static void AddToStartup()
    {
        IShellLink link = (IShellLink)new ShellLink();
        var location = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, "exe");
        link.SetPath(location);
        link.SetWorkingDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

        // save it
        IPersistFile file = (IPersistFile)link;
        var startupApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\Startup");
        file.Save(Path.Combine(startupApps, "Totoro.lnk"), false);
    }

    private static void RemoveFromStartup()
    {
        var startupApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\Startup");
        var shortcut = Path.Combine(startupApps, "Totoro.lnk");

        if (File.Exists(shortcut))
        {
            File.Delete(shortcut);
        }
    }
}

[ComImport]
[Guid("00021401-0000-0000-C000-000000000046")]
internal class ShellLink
{
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214F9-0000-0000-C000-000000000046")]
internal interface IShellLink
{
    void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
    void GetIDList(out IntPtr ppidl);
    void SetIDList(IntPtr pidl);
    void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    void GetHotkey(out short pwHotkey);
    void SetHotkey(short wHotkey);
    void GetShowCmd(out int piShowCmd);
    void SetShowCmd(int iShowCmd);
    void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
    void Resolve(IntPtr hwnd, int fFlags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}
