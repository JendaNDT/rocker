// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Infrastructure;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private bool secretOperationRunning;
    private FrameworkElement BuildSecretSettings()
    {
        var panel=Stack();panel.Children.Add(Text("AI klíč"));
        panel.Children.Add(Text("Klíč chrání Windows pro tento uživatelský účet. Nezahrnuje se do projektů ani záloh. Dotaz se odešle až tlačítkem v tutorovi. Každý poskytovatel má vlastní klíč; starý Gemini klíč se OpenAI neposílá.",true));
        var password=new PasswordBox { Header="Nový klíč",MaxLength=4096,PasswordRevealMode=PasswordRevealMode.Hidden,MinWidth=240,HorizontalAlignment=HorizontalAlignment.Stretch };
        var state=Text("Zjišťuji stav…",true);var save=new Button { Content="Uložit klíč" };var remove=new Button { Content="Odstranit klíč" };
        var provider=new ComboBox{Header="Klíč pro poskytovatele",ItemsSource=new[]{"OpenAI","Gemini — starý klíč"},SelectedIndex=aiSettings.Provider=="openai"?0:1};
        var store=new SecretStore(userFiles,provider.SelectedIndex==0?"openai":"gemini");int version=0;
        async Task Refresh()
        {
            int revision=++version;var access=await store.ReadAsync();
            if(closed || revision!=version)return;
            state.Text=access.State switch { SecretState.Ready=>"Klíč je bezpečně uložený.",SecretState.Missing=>"Klíč není uložený.",_=>"Klíč nelze zpřístupnit. Zadej jej znovu nebo ho odstraň." };
            remove.IsEnabled=access.State is SecretState.Ready or SecretState.Unavailable;
        }
        provider.SelectionChanged+=async(_,_)=>{if(secretOperationRunning)return;store=new SecretStore(userFiles,provider.SelectedIndex==0?"openai":"gemini");await Refresh();};
        save.Click+=async (_,_)=>
        {
            secretOperationRunning=true;provider.IsEnabled=save.IsEnabled=remove.IsEnabled=false;
            try { await store.SetAsync(password.Password);password.Password="";await Refresh(); }
            catch(Exception) { state.Text="Klíč se nepodařilo uložit. Zkontroluj jeho zadání a přístup do složky dat."; }
            finally { secretOperationRunning=false;if(!closed)provider.IsEnabled=save.IsEnabled=true; }
        };
        remove.Click+=async (_,_)=>
        {
            secretOperationRunning=true;provider.IsEnabled=save.IsEnabled=remove.IsEnabled=false;
            try { await store.RemoveAsync();password.Password="";await Refresh(); }
            catch(Exception) { state.Text="Klíč se nepodařilo odstranit. Zkontroluj přístup do složky dat."; }
            finally { secretOperationRunning=false;if(!closed)provider.IsEnabled=save.IsEnabled=true; }
        };
        panel.Children.Add(provider);panel.Children.Add(password);panel.Children.Add(save);panel.Children.Add(remove);panel.Children.Add(state);
        _=Refresh();return panel;
    }
}
