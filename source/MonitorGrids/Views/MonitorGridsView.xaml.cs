using System.Windows;
using MonitorGrids.ViewModels;

namespace MonitorGrids.Views;

public sealed partial class MonitorGridsView
{
    public MonitorGridsView(MonitorGridsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        //Đăng kí sự kiện khi vừa load để gọi hàm load links revit instance
        this.Loaded += new RoutedEventHandler(OnWindowFirstLoad);
    }

    /// <summary>
    /// Hàm xử lý sự kiện khi giao diện đã nạp xong (Event Handler)
    /// </summary>
    /// <param name="sender">Đối tượng phát ra sự kiện (ở đây chính là bản thân cái Window này)</param>
    /// <param name="e">Dữ liệu đi kèm sự kiện Loaded</param>
    /// 
    private void OnWindowFirstLoad(object sender, RoutedEventArgs e)
    {
        if(DataContext is MonitorGridsViewModel viewModel)
        {
            viewModel.InitFirst();// Gọi hàm quét RevitLinkInstance ngay lập tức
        }
    }
}