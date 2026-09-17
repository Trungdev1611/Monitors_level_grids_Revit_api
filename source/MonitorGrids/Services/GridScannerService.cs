using MonitorGrids.helper;
using MonitorGrids.Models;
using Serilog;

namespace MonitorGrids.Services;

/// <summary>
///     Quét và so sánh Grid giữa Host và Master link.
///     Ghép cặp theo Name vì Revit API không expose quan hệ Copy/Monitor thật
///     (xem https://forums.autodesk.com/t5/revit-ideas/copy-monitor-api/idi-p/6322737).
/// </summary>
public sealed class GridScannerService
{
  /// <summary>Dung sai vị trí: 0.001 feet (~0.3 mm).</summary>
  public const double OffsetToleranceFeet = 0.001;

  private readonly ILogger _logger;

  public GridScannerService(ILogger logger)
  {
    _logger = logger;
  }

  public GridSyncReport ScanAndCompare(Document hostDoc, RevitLinkInstance linkInstance)
  {
    var report = new GridSyncReport();

    var masterDoc = linkInstance.GetLinkDocument();
    if (masterDoc == null)
    {
      _logger.Warning("Link {LinkName} chưa được load, bỏ qua việc quét.", linkInstance.Name);
      return report;
    }

    // Design Option có thể tạo ra các trục trùng tên, nên gom nhóm thay vì ToDictionary trực tiếp
    var hostGrids = GroupByName(GridHelper.GetGridInDocument(hostDoc));
    var masterGrids = GroupByName(GridHelper.GetGridInDocument(masterDoc));

    foreach (var master in masterGrids)
    {
      if (!hostGrids.TryGetValue(master.Key, out var hostGrid))
      {
        report.NewInMaster.Add(master.Value);
        continue;
      }

      var pair = new GridPair
      {
        HostGrid = hostGrid,
        MasterGrid = master.Value,
        OffsetFeet = MeasureOffset(hostGrid, master.Value, linkInstance)
      };

      if (pair.OffsetFeet > OffsetToleranceFeet)
      {
        report.ShiftedGrids.Add(pair);
      }
      else
      {
        report.SyncedGrids.Add(pair);
      }
    }

    foreach (var host in hostGrids)
    {
      if (!masterGrids.ContainsKey(host.Key))
      {
        report.RedundantInHost.Add(host.Value);
      }
    }

    _logger.Information(
      "Quét Grid với link {LinkName}: tổng {Total}, đồng bộ {Synced}, mới ở master {New}, thừa ở host {Redundant}, lệch vị trí {Shifted}",
      linkInstance.Name, report.TotalCount, report.SyncedGrids.Count,
      report.NewInMaster.Count, report.RedundantInHost.Count, report.ShiftedGrids.Count);

    return report;
  }

  private double MeasureOffset(Grid hostGrid, Grid masterGrid, RevitLinkInstance linkInstance)
  {
    var hostCurve = hostGrid.Curve;
    var masterCurve = GridHelper.GetCurveInHostCoordinates(masterGrid, linkInstance);

    if (hostCurve == null || masterCurve == null)
    {
      _logger.Warning("Trục {GridName} không lấy được đường dựng, bỏ qua kiểm tra lệch vị trí.", hostGrid.Name);
      return 0;
    }

    return GridHelper.GetOffsetFeet(hostCurve, masterCurve);
  }

  private static Dictionary<string, Grid> GroupByName(List<Grid> grids)
  {
    return grids
      .GroupBy(grid => grid.Name)
      .ToDictionary(group => group.Key, group => group.First());
  }
}
