namespace MonitorGrids.helper;

public static class GridHelper
{
  /// <summary>Dung sai góc khi coi 2 trục là song song (~0.06 độ).</summary>
  private const double AngleToleranceRadians = 0.001;

  public static List<Grid> GetGridInDocument(Document document)
  {
    return new FilteredElementCollector(document)
      .OfClass(typeof(Grid))
      .Cast<Grid>()
      .ToList();
  }

  /// <summary>
  ///     Đưa đường dựng của trục bên Master link về hệ tọa độ của Host.
  /// </summary>
  public static Curve? GetCurveInHostCoordinates(Grid masterGrid, RevitLinkInstance linkInstance)
  {
    var curve = masterGrid.Curve;
    return curve?.CreateTransformed(linkInstance.GetTotalTransform());
  }

  /// <summary>
  ///     Độ lệch giữa 2 trục (feet). Trục là đường thẳng nên đo khoảng cách vuông góc,
  ///     không dùng khoảng cách điểm-điểm để tránh báo lệch sai khi 2 trục cùng đường nhưng khác chiều dài.
  /// </summary>
  public static double GetOffsetFeet(Curve hostCurve, Curve masterCurveInHost)
  {
    if (hostCurve is Line hostLine && masterCurveInHost is Line masterLine)
    {
      var direction = hostLine.Direction.Normalize();
      var offset = PerpendicularDistance(masterLine.GetEndPoint(0), hostLine.GetEndPoint(0), direction);

      var angle = hostLine.Direction.AngleTo(masterLine.Direction);
      if (angle > Math.PI / 2) angle = Math.PI - angle;

      // Trục bị xoay: quy đổi độ lệch góc thành độ lệch lớn nhất tại đầu kia của trục
      if (angle > AngleToleranceRadians)
      {
        var offsetAtEnd = PerpendicularDistance(masterLine.GetEndPoint(1), hostLine.GetEndPoint(0), direction);
        offset = Math.Max(offset, offsetAtEnd);
      }

      return offset;
    }

    // Trục cong: so theo 2 đầu mút, xét cả trường hợp 2 bên vẽ ngược chiều nhau
    var hostStart = hostCurve.GetEndPoint(0);
    var hostEnd = hostCurve.GetEndPoint(1);
    var masterStart = masterCurveInHost.GetEndPoint(0);
    var masterEnd = masterCurveInHost.GetEndPoint(1);

    var sameDirection = Math.Max(hostStart.DistanceTo(masterStart), hostEnd.DistanceTo(masterEnd));
    var reversed = Math.Max(hostStart.DistanceTo(masterEnd), hostEnd.DistanceTo(masterStart));
    return Math.Min(sameDirection, reversed);
  }

  /// <summary>
  ///     Vector cần tịnh tiến trục Host để trùng với trục Master.
  ///     Trả về null khi 2 trục không song song hoặc không phải đường thẳng,
  ///     vì khi đó phép tịnh tiến đơn thuần không đưa được về đúng vị trí.
  /// </summary>
  public static XYZ? GetTranslationToMatch(Curve hostCurve, Curve masterCurveInHost)
  {
    if (hostCurve is not Line hostLine || masterCurveInHost is not Line masterLine) return null;

    var angle = hostLine.Direction.AngleTo(masterLine.Direction);
    if (angle > Math.PI / 2) angle = Math.PI - angle;
    if (angle > AngleToleranceRadians) return null;

    var direction = hostLine.Direction.Normalize();
    var vector = masterLine.GetEndPoint(0) - hostLine.GetEndPoint(0);
    return vector - direction.Multiply(vector.DotProduct(direction));
  }

  private static double PerpendicularDistance(XYZ point, XYZ lineOrigin, XYZ lineDirection)
  {
    var vector = point - lineOrigin;
    var projected = lineDirection.Multiply(vector.DotProduct(lineDirection));
    return (vector - projected).GetLength();
  }
}
