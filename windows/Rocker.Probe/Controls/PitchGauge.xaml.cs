// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Automation;
namespace Rocker.Probe.Controls;
public sealed partial class PitchGauge : UserControl
{
    private double value=50;
    private bool hasReading;
    public double Value {get=>value;set{this.value=double.IsFinite(value)?Math.Clamp(value,0,100):50;UpdatePointer();}}
    public bool HasReading {get=>hasReading;set{hasReading=value;UpdatePointer();}}
    public PitchGauge()
    {
        InitializeComponent();
        for(int i=0;i<=20;++i)
        {
            double x=24+i*13.6;
            GaugeCanvas.Children.Add(new Line {X1=x,X2=x,Y1=i%5==0?32:42,Y2=57,Style=(Style)Application.Current.Resources["VintageInstrumentLineStyle"]});
            if(i%5!=0)continue;
            int cents=i*5-50;
            var label=new TextBlock{Text=cents.ToString("+0;-0;0"),FontSize=12,Width=40,TextAlignment=TextAlignment.Center,Style=(Style)Application.Current.Resources["VintageInstrumentTextStyle"]};
            Canvas.SetLeft(label,x-20);Canvas.SetTop(label,72);GaugeCanvas.Children.Add(label);
        }
        UpdatePointer();
    }
    private void UpdatePointer()
    {
        if(Pointer is null)return;
        double x=24+value*2.72;Pointer.X1=Pointer.X2=x;Canvas.SetLeft(Pivot,x-3);
        Pointer.Visibility=Pivot.Visibility=hasReading?Visibility.Visible:Visibility.Collapsed;
        AutomationProperties.SetHelpText(this,hasReading?$"{value-50:+0;-0;0} centů; střed je naladěný tón.":"Bez aktuálně měřeného tónu.");
    }
}
