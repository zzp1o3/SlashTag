using System.Windows;
using System.Windows.Media.Animation;

namespace SlashTag.Services.EdgeAnimations;

/// <summary>淡入淡出：最轻的兜底动画。</summary>
public sealed class FadeEdgeAnimation : IEdgeAnimation
{
    public string Id => "fade";
    public bool NeedsSnapshot => false;

    public async Task HideAsync(EdgeAnimationContext context)
    {
        var done = new TaskCompletionSource();
        var fade = new DoubleAnimation(1, 0, EdgeAnimationVisuals.Ms(140));
        fade.Completed += (_, _) => done.TrySetResult();
        context.NoteShell.BeginAnimation(UIElement.OpacityProperty, fade);
        await done.Task;
        context.NoteShell.BeginAnimation(UIElement.OpacityProperty, null);
        context.NoteShell.Opacity = 0;
    }

    public async Task ShowAsync(EdgeAnimationContext context)
    {
        context.NoteShell.Visibility = Visibility.Visible;
        context.NoteShell.Opacity = 0.4;
        var done = new TaskCompletionSource();
        var fade = new DoubleAnimation(0.4, 1, EdgeAnimationVisuals.Ms(150));
        fade.Completed += (_, _) => done.TrySetResult();
        context.NoteShell.BeginAnimation(UIElement.OpacityProperty, fade);
        await done.Task;
    }
}
