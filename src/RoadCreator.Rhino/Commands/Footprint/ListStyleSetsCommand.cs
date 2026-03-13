using global::Rhino;
using global::Rhino.Commands;
using RoadCreator.Core.Footprint;
using RoadCreator.Rhino.Footprint;

namespace RoadCreator.Rhino.Commands.Footprint;

/// <summary>
/// Lists all StyleSet definitions available: document-stored first, then built-ins.
///
/// Usage: RC_ListStyleSets
/// </summary>
[System.Runtime.InteropServices.Guid("7F2E6D8C-9A3B-4D4F-A7E0-6B1C5D2F8A3E")]
public class ListStyleSetsCommand : Command
{
    public override string EnglishName => "RC_ListStyleSets";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var stored = FootprintProfileStore.ListStyleSets(doc);

        // Show document-stored sets
        if (stored.Count > 0)
        {
            RhinoApp.WriteLine($"RC_ListStyleSets: {stored.Count} stored in document:");
            foreach (var name in stored)
            {
                var ss = FootprintProfileStore.GetStyleSet(doc, name);
                string detail = ss != null ? $"{ss.Styles.Count} styles" : "?";
                RhinoApp.WriteLine($"  [doc] {name}  ({detail})");
            }
        }
        else
        {
            RhinoApp.WriteLine("RC_ListStyleSets: no style sets stored in document.");
        }

        // Always show built-ins
        RhinoApp.WriteLine($"  Built-in style sets ({DefaultStyles.All.Count}):");
        foreach (var kvp in DefaultStyles.All)
            RhinoApp.WriteLine($"  [builtin] {kvp.Key}  ({kvp.Value.Styles.Count} styles)");

        return Result.Success;
    }
}
