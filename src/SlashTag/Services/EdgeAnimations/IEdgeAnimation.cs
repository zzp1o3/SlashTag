using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace SlashTag.Services.EdgeAnimations;

/// <summary>设置页用来列出可选动画。</summary>
public sealed record EdgeAnimationInfo(string Id, string NameKey);

/// <summary>贴边收起/展开动画。新增动画只需实现它并注册到 EdgeAnimator。</summary>
public interface IEdgeAnimation
{
    string Id { get; }
    bool NeedsSnapshot { get; }

    Task HideAsync(EdgeAnimationContext context);
    Task ShowAsync(EdgeAnimationContext context);
}

public sealed class EdgeAnimationContext
{
    public required FrameworkElement NoteShell { get; init; }
    public required Canvas Layer { get; init; }
    public required bool ToRight { get; init; }
    public ImageSource? Snapshot { get; init; }

    public double Width => NoteShell.ActualWidth;
    public double Height => NoteShell.ActualHeight;
    public double EdgeX => ToRight ? Width + 10 : -10;
    public double EdgeY => Height / 2;
}

internal static class EdgeAnimationVisuals
{
    public static ImageSource? Capture(FrameworkElement element)
    {
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return null;

        var dpi = VisualTreeHelper.GetDpi(element);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);
        bitmap.Render(element);
        bitmap.Freeze();
        return bitmap;
    }

    public static List<Rectangle> Tiles(Canvas layer, ImageSource snapshot, double width, double height, int cols, int rows)
    {
        var tiles = new List<Rectangle>();
        var tileWidth = width / cols;
        var tileHeight = height / rows;

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var tile = new Rectangle
                {
                    Width = tileWidth,
                    Height = tileHeight,
                    Fill = new ImageBrush(snapshot)
                    {
                        ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
                        Viewbox = new Rect((double)col / cols, (double)row / rows, 1.0 / cols, 1.0 / rows),
                        Stretch = Stretch.Fill,
                    },
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(tile, col * tileWidth);
                Canvas.SetTop(tile, row * tileHeight);
                layer.Children.Add(tile);
                tiles.Add(tile);
            }
        }

        return tiles;
    }

    public static TransformGroup Prepare(Rectangle tile)
    {
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(1, 1));
        group.Children.Add(new RotateTransform(0));
        group.Children.Add(new TranslateTransform(0, 0));
        tile.RenderTransform = group;
        return group;
    }

    public static TimeSpan Ms(double value) => TimeSpan.FromMilliseconds(value);

    public static void FlyOut(Rectangle tile, TransformGroup group, double dx, double dy, int delay, int duration)
    {
        var scale = (ScaleTransform)group.Children[0];
        var rotate = (RotateTransform)group.Children[1];
        var translate = (TranslateTransform)group.Children[2];
        var begin = Ms(delay);
        var span = Ms(duration);

        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, dx, span) { BeginTime = begin });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, dy, span) { BeginTime = begin });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 0.05, span) { BeginTime = begin });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 0.05, span) { BeginTime = begin });
        rotate.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(0, Random.Shared.Next(-120, 120), span) { BeginTime = begin });
        tile.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, span) { BeginTime = begin });
    }

    public static void Assemble(Rectangle tile, TransformGroup group, double dx, double dy, int delay, int duration)
    {
        var scale = (ScaleTransform)group.Children[0];
        var rotate = (RotateTransform)group.Children[1];
        var translate = (TranslateTransform)group.Children[2];
        var begin = Ms(delay);
        var span = Ms(duration);

        scale.ScaleX = 0.05;
        scale.ScaleY = 0.05;
        rotate.Angle = 0;
        translate.X = dx;
        translate.Y = dy;
        tile.Opacity = 0;

        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dx, 0, span) { BeginTime = begin });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(dy, 0, span) { BeginTime = begin });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.05, 1, span) { BeginTime = begin });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.05, 1, span) { BeginTime = begin });
        tile.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, span) { BeginTime = begin });
    }
}
