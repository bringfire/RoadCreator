using global::Rhino;
using global::Rhino.Commands;
using RoadCreator.Rhino.Footprint;

namespace RoadCreator.Rhino.Commands.Footprint;

/// <summary>
/// Lists all OffsetProfile definitions stored in the current document.
/// Output is written to the Rhino command line (agent-readable).
///
/// Usage: RC_ListProfiles
/// </summary>
[System.Runtime.InteropServices.Guid("4C9B3A5F-6D0E-4A1C-D4B7-3E8F2A9C5D0B")]
public class ListProfilesCommand : Command
{
    public override string EnglishName => "RC_ListProfiles";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var profiles = FootprintProfileStore.ListProfiles(doc);

        if (profiles.Count == 0)
        {
            RhinoApp.WriteLine("RC_ListProfiles: no profiles stored in this document.");
            RhinoApp.WriteLine("  Use RC_StoreProfile to add one.");
            return Result.Success;
        }

        RhinoApp.WriteLine($"RC_ListProfiles: {profiles.Count} profile(s):");
        foreach (var name in profiles)
        {
            var p = FootprintProfileStore.GetProfile(doc, name);
            string detail = p != null
                ? $"{p.Features.Count} features, units={p.Units}, baseline={p.Baseline}"
                : "?";
            RhinoApp.WriteLine($"  {name}  ({detail})");
        }

        return Result.Success;
    }
}
