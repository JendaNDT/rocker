// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private FrameworkElement BuildFirstRun()
    {
        var panel=Stack();int step=appSettings.SetupStep;string[] titles=["Vítej v Rockeru","Převod původních dat","Zvuková karta a kanál","Ověření signálu","Poslech a monitoring","Připraveno ke cvičení"];
        panel.Children.Add(Text(titles[step]));panel.Children.Add(Text($"Krok {step+1} z {titles.Length} · průvodce lze kdykoliv zopakovat",true));panel.Children.Add(new ProgressBar{Minimum=0,Maximum=titles.Length,Value=step+1});
        switch(step)
        {
            case 0:panel.Children.Add(Text("Rocker je místní kytarový pomocník. Začít můžeš bez kytary, zvukovky, internetu i AI účtu. Zvuk ani mikrofon se samy nezapnou."));AddSettingsLink(panel,"Nastavit profil kytary","26");break;
            case 1:panel.Children.Add(Text("Máš data ze starého Rockeru? Přenes nastavení, pokrok a uložené nahrávky z exportu. Původní webová data zůstanou zachovaná. Import je volitelný."));AddSettingsLink(panel,"Zálohy a převod dat","30");break;
            case 2:panel.Children.Add(Text("Pro M-Track Solo připoj kytaru do vstupu s přepínačem Instrument a sluchátka do stejného interfejsu. Vyber skutečný kanál; druhý se automaticky nepřimíchá. ASIO vyžaduje ovladač výrobce, Windows Audio nabízí alternativu."));AddSettingsLink(panel,"Vybrat zařízení a kanál","29");panel.Children.Add(Text(appSettings.AudioDevice is { } device?"Uloženo: "+device.Backend+" · "+device.Output:"Žádné zařízení není vybrané. Můžeš pokračovat s offline lekcemi.",true));break;
            case 3:panel.Children.Add(Text("V Live výslovně spusť vstup a zkontroluj úroveň kytary. Nastav gain na kartě tak, aby silnější hra nepřebuzovala vstup. Bez kytary tento krok přeskoč; ukázkový tón není měřený vstup."));AddSettingsLink(panel,"Live · úroveň a ladička","02");var demo=new Button{Content="Spustit ukázkový tón A · 440 Hz (bez kytary)"};demo.Click+=Start_Click;panel.Children.Add(demo);var stop=new Button{Content="Zastavit ukázku"};stop.Click+=Stop_Click;panel.Children.Add(stop);break;
            case 4:panel.Children.Add(Text("Začni nízkou hlasitostí sluchátek. Pro aparát zapni softwarový monitoring výslovně v Live. Přímý Direct Monitor na kartě je cesta bez efektů; současný poslech obou může vytvořit dvojí zvuk. Odezvu ověří až hra na skutečné kartě."));AddSettingsLink(panel,"Ovládat monitoring a aparát","02");break;
            case 5:panel.Children.Add(Text("Data se průběžně ukládají místně. Denní plán, lekce, aparát a nahrávky fungují bez AI klíče. Zahájit cvičení ve spodním panelu počítá aktivitu; samotné otevření stránky nepřidá minuty. YouTube a AI vyžadují síť pouze při použití."));AddSettingsLink(panel,"Nastavení a složka dat","28");break;
        }
        var previous=new Button{Content="Předchozí krok",IsEnabled=step>0};previous.Click+=(_,_)=>SetSetupStep(step-1);panel.Children.Add(previous);var next=new Button{Content=step==5?"Dokončit průvodce a otevřít přehled":"Pokračovat (tento krok lze přeskočit)",IsEnabled=persistenceReady};next.Click+=(_,_)=>{if(step<5)SetSetupStep(step+1);else{appSettings=appSettings with{Onboarded=true,SetupStep=0};SaveSettings();Navigate(new("01"));}};panel.Children.Add(next);var later=new Button{Content="Dokončit později"};later.Click+=(_,_)=>Navigate(new("01"));panel.Children.Add(later);return Card(panel);
    }
    private void SetSetupStep(int step){appSettings=appSettings with{SetupStep=Math.Clamp(step,0,5)};SaveSettings();workspaceCache.Remove(new("31"));ShowWorkspace();}
    private async Task VerifyFirstRunAsync()
    {
        var previous=appSettings;bool complete=true;for(int step=0;step<=5;++step){appSettings=appSettings with{SetupStep=step,Onboarded=false};workspaceCache.Remove(new("31"));Navigate(new("31"));complete&=WorkspaceContent.Content is FrameworkElement;}
        appSettings=appSettings with{Onboarded=true,SetupStep=0};SaveSettings();await autosave!.FlushAsync();var snapshot=await (await Rocker.Infrastructure.DataRepository.OpenAsync(userFiles)).SnapshotAsync();bool persisted=UserData.Read<AppSettings>(snapshot.Documents.Single(d=>d.Kind=="settings").Value).Onboarded;appSettings=previous;SaveSettings();await autosave.FlushAsync();workspaceCache.Remove(new("31"));Navigate(new("28"));bool settings=WorkspaceContent.Content is FrameworkElement;Navigate(new("01"));WriteShellDiagnostic("first-run.json",new{result=complete && persisted && settings?"passed":"failed",steps=6,completedWithoutDevice=true,completedWithoutAiKey=true,persistenceReopened=persisted,realSoundTested=false});if(!complete || !persisted || !settings)throw new InvalidOperationException("First-run/settings native check failed.");
    }
}
