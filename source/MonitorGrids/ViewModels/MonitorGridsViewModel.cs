using System.Collections.ObjectModel;
using Autodesk.Revit.UI;
using MonitorGrids.helper;
using Serilog;

namespace MonitorGrids.ViewModels;

public sealed partial class MonitorGridsViewModel : ObservableObject
{
  private readonly ILogger _logger;

  // 1. Đổi thành Document (Không để dấu ? nữa)
  private readonly Document _document;

  // Danh sách các File Link để binding lên ComboBox
  public ObservableCollection<RevitLinkInstance> RevitLinks { get; set; } = [];

  //link được chọn
  [ObservableProperty]
  private RevitLinkInstance _selectedLink;

  //Text Button Pinned
  [ObservableProperty]
  private string _textButtonPinned = "Không tồn tại link";

  //grids data in current Document
  private List<Grid> _gridInHost = [];
  public MonitorGridsViewModel(ILogger logger)
  {
    this._logger = logger;

    // 2. Kiểm tra nếu Revit không mở file nào thì báo lỗi/log và dừng lại
    if (RevitContext.ActiveDocument == null)
    {
      _logger.Error("Không tìm thấy Document hiện hành của Revit.");
      // Bạn có thể throw exception hoặc xử lý ngắt form tại đây tùy kiến trúc add-in
      throw new InvalidOperationException("Active document is null.");
    }
    // 3. Chắc chắn không null, gán giá trị an toàn
    this._document = RevitContext.ActiveDocument;
    this._gridInHost = GridHelper.GetGridInDocument(_document);

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
      // PinnedLink(IsClick: "false");
      CheckUpdateGridInLinkFile(SelectedLink);

    }
  }


  // Revit API chưa expose đầy đủ quan hệ Copy/Monitor (GetMonitoredLinkElementIds không đáng tin cậy,
  // xem https://forums.autodesk.com/t5/revit-ideas/copy-monitor-api/idi-p/6322737 - vẫn "Gathering Support" từ 2016).
  // Nên so sánh Grid theo Name thay vì dựa vào quan hệ monitor.
  private void CheckUpdateGridInLinkFile(RevitLinkInstance SelectedLink)
  {
    var masterDoc = SelectedLink.GetLinkDocument();

    var masterGridNames = GridHelper
      .GetGridInDocument(masterDoc)
      .Select(grid => grid.Name)
      .ToHashSet();

    var hostGridNames = _gridInHost
      .Select(grid => grid.Name)
      .ToHashSet();

    var gridNewInMaster = masterGridNames.Except(hostGridNames).ToHashSet();
    var gridRedundantInHost = hostGridNames.Except(masterGridNames).ToHashSet();

    _logger.Information(
        "Đối chiếu Grid theo tên: Master({MasterCount})={@MasterNames} | Host({HostCount})={@HostNames} | New={@New} | Redundant={@Redundant}",
        masterGridNames.Count, masterGridNames,
        hostGridNames.Count, hostGridNames,
        gridNewInMaster, gridRedundantInHost);

    if (gridNewInMaster.Count > 0)
    {
      TaskDialog.Show("Thông báo",
          $"Trục được thêm mới trong masterlink là {string.Join(", ", gridNewInMaster)}");
    }

    if (gridRedundantInHost.Count > 0)
    {
      TaskDialog.Show("Thông báo",
          $"Trục bị thừa trong host sau khi xóa ở master trong masterlink là {string.Join(", ", gridRedundantInHost)}");
    }
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

      // Chỉ ghi log ngắn gọn Tên file và ID khi người dùng click, tuyệt đối không truyền cả SelectedLink vào log
      _logger.Information("Đã đổi trạng thái Pin của file Link: {Name} (ID: {Id}) thành {Status}",
          SelectedLink.Name, SelectedLink.Id, SelectedLink.Pinned);

      // TextButtonPinned = SelectedLink.Pinned ? "📌 PINNED" : "📌 UNPINNED";
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

  // 2. Hàm tự động chạy BẤT CỨ KHI NÀO người dùng thay đổi ComboBox
  // Quy tắc đặt tên: On + Tên biến chữ Hoa + Changed
  partial void OnSelectedLinkChanged(RevitLinkInstance value)
  {
    if (value == null) return;
    _logger.Information("Người dùng vừa đổi ComboBox sang file link: {LinkName}", value.Name);

    TextButtonPinned = SelectedLink.Pinned ? "📌 PINNED" : "📌 UNPINNED";

  }


}