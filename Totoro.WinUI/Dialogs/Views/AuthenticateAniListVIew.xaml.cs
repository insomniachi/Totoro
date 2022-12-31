// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Totoro.WinUI.Dialogs.ViewModels;

namespace Totoro.WinUI.Dialogs.Views;

public class AuthenticateAniListViewBase : ReactivePage<AuthenticateAniListViewModel> { }

public sealed partial class AuthenticateAniListView : AuthenticateAniListViewBase
{
    public AuthenticateAniListView()
    {
        InitializeComponent();
    }
}
