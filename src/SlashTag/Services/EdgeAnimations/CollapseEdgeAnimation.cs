using System.Windows;
using System.Windows.Media;

namespace SlashTag.Services.EdgeAnimations;

/// <summary>像素坍缩：整张便签碎成 48 块，吸向贴边方向；展开时倒序拼回。</summary>
public sealed class CollapseEdgeAnimation : IEdgeAnimation
{
    private const int Cols = 6;
    private const int Rows = 8;

    public string Id => "collapse";
    public bool NeedsSnapshot => true;

    public async Task HideAsync(EdgeAnimationContext context)
    {
        context.NoteShell.Visibility = Visibility.Hidden;
        var tiles = EdgeAnimationVisuals.Tiles(context.Layer, context.Snapshot!, context.Width, context.Height, Cols, Rows);
        var total = 0;

        for (var i = 0; i < tiles.Count; i++)
        {
            var col = i % Cols;
            var row = i / Cols;
            var centerX = col * (context.Width / Cols) + context.Width / (Cols * 2);
            var centerY = row * (context.Height / Rows) + context.Height / (Rows * 2);
            var delay = col * 8 + row * 10;
            const int duration = 340;

            var group = EdgeAnimationVisuals.Prepare(tiles[i]);
            EdgeAnimationVisuals.FlyOut(tiles[i], group, context.EdgeX - centerX, context.EdgeY - centerY, delay, duration);
            total = Math.Max(total, delay + duration);
        }

        await Task.Delay(total + 40);
        context.Layer.Children.Clear();
    }

    public async Task ShowAsync(EdgeAnimationContext context)
    {
        context.NoteShell.Visibility = Visibility.Hidden;
        var tiles = EdgeAnimationVisuals.Tiles(context.Layer, context.Snapshot!, context.Width, context.Height, Cols, Rows);
        var total = 0;

        for (var i = 0; i < tiles.Count; i++)
        {
            var col = i % Cols;
            var row = i / Cols;
            var centerX = col * (context.Width / Cols) + context.Width / (Cols * 2);
            var centerY = row * (context.Height / Rows) + context.Height / (Rows * 2);
            var delay = ((Rows - 1 - row) * Cols + (Cols - 1 - col)) * 6;
            const int duration = 280;

            var group = EdgeAnimationVisuals.Prepare(tiles[i]);
            EdgeAnimationVisuals.Assemble(tiles[i], group, context.EdgeX - centerX, context.EdgeY - centerY, delay, duration);
            total = Math.Max(total, delay + duration);
        }

        await Task.Delay(total + 50);
        context.Layer.Children.Clear();
        context.NoteShell.Visibility = Visibility.Visible;
    }
}
