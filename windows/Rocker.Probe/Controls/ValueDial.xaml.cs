// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Automation;
namespace Rocker.Probe.Controls;
public sealed partial class ValueDial : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label),typeof(string),typeof(ValueDial),new PropertyMetadata("Hodnota",Changed));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum),typeof(double),typeof(ValueDial),new PropertyMetadata(0d,Changed));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum),typeof(double),typeof(ValueDial),new PropertyMetadata(100d,Changed));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value),typeof(double),typeof(ValueDial),new PropertyMetadata(0d,Changed));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty,value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty,value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty,value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty,value); }
    public event EventHandler? ValueChanged;
    private bool initialized, updating;
    public ValueDial() { InitializeComponent(); initialized = true; BuildScale(); Update(); }
    private void BuildScale()
    {
        // Raised bakelite ridges remain decorative; Slider and NumberBox own input.
        for (int i = 0; i < 32; ++i)
        {
            double angle = i * Math.PI / 16;
            var ridge = new Microsoft.UI.Xaml.Shapes.Line { X1 = 52 + Math.Sin(angle) * 32, Y1 = 52 - Math.Cos(angle) * 32, X2 = 52 + Math.Sin(angle) * 35, Y2 = 52 - Math.Cos(angle) * 35, Style = (Style)Application.Current.Resources["VintageDialTickStyle"], Opacity = .65 };
            DialFace.Children.Add(ridge);
        }
        for (int i=0;i<=10;++i)
        {
            double angle=(-135+i*27)*Math.PI/180;
            var tick=new Microsoft.UI.Xaml.Shapes.Line {X1=52+Math.Sin(angle)*43,Y1=52-Math.Cos(angle)*43,X2=52+Math.Sin(angle)*48,Y2=52-Math.Cos(angle)*48,Style=(Style)Application.Current.Resources["VintageDialTickStyle"]};
            DialFace.Children.Add(tick);
        }
    }
    private static void Changed(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        var control = (ValueDial)owner; control.Update();
        if (args.Property == ValueProperty && control.initialized && !control.updating) control.ValueChanged?.Invoke(control,EventArgs.Empty);
    }
    private void Update()
    {
        if (!initialized || updating) return;
        updating = true;
        try
        {
            if (!double.IsFinite(Minimum)) Minimum = 0;
            if (!double.IsFinite(Maximum) || Maximum <= Minimum) Maximum = Minimum + 1;
            double value = double.IsFinite(Value) ? Math.Clamp(Value,Minimum,Maximum) : Minimum;
            if (value != Value) Value = value;
            // Raise the maximum first when the whole range moves upward.
            if (Maximum > Number.Maximum) Number.Maximum = Maximum;
            Number.Minimum = Minimum; Number.Maximum = Maximum;
            if (Maximum > Position.Maximum) Position.Maximum = Maximum;
            Position.Minimum = Minimum; Position.Maximum = Maximum;
            Number.Value = Position.Value = value;
            ParameterLabel.Text = Label;
            AutomationProperties.SetName(Number,Label + ", přesná hodnota"); AutomationProperties.SetName(Position,Label);
            double angle = (-135 + 270 * (value - Minimum) / (Maximum - Minimum)) * Math.PI / 180;
            Needle.X1=52+Math.Sin(angle)*15; Needle.Y1=52-Math.Cos(angle)*15;
            Needle.X2=52+Math.Sin(angle)*29; Needle.Y2=52-Math.Cos(angle)*29;
        }
        finally { updating = false; }
    }
    private void Number_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!updating) Value = double.IsFinite(args.NewValue) ? args.NewValue : Minimum; }
    private void Position_Changed(object sender, RangeBaseValueChangedEventArgs args) { if (!updating) Value = args.NewValue; }
}
