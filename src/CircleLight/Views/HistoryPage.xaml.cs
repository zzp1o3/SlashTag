using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using CircleLight.Data;
using CircleLight.ViewModels;

namespace CircleLight.Views;

public partial class HistoryPage : UserControl
{
    public HistoryPage() => InitializeComponent();

    public void Load(TaskStore store) =>
        DataContext = new HistoryViewModel(store.GetHistory(), store.CountAllCompletions());
}
