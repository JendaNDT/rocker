// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Interop;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private List<double> palmOpen { get => documentContext.Current.Get<List<double>>("palmOpen", () => []); }
    private List<double> palmMuted { get => documentContext.Current.Get<List<double>>("palmMuted", () => []); }
    private string? palmCollect { get => documentContext.Current.Get<string?>("palmCollect", () => default); set => documentContext.Current.Set("palmCollect",value); }private string palmPickup { get => documentContext.Current.Get<string>("palmPickup", () => "bridge"); set => documentContext.Current.Set("palmPickup",value); }
    private ulong palmLastHit { get => documentContext.Current.Get<ulong>("palmLastHit", () => default); set => documentContext.Current.Set("palmLastHit",value); }
    private ulong palmStartFrame { get => documentContext.Current.Get<ulong>("palmStartFrame", () => default); set => documentContext.Current.Set("palmStartFrame",value); }private uint palmEpoch { get => documentContext.Current.Get<uint>("palmEpoch", () => default); set => documentContext.Current.Set("palmEpoch",value); }private bool palmListening { get => documentContext.Current.Get<bool>("palmListening", () => default); set => documentContext.Current.Set("palmListening",value); }
    private int palmGeneration { get => documentContext.Current.Get<int>("palmGeneration", () => default); set => documentContext.Current.Set("palmGeneration",value); }private bool palmOperation { get => documentContext.Current.Get<bool>("palmOperation", () => default); set => documentContext.Current.Set("palmOperation",value); }private string? palmSampleContext { get => documentContext.Current.Get<string?>("palmSampleContext", () => default); set => documentContext.Current.Set("palmSampleContext",value); }private string palmLastReading { get => documentContext.Current.Get<string>("palmLastReading", () => ""); set => documentContext.Current.Set("palmLastReading",value); }
    private PalmMuteState? palmState { get => documentContext.Current.Get<PalmMuteState?>("palmState", () => default); set => documentContext.Current.Set("palmState",value); }private TextBlock? palmReading { get => documentContext.Current.Get<TextBlock?>("palmReading", () => default); set => documentContext.Current.Set("palmReading",value); }
    private TextBlock? palmSummary { get => documentContext.Current.Get<TextBlock?>("palmSummary", () => default); set => documentContext.Current.Set("palmSummary",value); }private ListView? palmResults { get => documentContext.Current.Get<ListView?>("palmResults", () => default); set => documentContext.Current.Set("palmResults",value); }
    private void ResetPalmRuntime(){++palmGeneration;palmSampleContext=null;palmOperation=false;palmLastReading="";palmOpen.Clear();palmMuted.Clear();palmCollect=null;palmListening=false;palmState=null;palmReading=null;palmSummary=null;palmResults=null;}
    private bool PalmProfileMatches()=>appSettings.PalmMute is { } profile && profile.Device==PalmDeviceFingerprint() && profile.Tuning==appSettings.TuningId && profile.Instrument==InstrumentFingerprint() && profile.Pickup==palmPickup;
    private FrameworkElement BuildPalmMute(string? itemId=null)
    {var capturedDocument = documentContext.Current;
        var panel=Stack();panel.Children.Add(Text("Palm mute · kalibrace a skutečné úhozy"));
        var pickup=new TextBox{Header="Snímač / poloha přepínače pro tuto kalibraci",Text=palmPickup,MaxLength=64};pickup.TextChanged+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;palmPickup=pickup.Text.Trim();CancelPalm();UpdatePalm();};panel.Children.Add(pickup);
        palmReading=Text("Kalibruj alespoň tři otevřené a tři tlumené úhozy.",true);panel.Children.Add(palmReading);palmSummary=Text("",true);panel.Children.Add(palmSummary);
        async Task Collect(string technique)
        {
            if(palmOperation || audio is null)return;palmOperation=true;var generation=++palmGeneration;
            try{await ClaimDesktopGraderAsync();var value=await PreparePalmAsync();if(generation!=palmGeneration || CurrentTarget.ViewId!="12")return;var context=PalmContext();if(palmSampleContext!=context){palmOpen.Clear();palmMuted.Clear();palmSampleContext=context;}BaselinePalm(value);palmCollect=technique;palmListening=false;(technique=="open"?palmOpen:palmMuted).Clear();UpdatePalm();}catch(Exception error){ShowError(error);}finally{palmOperation=false;}
        }
        var open=new Button{Content="Nasbírat otevřené úhozy"};open.Click+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;await Collect("open");};panel.Children.Add(open);var muted=new Button{Content="Nasbírat tlumené úhozy"};muted.Click+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;await Collect("muted");};panel.Children.Add(muted);
        var save=new Button{Content="Vypočítat a uložit kalibraci"};save.Click+=(_,_)=>
        {using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;
            if(palmSampleContext!=PalmContext()){ShowPersistenceError("Nastavení se změnilo. Nasbírej oba typy úhozů znovu.");return;}var calibration=PalmMuteRules.Calibrate(palmOpen,palmMuted);if(calibration is null){ShowPersistenceError("Potřebuješ alespoň tři úhozy každého typu. Otevřené úhozy musí mít vyšší průměrný jas než tlumené.");return;}
            if(string.IsNullOrWhiteSpace(palmPickup)){ShowPersistenceError("Vyplň použitou polohu snímače.");return;}appSettings=appSettings with{PalmMute=new(PalmDeviceFingerprint(),appSettings.TuningId,InstrumentFingerprint(),calibration,1,palmPickup)};palmCollect=null;SaveSettings();UpdatePalm();
            if(palmOpen.Min()<=palmMuted.Max()){ShellNotice.IsOpen=true;ShellNotice.Severity=InfoBarSeverity.Warning;ShellNotice.Message="Vzorky kalibrace se překrývají. Rozpoznání může být nejisté; zkontroluj úhozy a případně kalibruj znovu.";}
        };panel.Children.Add(save);
        var sensitivity=new Slider{Header="Citlivost hranice (%)",Minimum=50,Maximum=150,Value=(appSettings.PalmMute?.Sensitivity??1)*100};sensitivity.ValueChanged+=(_,e)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;if(appSettings.PalmMute is not { } profile)return;appSettings=appSettings with{PalmMute=profile with{Sensitivity=e.NewValue/100}};SaveSettings();UpdatePalm();};panel.Children.Add(sensitivity);
        var drills=catalog!.Items("palmMuteDrills").Select(UserData.Read<PalmMuteDrill>).ToArray();var choose=new ComboBox{Header="Cvičení",ItemsSource=drills,DisplayMemberPath="Name"};choose.SelectionChanged+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;if(choose.SelectedItem is PalmMuteDrill drill){CancelPalm();palmState=PalmMuteRules.Start(drill);UpdatePalm();}};panel.Children.Add(choose);
        var start=new Button{Content="Začít hodnocení úhozů"};start.Click+=async(_,_)=>
        {using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;
            if(palmOperation || audio is null)return;palmOperation=true;var generation=++palmGeneration;
            try{if(!PalmProfileMatches())throw new InvalidOperationException("Kalibrace neodpovídá vstupu, ladění, profilu nebo snímači. Překalibruj aktuální nastavení.");if(palmState is null)return;var drill=palmState.Drill;await ClaimDesktopGraderAsync();var value=await PreparePalmAsync();if(generation!=palmGeneration || CurrentTarget.ViewId!="12" || palmState?.Drill.Id!=drill.Id)return;palmState=PalmMuteRules.Start(drill);BaselinePalm(value);palmCollect=null;palmListening=true;UpdatePalm();}catch(Exception error){ShowError(error);}finally{palmOperation=false;}
        };panel.Children.Add(start);var stop=new Button{Content="Pozastavit měření"};stop.Click+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;CancelPalm();UpdatePalm();};panel.Children.Add(stop);
        palmResults=new ListView{Header="Očekávané a měřené úhozy"};panel.Children.Add(palmResults);panel.Children.Add(Text("Klasifikace používá jas těla úhozu ze skutečného vstupu. Odstup od hranice je orientační pomůcka, nikoli statistická pravděpodobnost. Po změně kytary, snímače nebo gainu překalibruj. Měření úhozů má minimální rozestup přibližně 320 ms; rychlé pasáže posuzuj při poslechu.",true));panel.Children.Add(BuildRecordingControls());choose.SelectedItem=drills.FirstOrDefault(d=>d.Id==itemId)??drills[0];UpdatePalm();return Card(panel);
    }
    private void UpdatePalm()
    {var capturedDocument = documentContext.Current;
        if(palmReading is null)return;var profile=appSettings.PalmMute;palmReading.Text=palmCollect is { } collect?$"Kalibrace {(collect=="open"?"otevřených":"tlumených")} úhozů · otevřené {palmOpen.Count}, tlumené {palmMuted.Count}":palmListening?"Poslouchám nové úhozy ze vstupu.":"Měření je pozastavené.";palmReading.Text+=" "+palmLastReading;
        palmSummary!.Text=profile is null?"Zatím není uložená kalibrace.":$"Jas: otevřené {profile.Calibration.OpenMean:0} Hz · tlumené {profile.Calibration.MutedMean:0} Hz · hranice {profile.Calibration.Threshold*profile.Sensitivity:0} Hz · "+(PalmProfileMatches()?"Profil odpovídá aktuálnímu nastavení.":"Nastavení se změnilo; překalibruj.");
        if(palmState is { } state && palmResults is not null)palmResults.ItemsSource=state.Drill.Pattern.Select((expected,i)=>$"{i+1}. {(expected=="muted"?"Tlumený":"Otevřený")} · "+(i<state.Hits.Count?(state.Hits[i]==expected?"správně":"jiný typ úhozu"):"čeká")).ToArray();
    }
    private void CancelPalm(){++palmGeneration;palmListening=false;palmCollect=null;}
    private string PalmContext()=>PalmDeviceFingerprint()+"/"+appSettings.TuningId+"/"+InstrumentFingerprint()+"/"+palmPickup;
    private async Task<AnalysisSnapshot> PreparePalmAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(audio is null || closingWindow || deviceOperationRunning || backupOperationRunning || deviceSelectionUnavailable || appSettings.AudioDevice is not{InputChannel:>=0})throw new InvalidOperationException("Vyber vstup kytary.");
        await audio.StartLiveAsync();var value=audio.GetAnalysis();for(int wait=0;wait<50 && (!value.ActiveInput || value.Stale || value.Epoch!=audio.GetClock().Epoch);++wait){await Task.Delay(20);value=audio.GetAnalysis();}
        if(!value.ActiveInput || value.Stale || value.Epoch!=audio.GetClock().Epoch)throw new InvalidOperationException("Vstup zatím nedodává aktuální měření. Zkus to znovu.");return value;
    }
    private void BaselinePalm(AnalysisSnapshot value){palmEpoch=value.Epoch;palmLastHit=value.HitCount;palmStartFrame=value.Frame;palmLastReading="";}
    private void RefreshPalmMute(AnalysisSnapshot value)
    {
        if(!palmListening && palmCollect is null)return;
        if(!value.ActiveInput || value.Stale || value.Epoch!=palmEpoch || value.HitCount<palmLastHit){CancelPalm();palmLastReading="Vstup byl přerušen; spusť měření znovu.";UpdatePalm();return;}
        if(palmCollect is not null && palmSampleContext!=PalmContext() || palmListening && !PalmProfileMatches()){CancelPalm();palmLastReading="Nastavení se změnilo; překalibruj.";UpdatePalm();return;}
        if(value.HitCount<=palmLastHit || value.OnsetFrame<=palmStartFrame || value.Frame<value.OnsetFrame || (value.Frame-value.OnsetFrame)/value.SampleRate<.17 || !double.IsFinite(value.HitCentroid) || value.HitCentroid<=0)return;
        bool skipped=value.HitCount>palmLastHit+1;palmLastHit=value.HitCount;palmLastReading=$"Poslední úhoz: {value.HitCentroid:0} Hz.";
        if(palmCollect is { } kind){var samples=kind=="open"?palmOpen:palmMuted;if(samples.Count<64)samples.Add(value.HitCentroid);if(samples.Count==64)palmCollect=null;}
        else if(palmState is { } state && appSettings.PalmMute is { } profile)
        {
            if(skipped){CancelPalm();palmLastReading="Některé úhozy nebylo možné spolehlivě sledovat. Zopakuj pokus pomaleji.";UpdatePalm();return;}
            var calibration=profile.Calibration with{Threshold=profile.Calibration.Threshold*profile.Sensitivity};var technique=PalmMuteRules.ClassifyHit(value.HitCentroid,calibration);palmState=PalmMuteRules.LogHit(state,technique);
            var margin=Math.Abs(value.HitCentroid-calibration.Threshold)/Math.Max(1,profile.Calibration.OpenMean-profile.Calibration.MutedMean);palmLastReading+=$" Odstup od hranice: {margin:0.00} rozsahu kalibrace.";
            if(PalmMuteRules.IsComplete(palmState)){palmListening=false;palmLastReading+=$" Hotovo: {PalmMuteRules.Score(palmState)} / {palmState.Drill.Pattern.Count}.";}
        }
        UpdatePalm();
    }
    private async Task VerifyRhythmAndPalmAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        Navigate(new("10"));Navigate(new("11"));bool controls=rhythmEditors.ContainsKey("10") && rhythmEditors.ContainsKey("11") && rhythmDiagrams.Count==2 && rhythmRoundBars.ContainsKey("10") && rhythmRoundBars.ContainsKey("11");
        string exercise=catalog!.Items("powerChordExercises").First().GetProperty("id").GetString()!;RecordTempo(exercise,123);RecordTempo(exercise,100);Navigate(new("12"));
        var calibration=PalmMuteRules.Calibrate(new double[]{2400,2300,2500},new double[]{600,550,650})!;appSettings=appSettings with{PalmMute=new(PalmDeviceFingerprint(),appSettings.TuningId,InstrumentFingerprint(),calibration,1,palmPickup)};SaveSettings();
        var drill=UserData.Read<PalmMuteDrill>(catalog.Items("palmMuteDrills").First());palmState=PalmMuteRules.Start(drill);BaselinePalm(new AnalysisSnapshot{Epoch=88});palmListening=true;
        ulong count=0;foreach(string technique in drill.Pattern){++count;RefreshPalmMute(new AnalysisSnapshot{Epoch=88,Flags=4,SampleRate=48000,HitCount=count,OnsetFrame=count*24000,Frame=count*24000+12000,HitCentroid=technique=="muted"?600:2400});}
        bool score=palmState is not null && PalmMuteRules.IsComplete(palmState) && PalmMuteRules.Score(palmState)==drill.Pattern.Count && !palmListening && palmResults?.Items.Count==drill.Pattern.Count;
        await autosave!.FlushAsync();var snapshot=await (await Rocker.Infrastructure.DataRepository.OpenAsync(userFiles)).SnapshotAsync();bool persisted=UserData.Read<AppSettings>(snapshot.Documents.Single(d=>d.Kind=="settings").Value).PalmMute==appSettings.PalmMute && UserData.Read<ProgressData>(snapshot.Documents.Single(d=>d.Kind=="progress").Value).BpmRecords[exercise]==123;
        bool pass=controls && score && persisted;WriteShellDiagnostic("rhythm-palm-views.json",new{result=pass?"passed":"failed",nativeTrainers=3,nativeDiagrams=rhythmDiagrams.Count,nativeControlsVerified=controls,roundBarKeys=rhythmRoundBars.Keys.Order().ToArray(),simulatedUiHits=drill.Pattern.Count,persistenceReopened=persisted,hardwareVerified=false,physicalPalmMuteVerified=false});Navigate(new("01"));if(!pass)throw new InvalidOperationException("Native rhythm/palm view check failed.");
    }

}
