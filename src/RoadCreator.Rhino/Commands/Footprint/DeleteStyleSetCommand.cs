using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Rhino.Footprint;

namespace RoadCreator.Rhino.Commands.Footprint;

/// <summary>
/// Deletes a named StyleSet from the current document.
/// Built-in style sets cannot be deleted (they live in code, not in the document).
///
/// Usage (interactive):  RC_DeleteStyleSet → pick from list
/// Usage (scripted):     _RC_DeleteStyleSet _Enter my_office_styles _Enter
/// </summary>
[System.Runtime.InteropServices.Guid("8A3F7E9D-0B4C-4E5A-B8F1-7C2D6E3A9B4F")]
public class DeleteStyleSetCommand : Command
{
    public override string EnglishName => "RC_DeleteStyleSet";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var stored = FootprintProfileStore.ListStyleSets(doc);

        if (stored.Count == 0)
        {
            RhinoApp.WriteLine("RC_DeleteStyleSet: no style sets stored in this document.");
            RhinoApp.WriteLine("  Built-in style sets cannot be deleted.");
            return Result.Success;
        }

        string? nameToDelete = null;

        if (stored.Count == 1)
        {
            nameToDelete = stored[0];
        }
        else
        {
            var getOpt = new GetOption();
            getOpt.SetCommandPrompt("Select style set to delete");
            foreach (var s in stored)
                getOpt.AddOption(s.Replace(" ", "_").Replace("-", "_"));

            if (getOpt.Get() != GetResult.Option)
                return Result.Cancel;

            int idx = getOpt.Option().Index - 1;
            if (idx >= 0 && idx < stored.Count)
                nameToDelete = stored[idx];
        }

        if (nameToDelete == null)
            return Result.Cancel;

        bool deleted = FootprintProfileStore.DeleteStyleSet(doc, nameToDelete);
        RhinoApp.WriteLine(deleted
            ? $"RC_DeleteStyleSet: deleted '{nameToDelete}'."
            : $"RC_DeleteStyleSet: '{nameToDelete}' not found.");

        return deleted ? Result.Success : Result.Failure;
    }
}
