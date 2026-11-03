using CircleLight.Controls;
using CircleLight.Data;
using CircleLight.ViewModels;

namespace CircleLight.Views;

public partial class HistoryWindow : PixelWindow
{
    public HistoryWindow(TaskStore store)
    {
        InitializeComponent();
        DataContext = new HistoryViewModel(store.GetHistory(), store.CountAllCompletions());
    }
}
