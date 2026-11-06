using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SlashTag.Services.EdgeAnimations;

namespace SlashTag.Services;

/// <summary>贴边收起/展开动画的注册表：新增动画实现 IEdgeAnimation 后加到这里即可。</summary>
public sealed class EdgeAnimator
{
    private readonly FrameworkElement _noteShell;
    private readonly Canvas _layer;
    private readonly List<IEdgeAnimation> _animations;
    private readonly FadeEdgeAnimation _fallback = new();

    public EdgeAnimator(FrameworkElement noteShell, Canvas layer)
    {
        _noteShell = noteShell;
        _layer = layer;
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
        var snapshot = animation.NeedsSnapshot ? EdgeAnimationVisuals.Capture(_noteShell) : null;
        if (animation.NeedsSnapshot && snapshot is null) animation = _fallback;

        var context = new EdgeAnimationContext
        {
            NoteShell = _noteShell,
            Layer = _layer,
            ToRight = toRight,
            Snapshot = snapshot,
        };

        try
        {
            await animation.HideAsync(context);
        }
        finally
        {
            Finish(hidden: true);
        }
    }

    public async Task ShowAsync(IEdgeAnimation animation, bool toRight)
    {
        ImageSource? snapshot = null;
        if (animation.NeedsSnapshot)
        {
            _noteShell.Opacity = 1;
            _noteShell.Visibility = Visibility.Visible;
            snapshot = EdgeAnimationVisuals.Capture(_noteShell);
            _noteShell.Visibility = Visibility.Hidden;
            if (snapshot is null) animation = _fallback;
        }

        var context = new EdgeAnimationContext
        {
            NoteShell = _noteShell,
            Layer = _layer,
            ToRight = toRight,
            Snapshot = snapshot,
        };

        try
        {
            await animation.ShowAsync(context);
        }
        finally
        {
            Finish(hidden: false);
        }
    }

    private void Finish(bool hidden)
    {
        _layer.Children.Clear();
        _layer.RenderTransform = Transform.Identity;
        _noteShell.BeginAnimation(UIElement.OpacityProperty, null);
        _noteShell.Opacity = 1;
        _noteShell.Visibility = hidden ? Visibility.Hidden : Visibility.Visible;
    }
}
