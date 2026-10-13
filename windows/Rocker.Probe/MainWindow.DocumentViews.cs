// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.AppCore;
using Rocker.Probe.Views;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private ScoreDocumentView scoreDocumentView => documentContext.Current.Get("ScoreDocumentView", () => CreateScoreDocumentView(documentContext.Current));
    private FrameworkElement NotationPanel => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.NotationPanelControl : LegacyNotationPanel;
    private StackPanel ScoreButtons => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.ScoreButtonsControl : LegacyScoreButtons;
    private Button XmlButton => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.XmlButtonControl : LegacyXmlButton;
    private Button GpButton => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.GpButtonControl : LegacyGpButton;
    private Button OpenScoreButton => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.OpenScoreButtonControl : LegacyOpenScoreButton;
    private Button PlayScoreButton => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.PlayScoreButtonControl : LegacyPlayScoreButton;
    private StackPanel NotationControls => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.NotationControlsControl : LegacyNotationControls;
    private ComboBox NotationTrackBox => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.NotationTrackBoxControl : LegacyNotationTrackBox;
    private CheckBox NotationAllTracks => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.NotationAllTracksControl : LegacyNotationAllTracks;
    private ComboBox NotationZoom => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.NotationZoomControl : LegacyNotationZoom;
    private NumberBox NotationBar => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.NotationBarControl : LegacyNotationBar;
    private CheckBox NotationFollow => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.NotationFollowControl : LegacyNotationFollow;
    private NumberBox NotationLoopFirst => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.NotationLoopFirstControl : LegacyNotationLoopFirst;
    private NumberBox NotationLoopLast => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.NotationLoopLastControl : LegacyNotationLoopLast;
    private ComboBox NotationOccurrence => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.NotationOccurrenceControl : LegacyNotationOccurrence;
    private TextBlock NotationAccessible => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.NotationAccessibleControl : LegacyNotationAccessible;
    private TextBlock NotationStatus => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.NotationStatusControl : LegacyNotationStatus;
    private Canvas ScoreCanvas => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.ScoreCanvasControl : LegacyScoreCanvas;
    private Image ScoreImage => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.ScoreImageControl : LegacyScoreImage;
    private Border NotationPlayhead => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.NotationPlayheadControl : LegacyNotationPlayhead;
    private Border SelectionBox => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.SelectionBoxControl : LegacySelectionBox;
    private TextBlock SelectionStatus => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.SelectionStatusControl : LegacySelectionStatus;
    private Button ExportMidiButton => desktopEnabled && CurrentTarget.ViewId=="20" ? scoreDocumentView.ExportMidiButtonControl : LegacyExportMidiButton;
    private ScoreDocumentView CreateScoreDocumentView(DocumentSession session)
    {
        var view = new ScoreDocumentView();
        view.XmlButtonControl.Click += (sender,args) => { using var scope=documentContext.Enter(session); Xml_Click(sender,args); };
        view.GpButtonControl.Click += (sender,args) => { using var scope=documentContext.Enter(session); Gp_Click(sender,args); };
        view.OpenScoreButtonControl.Click += (sender,args) => { using var scope=documentContext.Enter(session); OpenScore_Click(sender,args); };
        view.PlayScoreButtonControl.Click += (sender,args) => { using var scope=documentContext.Enter(session); PlayScore_Click(sender,args); };
        view.NotationTrackBoxControl.SelectionChanged += (sender,args) => { using var scope=documentContext.Enter(session); NotationTrack_Changed(sender,args); };
        view.NotationAllTracksControl.Checked += (sender,args) => { using var scope=documentContext.Enter(session); NotationAudio_Changed(sender,args); };
        view.NotationAllTracksControl.Unchecked += (sender,args) => { using var scope=documentContext.Enter(session); NotationAudio_Changed(sender,args); };
        view.NotationZoomControl.SelectionChanged += (sender,args) => { using var scope=documentContext.Enter(session); NotationZoom_Changed(sender,args); };
        view.NotationBarControl.ValueChanged += (sender,args) => { using var scope=documentContext.Enter(session); NotationBar_Changed(sender,args); };
        view.NotationLoopFirstControl.ValueChanged += (sender,args) => { using var scope=documentContext.Enter(session); NotationLoopFirst_Changed(sender,args); };
        view.Action10Control.Click += (sender,args) => { using var scope=documentContext.Enter(session); NotationLoop_Click(sender,args); };
        view.Action11Control.Click += (sender,args) => { using var scope=documentContext.Enter(session); NotationLoopOff_Click(sender,args); };
        view.Action12Control.Click += (sender,args) => { using var scope=documentContext.Enter(session); NotationSeek_Click(sender,args); };
        view.ScoreCanvasControl.PointerPressed += (sender,args) => { using var scope=documentContext.Enter(session); Score_PointerPressed(sender,args); };
        view.ExportMidiButtonControl.Click += (sender,args) => { using var scope=documentContext.Enter(session); ExportMidi_Click(sender,args); };
        return view;
    }
    private VideoDocumentView videoDocumentView => documentContext.Current.Get("VideoDocumentView", () => CreateVideoDocumentView(documentContext.Current));
    private FrameworkElement VideoPanel => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoPanelControl : LegacyVideoPanel;
    private TextBox VideoUrl => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoUrlControl : LegacyVideoUrl;
    private StackPanel VideoButtons => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoButtonsControl : LegacyVideoButtons;
    private Button VideoLoadButton => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoLoadButtonControl : LegacyVideoLoadButton;
    private Button VideoPlayButton => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoPlayButtonControl : LegacyVideoPlayButton;
    private Button VideoPauseButton => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoPauseButtonControl : LegacyVideoPauseButton;
    private Button VideoStopButton => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoStopButtonControl : LegacyVideoStopButton;
    private ComboBox VideoRates => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoRatesControl : LegacyVideoRates;
    private Slider VideoSeek => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoSeekControl : LegacyVideoSeek;
    private NumberBox VideoLoopA => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoLoopAControl : LegacyVideoLoopA;
    private NumberBox VideoLoopB => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoLoopBControl : LegacyVideoLoopB;
    private Button VideoMarkA => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoMarkAControl : LegacyVideoMarkA;
    private Button VideoMarkB => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoMarkBControl : LegacyVideoMarkB;
    private Button VideoApplyLoop => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoApplyLoopControl : LegacyVideoApplyLoop;
    private CheckBox VideoLoopEnabled => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoLoopEnabledControl : LegacyVideoLoopEnabled;
    private TextBox VideoLoopLabel => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoLoopLabelControl : LegacyVideoLoopLabel;
    private ListView VideoSavedLoops => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoSavedLoopsControl : LegacyVideoSavedLoops;
    private TextBlock VideoLoopStatus => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoLoopStatusControl : LegacyVideoLoopStatus;
    private TextBlock VideoPosition => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoPositionControl : LegacyVideoPosition;
    private WebView2 VideoView => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoViewControl : LegacyVideoView;
    private TextBlock VideoStatus => desktopEnabled && CurrentTarget.ViewId=="08" ? videoDocumentView.VideoStatusControl : LegacyVideoStatus;
    private VideoDocumentView CreateVideoDocumentView(DocumentSession session)
    {
        var view = new VideoDocumentView();
        view.VideoLoadButtonControl.Click += (sender,args) => { using var scope=documentContext.Enter(session); VideoLoad_Click(sender,args); };
        view.VideoPlayButtonControl.Click += (sender,args) => { using var scope=documentContext.Enter(session); VideoPlay_Click(sender,args); };
        view.VideoPauseButtonControl.Click += (sender,args) => { using var scope=documentContext.Enter(session); VideoPause_Click(sender,args); };
        view.VideoStopButtonControl.Click += (sender,args) => { using var scope=documentContext.Enter(session); VideoStop_Click(sender,args); };
        view.Action4Control.Click += (sender,args) => { using var scope=documentContext.Enter(session); VideoExternal_Click(sender,args); };
        view.VideoRatesControl.SelectionChanged += (sender,args) => { using var scope=documentContext.Enter(session); VideoRate_Changed(sender,args); };
        view.VideoSeekControl.PointerReleased += (sender,args) => { using var scope=documentContext.Enter(session); VideoSeek_Released(sender,args); };
        view.VideoSeekControl.KeyUp += (sender,args) => { using var scope=documentContext.Enter(session); VideoSeek_KeyUp(sender,args); };
        view.VideoMarkAControl.Click += (sender,args) => { using var scope=documentContext.Enter(session); VideoMark_Click(sender,args); };
        view.VideoMarkBControl.Click += (sender,args) => { using var scope=documentContext.Enter(session); VideoMark_Click(sender,args); };
        view.VideoApplyLoopControl.Click += (sender,args) => { using var scope=documentContext.Enter(session); VideoApplyLoop_Click(sender,args); };
        view.VideoLoopEnabledControl.Checked += (sender,args) => { using var scope=documentContext.Enter(session); VideoLoop_Changed(sender,args); };
        view.VideoLoopEnabledControl.Unchecked += (sender,args) => { using var scope=documentContext.Enter(session); VideoLoop_Changed(sender,args); };
        view.Action13Control.Click += (sender,args) => { using var scope=documentContext.Enter(session); VideoSaveLoop_Click(sender,args); };
        view.Action14Control.Click += (sender,args) => { using var scope=documentContext.Enter(session); VideoOpenLoop_Click(sender,args); };
        view.Action15Control.Click += (sender,args) => { using var scope=documentContext.Enter(session); VideoRenameLoop_Click(sender,args); };
        view.Action16Control.Click += (sender,args) => { using var scope=documentContext.Enter(session); VideoDeleteLoop_Click(sender,args); };
        view.Action17Control.Click += (sender,args) => { using var scope=documentContext.Enter(session); VideoUndoLoop_Click(sender,args); };
        view.Action18Control.Click += (sender,args) => { using var scope=documentContext.Enter(session); VideoSharedAudio_Click(sender,args); };
        view.VideoSeekControl.AddHandler(UIElement.PointerPressedEvent,new Microsoft.UI.Xaml.Input.PointerEventHandler((sender,args)=>{using var scope=documentContext.Enter(session);if(args.GetCurrentPoint(view.VideoSeekControl).Properties.IsLeftButtonPressed)videoSeeking=true;}),true);
        view.VideoSeekControl.AddHandler(UIElement.PointerCaptureLostEvent,new Microsoft.UI.Xaml.Input.PointerEventHandler((sender,args)=>{using var scope=documentContext.Enter(session);VideoSeek_Released(sender,args);}),true);
        return view;
    }
}
