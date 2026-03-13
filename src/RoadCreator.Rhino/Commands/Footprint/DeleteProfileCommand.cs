using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Rhino.Footprint;

namespace RoadCreator.Rhino.Commands.Footprint;

/// <summary>
/// Deletes a named OffsetProfile from the current document.
///
/// Usage (interactive):  RC_DeleteProfile → pick from list
/// Usage (scripted):     _RC_DeleteProfile _Enter arterial_2plus1 _Enter
/// </summary>
[System.Runtime.InteropServices.Guid("5D0C4B6A-7E1F-4B2D-E5C8-4F9A3B0D6E1C")]
public class DeleteProfileCommand : Command
{
    public override string EnglishName => "RC_DeleteProfile";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var profiles = FootprintProfileStore.ListProfiles(doc);

        if (profiles.Count == 0)
        {
            RhinoApp.WriteLine("RC_DeleteProfile: no profiles stored in this document.");
            return Result.Success;
        }

        string? nameToDelete = null;

        if (profiles.Count == 1)
        {
            nameToDelete = profiles[0];
        }
        else
        {
            var getOpt = new GetOption();
            getOpt.SetCommandPrompt("Select profile to delete");
            foreach (var p in profiles)
                getOpt.AddOption(p.Replace(" ", "_").Replace("-", "_"));

            if (getOpt.Get() != GetResult.Option)
                return Result.Cancel;

            int idx = getOpt.Option().Index - 1;
            if (idx >= 0 && idx < profiles.Count)
                nameToDelete = profiles[idx];
        }

        if (nameToDelete == null)
            return Result.Cancel;

        bool deleted = FootprintProfileStore.DeleteProfile(doc, nameToDelete);
        RhinoApp.WriteLine(deleted
            ? $"RC_DeleteProfile: deleted '{nameToDelete}'."
            : $"RC_DeleteProfile: '{nameToDelete}' not found.");

        return deleted ? Result.Success : Result.Failure;
    }
}
