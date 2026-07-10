using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace PoolTournamentManager.App.Controls;

/// <summary>
/// A lightweight text element that renders its text with a stroke (outline) around the fill, so it
/// stays legible over a busy or variable background. WPF's <see cref="System.Windows.Controls.TextBlock"/>
/// has no text-outline option, so this draws the glyph geometry directly with both a <see cref="Pen"/>
/// (stroke) and a <see cref="Brush"/> (fill). Used for the Display window's round titles, which sit
/// directly over the faded ball watermark and otherwise disappear against its solid-white disc.
/// The font properties reuse the inherited <see cref="TextElement"/> attached properties so it reads
/// like a normal TextBlock in XAML (FontSize / FontWeight / FontFamily / …).
/// </summary>
public sealed class OutlinedTextBlock : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(string.Empty,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(3.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    // Reuse the inherited font attached properties so <controls:OutlinedTextBlock FontSize=".." .../>
    // works exactly like a TextBlock (and inherits ambient font values from ancestors).
    public static readonly DependencyProperty FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner(typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner(typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(SystemFonts.MessageFontSize,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner(typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(FontWeights.Normal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty FontStyleProperty =
        TextElement.FontStyleProperty.AddOwner(typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(FontStyles.Normal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty FontStretchProperty =
        TextElement.FontStretchProperty.AddOwner(typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(FontStretches.Normal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public FontStyle FontStyle { get => (FontStyle)GetValue(FontStyleProperty); set => SetValue(FontStyleProperty, value); }
    public FontStretch FontStretch { get => (FontStretch)GetValue(FontStretchProperty); set => SetValue(FontStretchProperty, value); }

    private FormattedText CreateFormattedText()
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        return new FormattedText(
            Text ?? string.Empty,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            Brushes.Black, // overridden by the fill/pen in OnRender; only the geometry is used
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return new Size(0, 0);
        }

        var formatted = CreateFormattedText();
        // Pad by the stroke so the outline isn't clipped at the edges (half on each side).
        return new Size(formatted.WidthIncludingTrailingWhitespace + StrokeThickness, formatted.Height + StrokeThickness);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        var formatted = CreateFormattedText();
        var geometry = formatted.BuildGeometry(new Point(StrokeThickness / 2, StrokeThickness / 2));
        var pen = Stroke is null || StrokeThickness <= 0
            ? null
            : new Pen(Stroke, StrokeThickness) { LineJoin = PenLineJoin.Round };

        // Draw stroke behind the fill (single DrawGeometry paints the pen under the brush).
        drawingContext.DrawGeometry(Fill, pen, geometry);
    }
}
