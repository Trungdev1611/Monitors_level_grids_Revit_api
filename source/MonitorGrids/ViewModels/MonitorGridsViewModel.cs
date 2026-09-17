using System.Collections.ObjectModel;
using System.Windows;
using MonitorGrids.Models;
using MonitorGrids.Services;
using Serilog;

namespace MonitorGrids.ViewModels;

public sealed partial class MonitorGridsViewModel : ObservableObject
{
  private readonly ILogger _logger;
  private readonly GridScannerService _scanner;
  private readonly GridSyncService _syncService;
  private readonly Document _document;

  private List<GridSyncItem> _allItems = [];

  // Danh sách các File Link để binding lên ComboBox
  public ObservableCollection<RevitLinkInstance> RevitLinks { get; set; } = [];

  // Kết quả quét hiển thị lên DataGrid
  public ObservableCollection<GridSyncItem> DisplayItems { get; } = [];

  //link được chọn
  [ObservableProperty]
  private RevitLinkInstance _selectedLink;

  //Text Button Pinned
  [ObservableProperty]
  private string _textButtonPinned = "Không tồn tại link";

  [ObservableProperty]
  private string _searchKeyword = string.Empty;

  [ObservableProperty]
  private string _statusMessage = "Sẵn sàng. Chọn link để quét trục.";

  [ObservableProperty]
  private int _totalCount;

  [ObservableProperty]
  private int _syncedCount;

  [ObservableProperty]
  private int _missingCount;

  [ObservableProperty]
  private int _shiftedCount;

  public MonitorGridsViewModel(ILogger logger, GridScannerService scanner, GridSyncService syncService)
  {
    _logger = logger;
    _scanner = scanner;
    _syncService = syncService;

    if (RevitContext.ActiveDocument == null)
    {
      _logger.Error("Không tìm thấy Document hiện hành của Revit.");
      throw new InvalidOperationException("Active document is null.");
    }

    _document = RevitContext.ActiveDocument;
  }

  //chạy khi sự kiện onLoad được gọi bên MonitorGridsView.xaml.cs
  public void InitFirst()
  {
    var revitLinkInstance = new FilteredElementCollector(_document)
      .OfClass(typeof(RevitLinkInstance))
      .Cast<RevitLinkInstance>()
      .Where(link => link.GetLinkDocument() != null)
      .ToList();

    foreach (var link in revitLinkInstance)
    {
      RevitLinks.Add(link);
    }

    _logger.Information("Danh sách Selectbox::: {@data}", RevitLinks.Select(link => link.Name));

    if (RevitLinks.Count > 0)
    {
      SelectedLink = RevitLinks[0];
    }
    else
    {
      StatusMessage = "Không tìm thấy file link nào đã được load trong model.";
    }
  }

  [RelayCommand]
  private void Rescan()
  {
    if (SelectedLink == null) return;

    var report = _scanner.ScanAndCompare(_document, SelectedLink);

    TotalCount = report.TotalCount;
    SyncedCount = report.SyncedGrids.Count;
    MissingCount = report.NewInMaster.Count;
    ShiftedCount = report.ShiftedGrids.Count;

    _allItems = BuildItems(report);
    ApplyFilter();

    StatusMessage = report.RedundantInHost.Count > 0
      ? $"Đã quét {TotalCount} trục — {MissingCount} trục mới ở master, {ShiftedCount} trục lệch vị trí, {report.RedundantInHost.Count} trục thừa ở host."
      : $"Đã quét {TotalCount} trục — {MissingCount} trục mới ở master, {ShiftedCount} trục lệch vị trí.";
  }

  [RelayCommand]
  private void SelectAllDifferences()
  {
    var actionable = _allItems.Where(item => item.Status != GridSyncStatus.Synced).ToList();
    foreach (var item in actionable)
    {
      item.IsSelected = true;
    }

    StatusMessage = actionable.Count > 0
      ? $"Đã chọn {actionable.Count} khác biệt. Kiểm tra lại rồi bấm 'Apply Selected Changes'."
      : "Không có khác biệt nào cần xử lý.";
  }

