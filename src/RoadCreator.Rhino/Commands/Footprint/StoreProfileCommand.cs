using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Footprint;
using RoadCreator.Rhino.Footprint;

namespace RoadCreator.Rhino.Commands.Footprint;

/// <summary>
/// Stores an OffsetProfile JSON definition in the current document.
/// The profile can then be referenced by name in RC_RoadFootprint.
///
/// Usage (interactive):  RC_StoreProfile → paste JSON
/// Usage (scripted):     _RC_StoreProfile _Enter {"schema":"roadcreator.offset-profile/v1",...} _Enter
///
/// The JSON must be minified to a single line for scripted/MCP use.
/// The profile name is read from the JSON "name" field.
/// </summary>
[System.Runtime.InteropServices.Guid("3B8A2F4E-5C9D-4F0B-C3A6-2D7E1F8B4C9A")]
public class StoreProfileCommand : Command
{
    public override string EnglishName => "RC_StoreProfile";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var gs = new GetString();
        gs.SetCommandPrompt("Paste OffsetProfile JSON (single line)");
        if (gs.Get() != GetResult.String)
            return Result.Cancel;

        var json = gs.StringResult().Trim();
        var profile = FootprintSerializer.DeserializeProfile(json);

        if (profile == null)
        {
            RhinoApp.WriteLine("RC_StoreProfile: invalid JSON or unrecognised schema.");
            return Result.Failure;
        }

        FootprintProfileStore.StoreProfile(doc, profile);
        RhinoApp.WriteLine($"RC_StoreProfile: stored profile '{profile.Name}' " +
                           $"({profile.Features.Count} features).");
        return Result.Success;
    }
}
