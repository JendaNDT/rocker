// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Windowing;
using Windows.System;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private bool focusMode;
    private bool focusPaneWasOpen;
    private readonly List<FrameworkElement> focusSecondary = [];
    private void InitializeFocusCommands()
    {
        ShellRoot.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Escape && focusMode && !args.Handled)
            { SetFocusMode(false); args.Handled = true; }
        };
    }
    private void FocusMode_Click(object sender, RoutedEventArgs args)
    {
        if (closed || closingWindow || workspaceOperation) return;
        SetFocusMode(!focusMode);
    }
    private void SetFocusMode(bool enabled)
    {
        if (focusMode == enabled) return;
        if(desktopEnabled)
        {
            var session=OpenDesktop(new("02"));var tab=TabFor(session)!;DetachDesktopTab(tab);var host=floatingHosts[tab];
            host.AppWindow.SetPresenter(enabled?AppWindowPresenterKind.FullScreen:AppWindowPresenterKind.Overlapped);focusMode=enabled;RefreshFocusChrome();return;
        }
        if (enabled)
        {
            focusPaneWasOpen = Navigation.IsPaneOpen;
            Navigate(new("02"));
            focusMode = true;
        }
        else focusMode = false;
        RefreshFocusChrome();
        WorkspaceScroll.ChangeView(null, 0, null, true);
        if (enabled) FocusExitButton.Focus(FocusState.Programmatic);
    }
    private void RefreshFocusChrome()
    {
        RefreshWorkspaceChrome();
        Navigation.IsPaneVisible = !focusMode;
        Navigation.IsBackButtonVisible = focusMode ? NavigationViewBackButtonVisible.Collapsed : NavigationViewBackButtonVisible.Visible;
        if (focusMode) Navigation.IsPaneOpen = false;
        else if (Navigation.DisplayMode == NavigationViewDisplayMode.Expanded) Navigation.IsPaneOpen = focusPaneWasOpen || Navigation.IsPaneOpen;
        WorkspaceTitle.Text = focusMode ? "Jen hraju" : Rocker.Domain.WorkspaceViews.Get(CurrentTarget.ViewId).Title;
        WorkspaceTrail.Visibility = focusMode ? Visibility.Collapsed : Visibility.Visible;
        FavoriteViewButton.Visibility = SearchButton.Visibility = focusMode ? Visibility.Collapsed : Visibility.Visible;
        FocusExitButton.Visibility = focusMode ? Visibility.Visible : Visibility.Collapsed;
        foreach (var element in focusSecondary) element.Visibility = focusMode ? Visibility.Collapsed : Visibility.Visible;
        if (liveTunerNote is not null) liveTunerNote.FontSize = focusMode ? 56 : 40;
        if (liveTunerNeedle is not null) liveTunerNeedle.Height = focusMode ? 110 : 70;
        if (liveStart is not null) liveStart.MinHeight = focusMode ? 48 : 40;
        if (liveStop is not null) liveStop.MinHeight = focusMode ? 48 : 40;
        foreach(var button in recordingStarts.Concat(recordingStops).Where(b=>b.Tag as string=="compact-recording")) button.MinHeight=focusMode?48:40;
    }
}
