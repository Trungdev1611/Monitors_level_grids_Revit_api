using MonitorGrids.helper;
using MonitorGrids.Models;
using Serilog;

namespace MonitorGrids.Services;

public sealed class GridSyncResult
{
  public int Copied { get; set; }
  public int Moved { get; set; }
  public int Deleted { get; set; }

  /// <summary>Các trục không xử lý tự động được, kèm lý do.</summary>
  public List<string> Skipped { get; } = [];

  public string? Error { get; set; }
}

/// <summary>
///     Áp dụng các thay đổi đã được người dùng chọn lên model host, trong một transaction duy nhất.
/// </summary>
public sealed class GridSyncService
{
  private readonly ILogger _logger;

  public GridSyncService(ILogger logger)
  {
    _logger = logger;
  }

  public GridSyncResult ExecuteSync(Document hostDoc, RevitLinkInstance linkInstance, IReadOnlyList<GridSyncItem> items)
  {
    var result = new GridSyncResult();

    var masterDoc = linkInstance.GetLinkDocument();
    if (masterDoc == null)
    {
      result.Error = "File link chưa được load, không thể đồng bộ.";
      return result;
    }

    using var transaction = new Transaction(hostDoc, "Đồng bộ trục với file link");
    try
    {
      transaction.Start();

      // Nuốt warning để Revit không bật dialog ẩn sau cửa sổ WPF khiến người dùng tưởng bị treo
      var failureOptions = transaction.GetFailureHandlingOptions();
      failureOptions.SetFailuresPreprocessor(new WarningSwallower());
      failureOptions.SetClearAfterRollback(true);
      transaction.SetFailureHandlingOptions(failureOptions);

      CopyNewGrids(hostDoc, masterDoc, linkInstance, items, result);
      MoveShiftedGrids(hostDoc, masterDoc, linkInstance, items, result);
      DeleteRedundantGrids(hostDoc, items, result);

      transaction.Commit();

      _logger.Information("Đồng bộ trục xong: copy {Copied}, dịch chuyển {Moved}, xóa {Deleted}, bỏ qua {Skipped}",
        result.Copied, result.Moved, result.Deleted, result.Skipped.Count);
    }
    catch (System.Exception ex)
    {
      if (transaction.HasStarted() && !transaction.HasEnded())
      {
        transaction.RollBack();
      }

      result.Error = ex.Message;
      _logger.Error(ex, "Lỗi khi đồng bộ trục với link {LinkName}", linkInstance.Name);
    }

    return result;
  }

  private static void CopyNewGrids(Document hostDoc, Document masterDoc, RevitLinkInstance linkInstance,
    IReadOnlyList<GridSyncItem> items, GridSyncResult result)
  {
    var idsToCopy = items
      .Where(item => item.Status == GridSyncStatus.NewInMaster && item.MasterGridId != null)
      .Select(item => item.MasterGridId!)
      .ToList();

    if (idsToCopy.Count == 0) return;

    var options = new CopyPasteOptions();
    options.SetDuplicateTypeNamesHandler(new UseDestinationTypesHandler());

    var copied = ElementTransformUtils.CopyElements(
      masterDoc, idsToCopy, hostDoc, linkInstance.GetTotalTransform(), options);

    result.Copied = copied.Count;
  }

  private void MoveShiftedGrids(Document hostDoc, Document masterDoc, RevitLinkInstance linkInstance,
    IReadOnlyList<GridSyncItem> items, GridSyncResult result)
  {
    foreach (var item in items.Where(item => item.Status == GridSyncStatus.Shifted))
    {
      if (item.HostGridId == null || item.MasterGridId == null) continue;

      if (hostDoc.GetElement(item.HostGridId) is not Grid hostGrid ||
          masterDoc.GetElement(item.MasterGridId) is not Grid masterGrid)
      {
        result.Skipped.Add($"{item.HostName}: không tìm thấy trục trong model");
        continue;
      }

      var hostCurve = hostGrid.Curve;
      var masterCurve = GridHelper.GetCurveInHostCoordinates(masterGrid, linkInstance);
      if (hostCurve == null || masterCurve == null)
      {
        result.Skipped.Add($"{item.HostName}: không lấy được đường dựng");
        continue;
      }

      var translation = GridHelper.GetTranslationToMatch(hostCurve, masterCurve);
      if (translation == null)
      {
        result.Skipped.Add($"{item.HostName}: trục bị xoay góc, cần chỉnh tay");
        continue;
      }

      // Revit không cho dịch chuyển element đang pin, nên tạm bỏ pin rồi trả lại trạng thái cũ
      var wasPinned = hostGrid.Pinned;
      if (wasPinned) hostGrid.Pinned = false;

      ElementTransformUtils.MoveElement(hostDoc, hostGrid.Id, translation);

      if (wasPinned) hostGrid.Pinned = true;
      result.Moved++;
    }
  }

  private static void DeleteRedundantGrids(Document hostDoc, IReadOnlyList<GridSyncItem> items, GridSyncResult result)
  {
    var idsToDelete = items
      .Where(item => item.Status == GridSyncStatus.RedundantInHost && item.HostGridId != null)
      .Select(item => item.HostGridId!)
      .ToList();

    if (idsToDelete.Count == 0) return;

    foreach (var id in idsToDelete)
    {
      if (hostDoc.GetElement(id) is { Pinned: true } element) element.Pinned = false;
    }

    result.Deleted = hostDoc.Delete(idsToDelete).Count;
  }
}

internal sealed class UseDestinationTypesHandler : IDuplicateTypeNamesHandler
{
  public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
  {
    return DuplicateTypeAction.UseDestinationTypes;
  }
}

internal sealed class WarningSwallower : IFailuresPreprocessor
{
  public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
  {
    foreach (var failure in failuresAccessor.GetFailureMessages())
    {
      if (failure.GetSeverity() == FailureSeverity.Warning)
      {
        failuresAccessor.DeleteWarning(failure);
      }
    }

    return FailureProcessingResult.Continue;
  }
}
