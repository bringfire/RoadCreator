using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Footprint;
using RoadCreator.Rhino.Footprint;

namespace RoadCreator.Rhino.Commands.Footprint;

/// <summary>
/// Stores a StyleSet JSON definition in the current document.
/// The style set can then be referenced by name in RC_RoadFootprint.
///
/// Usage (interactive):  RC_StoreStyleSet → paste JSON
/// Usage (scripted):     _RC_StoreStyleSet _Enter {"schema":"roadcreator.style-set/v1",...} _Enter
/// </summary>
[System.Runtime.InteropServices.Guid("6E1D5C7B-8F2A-4C3E-F6D9-5A0B4C1E7F2D")]
public class StoreStyleSetCommand : Command
{
    public override string EnglishName => "RC_StoreStyleSet";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var gs = new GetString();
        gs.SetCommandPrompt("Paste StyleSet JSON (single line)");
        if (gs.Get() != GetResult.String)
            return Result.Cancel;

        var json = gs.StringResult().Trim();
        var styleSet = FootprintSerializer.DeserializeStyleSet(json);

        if (styleSet == null)
        {
            RhinoApp.WriteLine("RC_StoreStyleSet: invalid JSON or unrecognised schema.");
            return Result.Failure;
        }

        FootprintProfileStore.StoreStyleSet(doc, styleSet);
        RhinoApp.WriteLine($"RC_StoreStyleSet: stored style set '{styleSet.Name}' " +
                           $"({styleSet.Styles.Count} styles).");
        return Result.Success;
    }
}
