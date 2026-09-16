using MonitorGrids.ViewModels;

namespace MonitorGrids.Views;

public sealed partial class MonitorGridsView
{
    public MonitorGridsView(MonitorGridsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}