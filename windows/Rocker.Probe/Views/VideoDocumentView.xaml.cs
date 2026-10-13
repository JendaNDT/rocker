// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml.Controls;
namespace Rocker.Probe.Views;
public sealed partial class VideoDocumentView : UserControl
{ public VideoDocumentView() { InitializeComponent(); }
internal Border VideoPanelControl => VideoPanel;
internal TextBox VideoUrlControl => VideoUrl;
internal StackPanel VideoButtonsControl => VideoButtons;
internal Button VideoLoadButtonControl => VideoLoadButton;
internal Button VideoPlayButtonControl => VideoPlayButton;
internal Button VideoPauseButtonControl => VideoPauseButton;
internal Button VideoStopButtonControl => VideoStopButton;
internal Button Action4Control => Action4;
internal ComboBox VideoRatesControl => VideoRates;
internal Slider VideoSeekControl => VideoSeek;
internal NumberBox VideoLoopAControl => VideoLoopA;
internal NumberBox VideoLoopBControl => VideoLoopB;
internal Button VideoMarkAControl => VideoMarkA;
internal Button VideoMarkBControl => VideoMarkB;
internal Button VideoApplyLoopControl => VideoApplyLoop;
internal CheckBox VideoLoopEnabledControl => VideoLoopEnabled;
internal TextBox VideoLoopLabelControl => VideoLoopLabel;
internal Button Action13Control => Action13;
internal ListView VideoSavedLoopsControl => VideoSavedLoops;
internal Button Action14Control => Action14;
internal Button Action15Control => Action15;
internal Button Action16Control => Action16;
internal Button Action17Control => Action17;
internal TextBlock VideoLoopStatusControl => VideoLoopStatus;
internal Button Action18Control => Action18;
internal TextBlock VideoPositionControl => VideoPosition;
internal WebView2 VideoViewControl => VideoView;
internal TextBlock VideoStatusControl => VideoStatus;
}
