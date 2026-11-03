using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Rectangle = System.Windows.Shapes.Rectangle;
using Size = System.Windows.Size;

namespace CircleLight.Services;

/// <summary>
/// 新增与完成任务的像素动效：覆盖层绘制，不影响布局与命中测试。
/// 完成动效带连击：2.6 秒内连续勾选会升档，×3 起触发 PERFECT。
/// </summary>
public sealed class EffectService
{
    private static readonly Color[] ParticleColors =
    {
        Color.FromRgb(0xFF, 0xE4, 0x5C),
        Color.FromRgb(0x66, 0xE0, 0xFF),
        Color.FromRgb(0xFF, 0x6F, 0xB5),
        Color.FromRgb(0xF2, 0xF0, 0xFF),
    };

    private static readonly FontFamily PixelFont = new("Courier New");

    private readonly Canvas _layer;
    private readonly TranslateTransform _shake = new();
    private readonly Brush _yellow;
    private readonly Brush _magenta;
    private readonly DispatcherTimer _comboTimer;
    private Border? _comboBadge;
    private TextBlock? _comboText;
    private int _combo;
    private DateTime _lastComplete;

    public EffectService(Canvas layer, FrameworkElement shakeTarget)
    {
        _layer = layer;
        _yellow = FindBrush("YellowBrush");
        _magenta = FindBrush("MagentaBrush");
        shakeTarget.RenderTransform = _shake;

        _comboTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        _comboTimer.Tick += (_, _) =>
        {
            _comboTimer.Stop();
            _combo = 0;
            if (_comboBadge is not null) _layer.Children.Remove(_comboBadge);
            _comboBadge = null;
            _comboText = null;
        };
    }

    /// <summary>系统关闭「动画效果」时整体跳过。</summary>
    private static bool Enabled => SystemParameters.ClientAreaAnimation;

    /// <summary>把行元素的位置换算到特效层坐标。必须在行被移除前调用。</summary>
    public Rect RectOf(FrameworkElement row)
    {
        try
        {
            return row.TransformToVisual(_layer)
                .TransformBounds(new Rect(row.RenderSize));
        }
        catch (InvalidOperationException)
        {
            return Rect.Empty;
        }
    }

