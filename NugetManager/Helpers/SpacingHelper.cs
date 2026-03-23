using System.Windows;
using System.Windows.Controls;
using Orientation = System.Windows.Controls.Orientation;

namespace NugetManager.Helpers;

/// <summary>
/// Attached property that adds uniform spacing between StackPanel children.
/// </summary>
public static class SpacingHelper
{
    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.RegisterAttached(
            "Spacing", typeof(double), typeof(SpacingHelper),
            new PropertyMetadata(0.0, OnSpacingChanged));

    public static void SetSpacing(DependencyObject obj, double value) =>
        obj.SetValue(SpacingProperty, value);

    public static double GetSpacing(DependencyObject obj) =>
        (double)obj.GetValue(SpacingProperty);

    private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not StackPanel panel) return;
        var spacing = (double)e.NewValue;
        panel.Loaded -= Panel_Loaded;
        panel.Loaded += Panel_Loaded;
        ApplySpacing(panel, spacing);
    }

    private static void Panel_Loaded(object sender, RoutedEventArgs e) =>
        ApplySpacing((StackPanel)sender, GetSpacing((StackPanel)sender));

    private static void ApplySpacing(StackPanel panel, double spacing)
    {
        for (int i = 0; i < panel.Children.Count; i++)
        {
            if (panel.Children[i] is not FrameworkElement el) continue;
            var existing = el.Margin;
            if (panel.Orientation == Orientation.Vertical)
                el.Margin = new Thickness(existing.Left, existing.Top, existing.Right,
                    i < panel.Children.Count - 1 ? spacing : 0);
            else
                el.Margin = new Thickness(existing.Left, existing.Top,
                    i < panel.Children.Count - 1 ? spacing : 0, existing.Bottom);
        }
    }
}
