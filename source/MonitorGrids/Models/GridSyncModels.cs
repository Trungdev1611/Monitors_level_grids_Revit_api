namespace MonitorGrids.Models;

public enum GridSyncStatus
{
  Synced,
  NewInMaster,
  RedundantInHost,
  Shifted
}

/// <summary>
///     Cặp Grid được ghép theo Name giữa Host và Master link.
/// </summary>
public sealed class GridPair
{
  public Grid HostGrid { get; set; } = null!;
  public Grid MasterGrid { get; set; } = null!;

  /// <summary>Độ lệch vị trí giữa 2 trục, đơn vị nội bộ của Revit (feet).</summary>
  public double OffsetFeet { get; set; }
}

public sealed class GridSyncReport
{
  /// <summary>Trục có trong Master link nhưng Host chưa có.</summary>
  public List<Grid> NewInMaster { get; } = [];

  /// <summary>Trục còn ở Host nhưng Master link đã xóa.</summary>
  public List<Grid> RedundantInHost { get; } = [];

  /// <summary>Trục trùng tên nhưng lệch vị trí quá dung sai.</summary>
  public List<GridPair> ShiftedGrids { get; } = [];

  /// <summary>Trục khớp cả tên lẫn vị trí.</summary>
  public List<GridPair> SyncedGrids { get; } = [];

  public int TotalCount => NewInMaster.Count + RedundantInHost.Count + ShiftedGrids.Count + SyncedGrids.Count;
}
