using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Rhino.Plugin;

namespace RoadCreator.Rhino.Database;

/// <summary>
/// Set, show, or clear the path to an external .3dm database file.
/// When set, all database commands read/write from this file instead of document layers.
/// </summary>
[System.Runtime.InteropServices.Guid("C3000001-D4E5-F6A7-B8C9-D0E1F2A3B401")]
public class SetExternalDatabaseCommand : Command
{
    public override string EnglishName => "RC_SetExternalDatabase";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var current = ExternalDatabase.Path;
        if (!string.IsNullOrEmpty(current))
            RhinoApp.WriteLine($"Current external database: {current}");
        else
            RhinoApp.WriteLine("No external database set (using document layers).");

        var getOpt = new GetOption();
        getOpt.SetCommandPrompt("External database action");
        getOpt.AddOption("Browse");
        getOpt.AddOption("Clear");
        if (getOpt.Get() != GetResult.Option)
            return Result.Cancel;

        var option = getOpt.Option().EnglishName;

        if (option == "Clear")
        {
            ExternalDatabase.Path = null;
            PersistPath(null);
            RhinoApp.WriteLine("External database cleared. Using document layers.");
            return Result.Success;
        }

        // Browse
        var dialog = new global::Rhino.UI.OpenFileDialog
        {
            Filter = "Rhino Files (*.3dm)|*.3dm",
            Title = "Select external database file",
        };
        if (!dialog.ShowOpenDialog())
            return Result.Cancel;

        var path = dialog.FileName;
        if (string.IsNullOrEmpty(path))
            return Result.Cancel;

        ExternalDatabase.Path = path;
        PersistPath(path);
        RhinoApp.WriteLine($"External database set: {path}");
        return Result.Success;
    }

    private static void PersistPath(string? path)
    {
        var plugin = RoadCreatorPlugin.Instance;
        if (plugin == null) return;
        plugin.Settings.SetString("ExternalDatabasePath", path ?? "");
    }
}
