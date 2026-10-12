// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    // Walk authored content rather than template internals. Windows supplies
    // the peers and names for template spin buttons, selection and scrolling.
    private static IEnumerable<FrameworkElement> AuthoredElements(FrameworkElement root)
    {
        yield return root;
        IEnumerable<FrameworkElement> children=root switch{
            Panel panel=>panel.Children.OfType<FrameworkElement>(),
            Border border=>border.Child is FrameworkElement child?[child]:[],
            UserControl user=>user.Content is FrameworkElement child?[child]:[],
            ContentControl content=>content.Content is FrameworkElement child?[child]:[],
            _=>[]};
        foreach(var child in children)foreach(var item in AuthoredElements(child))yield return item;
    }
    private static void LabelNativeControls(FrameworkElement root)
    {
        foreach(var element in AuthoredElements(root))
        {
            if(AutomationProperties.GetName(element).Length>0)continue;
            object? label=element switch{AppBarButton button=>button.Label,ButtonBase button=>button.Content,ComboBox box=>box.Header,TextBox box=>box.Header,PasswordBox box=>box.Header,NumberBox box=>box.Header,Slider slider=>slider.Header,ToggleSwitch toggle=>toggle.Header,ListView list=>list.Header,_=>null};
            if(label is string text && !string.IsNullOrWhiteSpace(text))AutomationProperties.SetName(element,text);
        }
    }
    private async Task VerifyAccessibilityAsync()
    {
        int controls=0;var missing=new List<string>();
        foreach(var view in Rocker.Domain.WorkspaceViews.All)
        {
            Navigate(new(view.Id));await Task.Delay(20);
            FrameworkElement root=view.Id=="20"?NotationPanel:view.Id=="08"?VideoPanel:view.Id=="29"?DevicePanel:WorkspaceContent;
            LabelNativeControls(root);
            foreach(var element in AuthoredElements(root).Where(e=>e is ButtonBase or ComboBox or TextBox or PasswordBox or NumberBox or Slider or ToggleSwitch))
            {
                ++controls;
                bool visible=element.IsLoaded;
                for(DependencyObject? parent=element;parent is not null;parent=VisualTreeHelper.GetParent(parent))if(parent is FrameworkElement {Visibility:Visibility.Collapsed})visible=false;
                string declared=AutomationProperties.GetName(element);
                var peer=visible?FrameworkElementAutomationPeer.FromElement(element)??FrameworkElementAutomationPeer.CreatePeerForElement(element):null;
                // Collapsed actions do not expose a native peer until loaded;
                // keep checking their declared name for when they become available.
                if(string.IsNullOrWhiteSpace(declared) || visible && (peer is null || string.IsNullOrWhiteSpace(peer.GetName())))missing.Add(view.Id+":"+element.GetType().Name+":"+element.Name+":"+declared+":visible="+visible);
            }
        }
        LabelNativeControls(ShellRoot);var playPeer=FrameworkElementAutomationPeer.FromElement(StopButton)??FrameworkElementAutomationPeer.CreatePeerForElement(StopButton);var rangePeer=FrameworkElementAutomationPeer.FromElement(VolumeSlider)??FrameworkElementAutomationPeer.CreatePeerForElement(VolumeSlider);
        bool patterns=playPeer?.GetPattern(PatternInterface.Invoke) is not null && rangePeer?.GetPattern(PatternInterface.RangeValue) is not null;
        Navigate(new("01"));bool passed=controls>100 && missing.Count==0 && patterns;
        WriteShellDiagnostic("accessibility.json",new{result=passed?"passed":"failed",authoredControlsChecked=controls,unnamed=missing,standardInvokeAndRangePatterns=patterns,narratorManualVerified=false,keyboardManualVerified=false,physicalDpiVerified=false,systemHighContrastManualVerified=false});
        if(!passed)throw new InvalidOperationException("Native accessibility peers have missing names or patterns: "+string.Join(", ",missing.Take(8)));
    }
}
