// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml.Controls;
namespace Rocker.Probe.Views;
public sealed partial class ScoreDocumentView : UserControl
{ public ScoreDocumentView() { InitializeComponent(); }
internal Grid NotationPanelControl => NotationPanel;
internal StackPanel ScoreButtonsControl => ScoreButtons;
internal Button XmlButtonControl => XmlButton;
internal Button GpButtonControl => GpButton;
internal Button OpenScoreButtonControl => OpenScoreButton;
internal Button PlayScoreButtonControl => PlayScoreButton;
internal StackPanel NotationControlsControl => NotationControls;
internal ComboBox NotationTrackBoxControl => NotationTrackBox;
internal CheckBox NotationAllTracksControl => NotationAllTracks;
internal ComboBox NotationZoomControl => NotationZoom;
internal NumberBox NotationBarControl => NotationBar;
internal CheckBox NotationFollowControl => NotationFollow;
internal NumberBox NotationLoopFirstControl => NotationLoopFirst;
internal NumberBox NotationLoopLastControl => NotationLoopLast;
internal ComboBox NotationOccurrenceControl => NotationOccurrence;
internal Button Action10Control => Action10;
internal Button Action11Control => Action11;
internal Button Action12Control => Action12;
internal TextBlock NotationAccessibleControl => NotationAccessible;
internal TextBlock NotationStatusControl => NotationStatus;
internal Canvas ScoreCanvasControl => ScoreCanvas;
internal Image ScoreImageControl => ScoreImage;
internal Border NotationPlayheadControl => NotationPlayhead;
internal Border SelectionBoxControl => SelectionBox;
internal TextBlock SelectionStatusControl => SelectionStatus;
internal Button ExportMidiButtonControl => ExportMidiButton;
}
