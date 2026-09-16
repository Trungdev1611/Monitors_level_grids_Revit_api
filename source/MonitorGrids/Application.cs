using Nice3point.Revit.Toolkit.External;
using MonitorGrids.Commands;

namespace MonitorGrids;

/// <summary>
///     Application entry point
/// </summary>
[UsedImplicitly]
public class Application : ExternalApplication
{
    public override void OnStartup()
    {
        Host.Start();
        CreateRibbon();
    }

    private void CreateRibbon()
    {
        var panel = Application.CreatePanel("Commands", "MonitorGrids");

        panel.AddPushButton<StartupCommand>("Execute")
            .SetImage("/MonitorGrids;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/MonitorGrids;component/Resources/Icons/RibbonIcon32.png");
    }
}