using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SlashTag.Services.EdgeAnimations;
using SlashTag.Views;

namespace SlashTag.Services;

/// <summary>贴边收起/展开动画的注册表：新增动画实现 IEdgeAnimation 后加到这里即可。</summary>
public sealed class EdgeAnimator
{
    private readonly FrameworkElement _noteShell;
    private readonly List<IEdgeAnimation> _animations;
    private readonly FadeEdgeAnimation _fallback = new();

    public EdgeAnimator(FrameworkElement noteShell)
    {
        _noteShell = noteShell;
        _animations = new List<IEdgeAnimation>
        {
            new CollapseEdgeAnimation(),
            new GlitchEdgeAnimation(),
            new FadeEdgeAnimation(),
        };
    }

    /// <summary>设置页据此生成选项，顺序即展示顺序。</summary>
    public static IReadOnlyList<EdgeAnimationInfo> Catalog { get; } = new[]
    {
        new EdgeAnimationInfo("collapse", "EdgeAnimCollapse"),
        new EdgeAnimationInfo("glitch", "EdgeAnimGlitch"),
        new EdgeAnimationInfo("fade", "EdgeAnimFade"),
    };

    public IEdgeAnimation Resolve(string? mode, string? pool)
    {
        if (string.Equals(mode, "random", StringComparison.OrdinalIgnoreCase))
        {
            var ids = (pool ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var candidates = _animations.Where(a => ids.Contains(a.Id)).ToList();
            if (candidates.Count == 0) candidates = _animations.ToList();
            return candidates[Random.Shared.Next(candidates.Count)];
        }

        return _animations.FirstOrDefault(a => a.Id == mode) ?? _animations[0];
    }

    public async Task HideAsync(IEdgeAnimation animation, bool toRight)
    {
        var window = Window.GetWindow(_noteShell);
        if (!animation.NeedsSnapshot)
        {
            var context = new EdgeAnimationContext
            {
                NoteShell = _noteShell,
                Layer = new Canvas(),
                ToRight = toRight,
            };
            try
            {
                await animation.HideAsync(context);
            }
            finally
            {
                Finish(hidden: true);
            }
            return;
        }

        var snapshot = EdgeAnimationVisuals.Capture(_noteShell);
        if (snapshot is null)
        {
            await HideAsync(_fallback, toRight);
            return;
        }

        var overlay = new AnimationOverlayWindow();
        overlay.Prepare(_noteShell);
        window?.Hide();

        var capture = new EdgeAnimationContext
        {
            NoteShell = _noteShell,
            Layer = overlay.CanvasLayer,
            ToRight = toRight,
            Snapshot = snapshot,
        };

        try
        {
            await animation.HideAsync(capture);
        }
        finally
        {
            overlay.Finish();
            Finish(hidden: true);
        }
    }

    public async Task ShowAsync(IEdgeAnimation animation, bool toRight)
    {
        var window = Window.GetWindow(_noteShell);

        if (!animation.NeedsSnapshot)
        {
            if (window is not null && !window.IsVisible)
            {
                window.Show();
                window.UpdateLayout();
            }

            var context = new EdgeAnimationContext
            {
                NoteShell = _noteShell,
                Layer = new Canvas(),
                ToRight = toRight,
            };
            try
            {
                await animation.ShowAsync(context);
            }
            finally
            {
                Finish(hidden: false);
            }
            return;
        }

        if (window is not null && !window.IsVisible)
        {
            window.Show();
            window.UpdateLayout();
        }

        _noteShell.BeginAnimation(UIElement.OpacityProperty, null);
        _noteShell.Opacity = 1;
        _noteShell.Visibility = Visibility.Visible;
        var snapshot = EdgeAnimationVisuals.Capture(_noteShell);
        if (snapshot is null)
        {
            await ShowAsync(_fallback, toRight);
            return;
        }

        var overlay = new AnimationOverlayWindow();
        overlay.Prepare(_noteShell);
        _noteShell.Visibility = Visibility.Hidden;
        window?.Hide();

        var capture = new EdgeAnimationContext
        {
            NoteShell = _noteShell,
            Layer = overlay.CanvasLayer,
            ToRight = toRight,
            Snapshot = snapshot,
        };

        try
        {
            await animation.ShowAsync(capture);
        }
        finally
        {
            overlay.Finish();
            if (window is not null && !window.IsVisible)
            {
                window.Show();
                window.UpdateLayout();
            }
            Finish(hidden: false);
        }
    }

    private void Finish(bool hidden)
    {
        _noteShell.BeginAnimation(UIElement.OpacityProperty, null);
        _noteShell.Opacity = 1;
        _noteShell.Visibility = hidden ? Visibility.Hidden : Visibility.Visible;
    }
}
