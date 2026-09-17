namespace MonitorGrids.helper;

public static class GridHelper
{
  public static List<Grid> GetGridInDocument(Document document)
  {
    var abc = new FilteredElementCollector(document)
    .OfClass(typeof(Grid))
    .Cast<Grid>()
    .ToList();
    return abc;

  }
}