  [RelayCommand]
  private void ApplySelectedChanges()
  {
    if (SelectedLink == null) return;

    var selected = _allItems
      .Where(item => item.IsSelected && item.Status != GridSyncStatus.Synced)
      .ToList();

    if (selected.Count == 0)
    {
      StatusMessage = "Chưa chọn dòng nào để đồng bộ.";
      return;
    }

    if (!ConfirmApply(selected)) return;

    var result = _syncService.ExecuteSync(_document, SelectedLink, selected);

    if (result.Error != null)
    {
      StatusMessage = $"Đồng bộ thất bại: {result.Error}";
      MessageBox.Show(result.Error, "Đồng bộ thất bại", MessageBoxButton.OK, MessageBoxImage.Error);
      return;
    }

    Rescan();

    StatusMessage = $"Đã copy {result.Copied} trục, dịch chuyển {result.Moved} trục, xóa {result.Deleted} trục." +
                    (result.Skipped.Count > 0 ? $" Bỏ qua {result.Skipped.Count} trục." : string.Empty);

    if (result.Skipped.Count > 0)
    {
      MessageBox.Show($"Các trục sau không xử lý tự động được:\n\n{string.Join("\n", result.Skipped)}",
        "Cần xử lý thủ công", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
  }

  private static bool ConfirmApply(List<GridSyncItem> selected)
  {
    var copyCount = selected.Count(item => item.Status == GridSyncStatus.NewInMaster);
    var moveCount = selected.Count(item => item.Status == GridSyncStatus.Shifted);
    var deleteCount = selected.Count(item => item.Status == GridSyncStatus.RedundantInHost);

    var message = $"Sẽ áp dụng {selected.Count} thay đổi lên model hiện hành:\n\n" +
                  $"• Copy sang host: {copyCount} trục\n" +
                  $"• Dịch chuyển vị trí: {moveCount} trục\n" +
                  $"• Xóa khỏi host: {deleteCount} trục\n\n";

    if (deleteCount > 0)
    {
      message += $"Lưu ý: {deleteCount} trục sẽ bị XÓA khỏi model host. " +
                 "Các dimension/annotation đang bám vào những trục này cũng sẽ mất.\n\n";
    }

    message += "Có thể hoàn tác bằng Ctrl+Z trong Revit sau khi đóng cửa sổ này. Tiếp tục?";

    return MessageBox.Show(message, "Xác nhận đồng bộ", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
           == MessageBoxResult.OK;
  }

  private static List<GridSyncItem> BuildItems(GridSyncReport report)
  {
    var items = new List<GridSyncItem>();

    foreach (var grid in report.NewInMaster)
    {
      items.Add(new GridSyncItem
      {
        Status = GridSyncStatus.NewInMaster,
        StatusText = "Mới ở Master",
        MasterName = grid.Name,
        RecommendedAction = "Copy sang Host",
        MasterGridId = grid.Id
      });
    }

    foreach (var pair in report.ShiftedGrids)
    {
      items.Add(new GridSyncItem
      {
        Status = GridSyncStatus.Shifted,
        StatusText = "Lệch vị trí",
        MasterName = pair.MasterGrid.Name,
        HostName = pair.HostGrid.Name,
        OffsetDistance = FormatOffset(pair.OffsetFeet),
        RecommendedAction = "Cập nhật vị trí",
        MasterGridId = pair.MasterGrid.Id,
        HostGridId = pair.HostGrid.Id
      });
    }

    foreach (var grid in report.RedundantInHost)
    {
      items.Add(new GridSyncItem
      {
        Status = GridSyncStatus.RedundantInHost,
        StatusText = "Thừa ở Host",
        HostName = grid.Name,
        RecommendedAction = "Xóa khỏi Host",
        HostGridId = grid.Id
      });
    }

    foreach (var pair in report.SyncedGrids)
    {
      items.Add(new GridSyncItem
      {
        Status = GridSyncStatus.Synced,
        StatusText = "Đã đồng bộ",
        MasterName = pair.MasterGrid.Name,
        HostName = pair.HostGrid.Name,
        OffsetDistance = FormatOffset(pair.OffsetFeet),
        RecommendedAction = "Không cần xử lý",
        MasterGridId = pair.MasterGrid.Id,
        HostGridId = pair.HostGrid.Id
      });
    }

    return items;
  }

  private static string FormatOffset(double offsetFeet)
  {
    var millimeters = UnitUtils.ConvertFromInternalUnits(offsetFeet, UnitTypeId.Millimeters);
    return $"{millimeters:0.##} mm";
  }

  private void ApplyFilter()
  {
    var keyword = SearchKeyword?.Trim();

    var filtered = string.IsNullOrEmpty(keyword)
      ? _allItems
      : _allItems.Where(item =>
          item.MasterName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
          item.HostName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
          item.StatusText.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    DisplayItems.Clear();
    foreach (var item in filtered)
    {
      DisplayItems.Add(item);
    }
  }

  partial void OnSearchKeywordChanged(string value)
  {
    ApplyFilter();
  }

  [RelayCommand]
  private void PinnedLink(string IsClick = "true")
  {
    if (SelectedLink == null) return;

    using Transaction t = new(SelectedLink.Document, "Pin or unpin Link");
    try
    {
      t.Start();
      if (IsClick == "true")
      {
        SelectedLink.Pinned = !SelectedLink.Pinned;
      }

      _logger.Information("Đã đổi trạng thái Pin của file Link: {Name} (ID: {Id}) thành {Status}",
          SelectedLink.Name, SelectedLink.Id, SelectedLink.Pinned);

      TextButtonPinned = SelectedLink.Pinned ? "📌 PINNED" : "📌 UNPINNED";
      t.Commit();
    }
    catch (System.Exception ex)
    {
      if (t.HasStarted() && !t.HasEnded())
      {
        t.RollBack();
      }
      _logger.Error(ex, "Lỗi khi Pin/Unpin link {LinkName}", SelectedLink?.Name);
    }
  }

  // Chạy mỗi khi người dùng đổi link trên ComboBox
  partial void OnSelectedLinkChanged(RevitLinkInstance value)
  {
    if (value == null) return;

    _logger.Information("Người dùng vừa đổi ComboBox sang file link: {LinkName}", value.Name);
    TextButtonPinned = value.Pinned ? "📌 PINNED" : "📌 UNPINNED";

    Rescan();
  }
}