    /// <summary>抓取行当前画面，用于断行残片。</summary>
    public static ImageSource? Capture(FrameworkElement row)
    {
        if (row.ActualWidth <= 0 || row.ActualHeight <= 0) return null;

        var dpi = VisualTreeHelper.GetDpi(row);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(row.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(row.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);
        bitmap.Render(row);
        bitmap.Freeze();
        return bitmap;
    }

    public void PlayAdd(FrameworkElement row)
    {
        if (!Enabled) return;

        var rect = RectOf(row);
        if (rect.IsEmpty) return;

        var group = new TransformGroup();
        var scale = new ScaleTransform(1, 1) { CenterX = rect.Width / 2, CenterY = 0 };
        var translate = new TranslateTransform();
        group.Children.Add(scale);
        group.Children.Add(translate);
        row.RenderTransform = group;

        var duration = Ms(240);
        var drop = KeyFrames(duration,
            (0, -20), (132, 2), (187, -1), (240, 0));
        translate.BeginAnimation(TranslateTransform.YProperty, drop);

        var squash = KeyFrames(duration,
            (0, 0.5), (132, 1.1), (187, 0.97), (240, 1));
        squash.Completed += (_, _) => row.RenderTransform = Transform.Identity;
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, squash);

        row.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, Ms(90)));

        Delay(150, () => Dust(rect));
    }

    public void PlayComplete(Rect rect, ImageSource? snapshot, bool overdue, bool split)
    {
        if (!Enabled || rect.IsEmpty) return;

        _combo = DateTime.Now - _lastComplete < TimeSpan.FromSeconds(2.6) ? _combo + 1 : 1;
        _lastComplete = DateTime.Now;
        var combo = _combo;

        ShowCombo(combo);
        if (split && snapshot is not null) Split(rect, snapshot, combo);
        Slash(rect, combo);

        var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        Delay(70, () =>
        {
            Flash(combo >= 3 ? 0.4 : 0.55, combo >= 3 ? _yellow : Brushes.White);
            Burst(center, 12 + combo * 3, combo);
            Shake(2 + combo);
            if (combo >= 3) Perfect();
        });

        Float(rect.Right - 26, rect.Top + 2, "+1", _yellow, 13);
        if (overdue) Float(rect.Right - 30, rect.Top + 18, "RESCUED", _magenta, 9);
    }

    // ---------- 分层动效 ----------

    private void Split(Rect rect, ImageSource snapshot, int combo)
    {
        var half = rect.Height / 2;
        var top = Half(snapshot, new Rect(0, 0, 1, 0.52), rect.X, rect.Y, rect.Width, half);
        var bottom = Half(snapshot, new Rect(0, 0.48, 1, 0.52), rect.X, rect.Y + rect.Height - half, rect.Width, half);

        Drop(top, 14 + combo * 2, -22, 7);
        Drop(bottom, -16 - combo * 2, 30, -8);
    }

    private Rectangle Half(ImageSource snapshot, Rect viewbox, double x, double y, double width, double height)
    {
        var rect = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new ImageBrush(snapshot)
            {
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewbox = viewbox,
                Stretch = Stretch.Fill,
            },
            RenderTransformOrigin = new Point(0.5, 0.5),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        _layer.Children.Add(rect);
        return rect;
    }

    private void Drop(Rectangle rect, double dx, double dy, double angle)
    {
        var group = new TransformGroup();
        var rotate = new RotateTransform();
        var translate = new TranslateTransform();
        group.Children.Add(rotate);
        group.Children.Add(translate);
        rect.RenderTransform = group;

        var duration = Ms(300);
        var ease = new PowerEase { EasingMode = EasingMode.EaseOut, Power = 2 };
        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dx, duration) { EasingFunction = ease });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(dy, duration) { EasingFunction = ease });
        rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(angle, duration) { EasingFunction = ease });

        var fade = new DoubleAnimation(1, 0, duration);
        fade.Completed += (_, _) => _layer.Children.Remove(rect);
        rect.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void Slash(Rect rect, int combo)
    {
        var width = rect.Width + 16;
        var bar = new Rectangle
        {
            Width = width,
            Height = 4,
            Fill = combo >= 3 ? _magenta : combo == 2 ? _yellow : Brushes.White,
            Stroke = combo >= 3 ? _magenta : _yellow,
            StrokeThickness = 2,
            RenderTransformOrigin = new Point(0.5, 0.5),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(bar, rect.X - 8);
        Canvas.SetTop(bar, rect.Y + rect.Height / 2 - 2);

        var group = new TransformGroup();
        var scale = new ScaleTransform(0.25, 1);
        var rotate = new RotateTransform(-16);
        var translate = new TranslateTransform();
        group.Children.Add(scale);
        group.Children.Add(rotate);
        group.Children.Add(translate);
        bar.RenderTransform = group;
        _layer.Children.Add(bar);

        var duration = Ms(170);
        var grow = KeyFrames(duration, (0, 0.25), (85, 0.9), (145, 1.15));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);

        var sweep = new DoubleAnimation(-width * 0.6, width * 0.5, duration);
        sweep.Completed += (_, _) => _layer.Children.Remove(bar);
        translate.BeginAnimation(TranslateTransform.XProperty, sweep);

        var fade = new DoubleAnimationUsingKeyFrames { Duration = duration };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, Ms(0)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, Ms(40)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, Ms(140)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, Ms(170)));
        bar.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void Burst(Point center, int count, int combo)
    {
        var spread = 100 + combo * 12;
        var up = 150 + combo * 12;

        for (var i = 0; i < count; i++)
        {
            var size = 4 + Random.Shared.Next(0, 3) * 2;
            var particle = new Rectangle
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(ParticleColors[Random.Shared.Next(ParticleColors.Length)]),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(particle, center.X);
            Canvas.SetTop(particle, center.Y);

            var translate = new TranslateTransform();
            particle.RenderTransform = translate;
            _layer.Children.Add(particle);

            var vx = (Random.Shared.NextDouble() * 2 - 1) * spread;
            var vy = -(up * 0.35 + Random.Shared.NextDouble() * up * 0.65);
            var dy = 320 + Math.Abs(vy) * 0.4;
            var duration = Ms(520 + combo * 40 + Random.Shared.Next(-60, 120));

            translate.BeginAnimation(TranslateTransform.XProperty, KeyFrames(duration,
                (0, 0), (duration.TotalMilliseconds * 0.45, vx * 0.7), (duration.TotalMilliseconds, vx * 1.6)));
            translate.BeginAnimation(TranslateTransform.YProperty, KeyFrames(duration,
                (0, 0), (duration.TotalMilliseconds * 0.45, vy + dy * 0.35), (duration.TotalMilliseconds, vy + dy)));

            var fade = new DoubleAnimation(1, 0, duration);
            fade.Completed += (_, _) => _layer.Children.Remove(particle);
            particle.BeginAnimation(UIElement.OpacityProperty, fade);
        }
    }

    private void Shake(double px)
    {
        var duration = Ms(130);
        _shake.BeginAnimation(TranslateTransform.XProperty,
            KeyFrames(duration, (0, 0), (30, px), (65, -px), (95, px * 0.5), (130, 0)));
        _shake.BeginAnimation(TranslateTransform.YProperty,
            KeyFrames(duration, (0, 0), (30, -px), (65, px * 0.6), (95, -px * 0.3), (130, 0)));
    }

    private void Perfect()
    {
        var text = new TextBlock
        {
            Text = "PERFECT",
            Width = _layer.ActualWidth,
            TextAlignment = TextAlignment.Center,
            FontFamily = PixelFont,
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = _yellow,
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                ShadowDepth = 3,
                BlurRadius = 0,
                Opacity = 0.8,
            },
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        Canvas.SetLeft(text, 0);
        Canvas.SetTop(text, _layer.ActualHeight * 0.40);

        var scale = new ScaleTransform(0.6, 0.6);
        text.RenderTransform = scale;
        _layer.Children.Add(text);

        var duration = Ms(700);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            KeyFrames(duration, (0, 0.6), (245, 1.15), (420, 1), (700, 1)));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            KeyFrames(duration, (0, 0.6), (245, 1.15), (420, 1), (700, 1)));

        var fade = new DoubleAnimationUsingKeyFrames { Duration = duration };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, Ms(0)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, Ms(245)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, Ms(420)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, Ms(700)));
        fade.Completed += (_, _) => _layer.Children.Remove(text);
        text.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void ShowCombo(int combo)
    {
        if (_comboBadge is null)
        {
            _comboText = new TextBlock
            {
                FontFamily = PixelFont,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = _magenta,
            };
            _comboBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x0A, 0x09, 0x18)),
                BorderBrush = _magenta,
                BorderThickness = new Thickness(2),
                Padding = new Thickness(5, 2, 5, 2),
                Child = _comboText,
                RenderTransformOrigin = new Point(0.5, 0.5),
                IsHitTestVisible = false,
            };
            _layer.Children.Add(_comboBadge);
        }

        _comboText!.Text = "COMBO ×" + combo;
        _comboBadge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(_comboBadge, Math.Max(4, _layer.ActualWidth - _comboBadge.DesiredSize.Width - 8));
        Canvas.SetTop(_comboBadge, 34);

        var scale = new ScaleTransform(1.4, 1.4);
        _comboBadge.RenderTransform = scale;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, Ms(160)));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, Ms(160)));

        _comboTimer.Stop();
        _comboTimer.Start();
    }

    private void Float(double x, double y, string text, Brush color, double size)
    {
        var label = new TextBlock
        {
            Text = text,
            FontFamily = PixelFont,
            FontSize = size,
            FontWeight = FontWeights.Bold,
            Foreground = color,
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                ShadowDepth = 2,
                BlurRadius = 0,
                Opacity = 0.7,
            },
            IsHitTestVisible = false,
        };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
        Canvas.SetTop(label, y);

        var translate = new TranslateTransform();
        label.RenderTransform = translate;
        _layer.Children.Add(label);

        var duration = Ms(620);
        translate.BeginAnimation(TranslateTransform.YProperty,
            KeyFrames(duration, (0, 0), (434, -22), (620, -34)));

        var fade = new DoubleAnimationUsingKeyFrames { Duration = duration };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, Ms(0)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, Ms(500)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, Ms(620)));
        fade.Completed += (_, _) => _layer.Children.Remove(label);
        label.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void Flash(double opacity, Brush color)
    {
        var flash = new Rectangle
        {
            Width = _layer.ActualWidth,
            Height = _layer.ActualHeight,
            Fill = color,
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(flash, 0);
        Canvas.SetTop(flash, 0);
        _layer.Children.Add(flash);

        var fade = new DoubleAnimationUsingKeyFrames { Duration = Ms(70) };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(opacity, Ms(0)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, Ms(70)));
        fade.Completed += (_, _) => _layer.Children.Remove(flash);
        flash.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void Dust(Rect rect)
    {
        var center = new Point(rect.X + 20, rect.Y + rect.Height - 3);
        for (var i = 0; i < 7; i++)
        {
            var size = 3 + Random.Shared.Next(0, 3) * 2;
            var particle = new Rectangle
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(i % 2 == 0
                    ? Color.FromRgb(0x8F, 0x8A, 0xC0)
                    : Color.FromRgb(0xF2, 0xF0, 0xFF)),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(particle, center.X);
            Canvas.SetTop(particle, center.Y);

            var translate = new TranslateTransform();
            particle.RenderTransform = translate;
            _layer.Children.Add(particle);

            var duration = Ms(300 + Random.Shared.Next(-40, 80));
            translate.BeginAnimation(TranslateTransform.XProperty, KeyFrames(duration,
                (0, 0), (duration.TotalMilliseconds, (Random.Shared.NextDouble() * 2 - 1) * 44)));
            translate.BeginAnimation(TranslateTransform.YProperty, KeyFrames(duration,
                (0, 0), (duration.TotalMilliseconds * 0.5, -18), (duration.TotalMilliseconds, 42)));

            var fade = new DoubleAnimation(1, 0, duration);
            fade.Completed += (_, _) => _layer.Children.Remove(particle);
            particle.BeginAnimation(UIElement.OpacityProperty, fade);
        }
    }

    // ---------- 工具 ----------

    private void Delay(int milliseconds, Action action)
    {
        var timer = new DispatcherTimer { Interval = Ms(milliseconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
    }

    private static DoubleAnimationUsingKeyFrames KeyFrames(TimeSpan duration, params (double At, double Value)[] frames)
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = duration };
        foreach (var (at, value) in frames)
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(value, Ms(at)));
        return animation;
    }

    private static TimeSpan Ms(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private static Brush FindBrush(string key) =>
        (Brush)(Application.Current.TryFindResource(key) ?? Brushes.White);
}
