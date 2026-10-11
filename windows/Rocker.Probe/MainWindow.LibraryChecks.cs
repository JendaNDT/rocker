// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private void VerifyLibraryGames()
    {
        Navigate(new("17","F"));int diagrams=0;foreach(var chord in catalog!.Items("chords")){libraryChord!.ShowChord(chord);libraryChord.ShowChord(chord,true);++diagrams;}
        Navigate(new("18","minor-pentatonic"));var scale=catalog.Find("scales","minor-pentatonic")!.Value;libraryScale!.SetTuning(GameStandardTuning,scale.GetProperty("intervals").EnumerateArray().Select(n=>n.GetInt32()).ToArray(),0,true,12,SelectedNotation);libraryScale.SelectNote(0,15);bool board=libraryScale.CellsCount==78 && libraryScale.SelectedMidi==55 && libraryScale.SelectedPosition==(0,15);
        int tabs=0;Navigate(new("19","smoke-on-the-water"));foreach(var riff in catalog.Items("riffs").Select(UserData.Read<PracticeRiff>)){DrawRiffTab(riff);if(riffTabCells.Count!=riff.Notes.Count)throw new InvalidOperationException("Native riff tab lost a note.");++tabs;}bool alias=selectedRiff?.Id=="chromatic-pulse";
        Navigate(new("21"));fretStarted=true;fretTarget=4;fretScore=new();gameBoard!.SelectNote(0,0);bool correct=fretScore==new GameScore(1,1) && fretTarget!=4;fretTarget=6;gameBoard.SelectNote(0,0);bool wrong=fretScore==new GameScore(1,0) && fretTarget==6;
        Navigate(new("22"));int intervals=earAnswers.Count;earMode!.SelectedIndex=1;int chords=earAnswers.Count;earStarted=earHeard=true;earIndex=0;earScore=new();bool ear=AnswerEarGame(0,false) && earScore==new GameScore(1,1) && earIndex!=0 && !earHeard;
        bool pass=diagrams==26 && tabs==14 && libraryCounts["scales"]==6 && board && alias && correct && wrong && intervals==8 && chords==4 && ear;
        WriteShellDiagnostic("library-games-views.json",new{result=pass?"passed":"failed",nativeChordDiagrams=diagrams,nativeScales=libraryCounts["scales"],nativeRiffTabs=tabs,legacyAliasResolved=alias,selectedFret=15,boardCells=libraryScale.CellsCount,intervalChoices=intervals,chordChoices=chords,scoreRulesVerified=correct && wrong && ear,physicalKeyboardVerified=false,physicalAudioVerified=false});Navigate(new("01"));if(!pass)throw new InvalidOperationException("Native library/game controls failed.");
    }
}
