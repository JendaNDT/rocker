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
        panel.Children.Add(Text("Klíč chrání Windows pro tento uživatelský účet. Nezahrnuje se do projektů ani záloh. AI se připojí až při dokončení tutora.",true));
        var password=new PasswordBox { Header="Nový klíč",MaxLength=4096,PasswordRevealMode=PasswordRevealMode.Hidden,MinWidth=240,HorizontalAlignment=HorizontalAlignment.Stretch };
        var state=Text("Zjišťuji stav…",true);var save=new Button { Content="Uložit klíč" };var remove=new Button { Content="Odstranit klíč" };
        var store=new SecretStore(userFiles);
        async Task Refresh()
        {
            var access=await store.ReadAsync();
            if(closed)return;
            state.Text=access.State switch { SecretState.Ready=>"Klíč je bezpečně uložený.",SecretState.Missing=>"Klíč není uložený.",_=>"Klíč nelze zpřístupnit. Zadej jej znovu nebo ho odstraň." };
            remove.IsEnabled=access.State is SecretState.Ready or SecretState.Unavailable;
        }
        save.Click+=async (_,_)=>
        {
            secretOperationRunning=true;save.IsEnabled=remove.IsEnabled=false;
            try { await store.SetAsync(password.Password);password.Password="";await Refresh(); }
            catch(Exception) { state.Text="Klíč se nepodařilo uložit. Zkontroluj jeho zadání a přístup do složky dat."; }
            finally { secretOperationRunning=false;if(!closed)save.IsEnabled=true; }
        };
        remove.Click+=async (_,_)=>
        {
            secretOperationRunning=true;save.IsEnabled=remove.IsEnabled=false;
            try { await store.RemoveAsync();password.Password="";await Refresh(); }
            catch(Exception) { state.Text="Klíč se nepodařilo odstranit. Zkontroluj přístup do složky dat."; }
            finally { secretOperationRunning=false;if(!closed)save.IsEnabled=true; }
        };
        panel.Children.Add(password);panel.Children.Add(save);panel.Children.Add(remove);panel.Children.Add(state);
        _=Refresh();return panel;
    }
}
