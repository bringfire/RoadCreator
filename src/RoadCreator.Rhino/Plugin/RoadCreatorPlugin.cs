using System.Runtime.InteropServices;

[assembly: Guid("8A3F2C4E-1D59-4B7E-A9F3-6E2D8C7B5A4F")]
[assembly: global::Rhino.PlugIns.PlugInDescription(
    global::Rhino.PlugIns.DescriptionType.Organization, "RoadCreator")]
[assembly: global::Rhino.PlugIns.PlugInDescription(
    global::Rhino.PlugIns.DescriptionType.UpdateUrl, "")]

namespace RoadCreator.Rhino.Plugin;

/// <summary>
/// RoadCreator Rhino plugin entry point.
/// </summary>
[Guid("8A3F2C4E-1D59-4B7E-A9F3-6E2D8C7B5A4F")]
public class RoadCreatorPlugin : global::Rhino.PlugIns.PlugIn
{
    public static RoadCreatorPlugin? Instance { get; private set; }

    public RoadCreatorPlugin()
    {
        Instance = this;
    }

    protected override global::Rhino.PlugIns.LoadReturnCode OnLoad(ref string errorMessage)
    {
        // Restore persisted external database path
        var dbPath = Settings.GetString("ExternalDatabasePath", "");
        if (!string.IsNullOrEmpty(dbPath))
            Database.ExternalDatabase.Path = dbPath;

        global::Rhino.RhinoApp.WriteLine("RoadCreator plugin loaded.");
        return global::Rhino.PlugIns.LoadReturnCode.Success;
    }
}
