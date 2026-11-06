using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace SlashTag.Services.EdgeAnimations;

/// <summary>故障：八条横向切片随机错位、闪烁，再缩进贴边方向；展开时从两侧滑入咬合。</summary>
public sealed class GlitchEdgeAnimation : IEdgeAnimation
{
    private const int Rows = 8;

    public string Id => "glitch";
    public bool NeedsSnapshot => true;

    public async Task HideAsync(EdgeAnimationContext context)
    {
        context.NoteShell.Visibility = Visibility.Hidden;
        var slices = EdgeAnimationVisuals.Tiles(context.Layer, context.Snapshot!, context.Width, context.Height, 1, Rows);
        var total = 0;

        for (var i = 0; i < slices.Count; i++)
        {
            var centerY = i * (context.Height / Rows) + context.Height / (Rows * 2);
            var dx = context.EdgeX - context.Width / 2;
            var dy = context.EdgeY - centerY;
            var delay = i * 8;
            var slice = slices[i];
            var group = EdgeAnimationVisuals.Prepare(slice);
            var scale = (ScaleTransform)group.Children[0];
            var translate = (TranslateTransform)group.Children[2];
            var begin = EdgeAnimationVisuals.Ms(delay);
            var span = EdgeAnimationVisuals.Ms(460);

            var x = new DoubleAnimationUsingKeyFrames { Duration = span, BeginTime = begin };
            x.KeyFrames.Add(new LinearDoubleKeyFrame(0, EdgeAnimationVisuals.Ms(0)));
            x.KeyFrames.Add(new LinearDoubleKeyFrame(Random.Shared.Next(-26, 27), EdgeAnimationVisuals.Ms(70)));
            x.KeyFrames.Add(new LinearDoubleKeyFrame(Random.Shared.Next(-18, 19), EdgeAnimationVisuals.Ms(140)));
            x.KeyFrames.Add(new LinearDoubleKeyFrame(0, EdgeAnimationVisuals.Ms(200)));
            x.KeyFrames.Add(new LinearDoubleKeyFrame(Random.Shared.Next(-10, 11), EdgeAnimationVisuals.Ms(260)));
            x.KeyFrames.Add(new LinearDoubleKeyFrame(dx, EdgeAnimationVisuals.Ms(460)));
            translate.BeginAnimation(TranslateTransform.XProperty, x);

            var y = new DoubleAnimationUsingKeyFrames { Duration = span, BeginTime = begin };
            y.KeyFrames.Add(new LinearDoubleKeyFrame(0, EdgeAnimationVisuals.Ms(0)));
            y.KeyFrames.Add(new LinearDoubleKeyFrame(0, EdgeAnimationVisuals.Ms(260)));
            y.KeyFrames.Add(new LinearDoubleKeyFrame(dy, EdgeAnimationVisuals.Ms(460)));
            translate.BeginAnimation(TranslateTransform.YProperty, y);

            var sx = new DoubleAnimationUsingKeyFrames { Duration = span, BeginTime = begin };
            sx.KeyFrames.Add(new LinearDoubleKeyFrame(1, EdgeAnimationVisuals.Ms(0)));
            sx.KeyFrames.Add(new LinearDoubleKeyFrame(1, EdgeAnimationVisuals.Ms(260)));
            sx.KeyFrames.Add(new LinearDoubleKeyFrame(0.05, EdgeAnimationVisuals.Ms(460)));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);

            var opacity = new DoubleAnimationUsingKeyFrames { Duration = span, BeginTime = begin };
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1, EdgeAnimationVisuals.Ms(0)));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0.35, EdgeAnimationVisuals.Ms(130)));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1, EdgeAnimationVisuals.Ms(170)));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0.5, EdgeAnimationVisuals.Ms(210)));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1, EdgeAnimationVisuals.Ms(260)));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, EdgeAnimationVisuals.Ms(460)));
            slice.BeginAnimation(UIElement.OpacityProperty, opacity);

            total = Math.Max(total, delay + 460);
        }

        await Task.Delay(total + 40);
        context.Layer.Children.Clear();
    }

    public async Task ShowAsync(EdgeAnimationContext context)
    {
        context.NoteShell.Visibility = Visibility.Hidden;
        var slices = EdgeAnimationVisuals.Tiles(context.Layer, context.Snapshot!, context.Width, context.Height, 1, Rows);
        var total = 0;

        for (var i = 0; i < slices.Count; i++)
        {
            var centerY = i * (context.Height / Rows) + context.Height / (Rows * 2);
            var dx = context.EdgeX - context.Width / 2;
            var dy = context.EdgeY - centerY;
            var jitter = (i % 2 == 0 ? -1 : 1) * Random.Shared.Next(50, 110);
            var delay = i * 14;
            var slice = slices[i];
            var group = EdgeAnimationVisuals.Prepare(slice);
            var scale = (ScaleTransform)group.Children[0];
            var translate = (TranslateTransform)group.Children[2];
            var begin = EdgeAnimationVisuals.Ms(delay);
            var span = EdgeAnimationVisuals.Ms(480);

            translate.X = dx;
            translate.Y = dy;
            scale.ScaleX = 0.05;
            scale.ScaleY = 0.05;
            slice.Opacity = 0;

            var x = new DoubleAnimationUsingKeyFrames { Duration = span, BeginTime = begin };
            x.KeyFrames.Add(new LinearDoubleKeyFrame(dx, EdgeAnimationVisuals.Ms(0)));
            x.KeyFrames.Add(new LinearDoubleKeyFrame(jitter, EdgeAnimationVisuals.Ms(200)));
            x.KeyFrames.Add(new LinearDoubleKeyFrame(jitter * 0.2, EdgeAnimationVisuals.Ms(340)));
            x.KeyFrames.Add(new LinearDoubleKeyFrame(0, EdgeAnimationVisuals.Ms(480)));
            translate.BeginAnimation(TranslateTransform.XProperty, x);

            var y = new DoubleAnimationUsingKeyFrames { Duration = span, BeginTime = begin };
            y.KeyFrames.Add(new LinearDoubleKeyFrame(dy, EdgeAnimationVisuals.Ms(0)));
            y.KeyFrames.Add(new LinearDoubleKeyFrame(0, EdgeAnimationVisuals.Ms(200)));
            y.KeyFrames.Add(new LinearDoubleKeyFrame(0, EdgeAnimationVisuals.Ms(480)));
            translate.BeginAnimation(TranslateTransform.YProperty, y);

            var sx = new DoubleAnimationUsingKeyFrames { Duration = span, BeginTime = begin };
            sx.KeyFrames.Add(new LinearDoubleKeyFrame(0.05, EdgeAnimationVisuals.Ms(0)));
            sx.KeyFrames.Add(new LinearDoubleKeyFrame(1.04, EdgeAnimationVisuals.Ms(200)));
            sx.KeyFrames.Add(new LinearDoubleKeyFrame(1, EdgeAnimationVisuals.Ms(480)));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);

            var sy = new DoubleAnimationUsingKeyFrames { Duration = span, BeginTime = begin };
            sy.KeyFrames.Add(new LinearDoubleKeyFrame(0.05, EdgeAnimationVisuals.Ms(0)));
            sy.KeyFrames.Add(new LinearDoubleKeyFrame(1, EdgeAnimationVisuals.Ms(200)));
            sy.KeyFrames.Add(new LinearDoubleKeyFrame(1, EdgeAnimationVisuals.Ms(480)));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);

            var opacity = new DoubleAnimationUsingKeyFrames { Duration = span, BeginTime = begin };
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, EdgeAnimationVisuals.Ms(0)));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1, EdgeAnimationVisuals.Ms(120)));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1, EdgeAnimationVisuals.Ms(480)));
            slice.BeginAnimation(UIElement.OpacityProperty, opacity);

            total = Math.Max(total, delay + 480);
        }

        await Task.Delay(total + 40);
        Shake(context.Layer);
        await Task.Delay(140);
        context.Layer.Children.Clear();
        context.NoteShell.Visibility = Visibility.Visible;
    }

    private static void Shake(Canvas layer)
    {
        var transform = new TranslateTransform();
        layer.RenderTransform = transform;
        var duration = EdgeAnimationVisuals.Ms(130);
        var x = new DoubleAnimationUsingKeyFrames { Duration = duration };
        x.KeyFrames.Add(new LinearDoubleKeyFrame(0, EdgeAnimationVisuals.Ms(0)));
        x.KeyFrames.Add(new LinearDoubleKeyFrame(3, EdgeAnimationVisuals.Ms(35)));
        x.KeyFrames.Add(new LinearDoubleKeyFrame(-3, EdgeAnimationVisuals.Ms(70)));
        x.KeyFrames.Add(new LinearDoubleKeyFrame(0, EdgeAnimationVisuals.Ms(130)));
        transform.BeginAnimation(TranslateTransform.XProperty, x);
        var y = new DoubleAnimationUsingKeyFrames { Duration = duration };
        y.KeyFrames.Add(new LinearDoubleKeyFrame(0, EdgeAnimationVisuals.Ms(0)));
        y.KeyFrames.Add(new LinearDoubleKeyFrame(-3, EdgeAnimationVisuals.Ms(35)));
        y.KeyFrames.Add(new LinearDoubleKeyFrame(3, EdgeAnimationVisuals.Ms(70)));
        y.KeyFrames.Add(new LinearDoubleKeyFrame(0, EdgeAnimationVisuals.Ms(130)));
        transform.BeginAnimation(TranslateTransform.YProperty, y);
    }
}
