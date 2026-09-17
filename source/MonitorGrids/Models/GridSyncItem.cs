namespace MonitorGrids.Models;

/// <summary>
///     Một dòng hiển thị trên DataGrid kết quả quét.
/// </summary>
public sealed partial class GridSyncItem : ObservableObject
{
  [ObservableProperty]
  private bool _isSelected;

  public GridSyncStatus Status { get; set; }
  public string StatusText { get; set; } = string.Empty;
  public string ElementType { get; set; } = "Grid";
  public string MasterName { get; set; } = "—";
  public string HostName { get; set; } = "—";
  public string OffsetDistance { get; set; } = "—";
  public string RecommendedAction { get; set; } = string.Empty;

  public ElementId? HostGridId { get; set; }
  public ElementId? MasterGridId { get; set; }
}
