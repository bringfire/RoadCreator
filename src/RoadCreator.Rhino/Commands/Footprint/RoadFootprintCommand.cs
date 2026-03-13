using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Footprint;
using RoadCreator.Rhino.Footprint;

namespace RoadCreator.Rhino.Commands.Footprint;

/// <summary>
/// Generates 2D plan-view offset curves from a centerline using a named or inline
/// OffsetProfile and StyleSet.
///
/// Usage (interactive):
///   RC_RoadFootprint  → select curve → pick profile → pick style set
///
/// Usage (scripted / MCP agent):
///   Pre-select the curve, then:
///   _-RC_RoadFootprint "arterial_2plus1" "default_generic"
///   _-RC_RoadFootprint "{\"schema\":\"roadcreator.offset-profile/v1\",...}" "default"
///   The second prompt accepts "default" to use the built-in generic style set.
///
/// Output: one offset curve per profile feature, placed on appropriate layers.
///   Reports feature ids and object GUIDs to the command line for agent consumption.
/// </summary>
[System.Runtime.InteropServices.Guid("2A7F1E3D-4B8C-4E9A-B2F5-1C6D0E7A3B8F")]
public class RoadFootprintCommand : Command
{
    public override string EnglishName => "RC_RoadFootprint";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // ── 1. Select centerline ──────────────────────────────────────────────
        var getCurve = new GetObject();
        getCurve.SetCommandPrompt("Select centerline curve");
        getCurve.GeometryFilter = ObjectType.Curve;
        getCurve.EnablePreSelect(true, true);
        if (getCurve.Get() != GetResult.Object)
            return Result.Cancel;

        var centerline = getCurve.Object(0).Curve();
        if (centerline == null)
            return Result.Cancel;

        return mode == RunMode.Scripted
            ? RunScripted(doc, centerline)
            : RunInteractive(doc, centerline);
    }

    // ── Scripted / MCP path ───────────────────────────────────────────────────

    private static Result RunScripted(RhinoDoc doc, Curve centerline)
    {
        // Read profile: name from doc store, or inline JSON
        var gsProfile = new GetString();
        gsProfile.SetCommandPrompt("Profile name or JSON");
        if (gsProfile.Get() != GetResult.String)
            return Result.Cancel;

        var profileInput = gsProfile.StringResult().Trim();
        var profile = FootprintProfileStore.ResolveProfile(doc, profileInput);
        if (profile == null)
        {
            RhinoApp.WriteLine($"RC_RoadFootprint: profile not found or invalid: {profileInput}");
            return Result.Failure;
        }

        // Read style set: name from doc store or built-in; "default" → built-in generic
        var gsStyle = new GetString();
        gsStyle.SetCommandPrompt("Style set name (or \"default\")");
        if (gsStyle.Get() != GetResult.String)
            return Result.Cancel;

        var styleInput = gsStyle.StringResult().Trim();
        var styleKey = string.Equals(styleInput, "default", StringComparison.OrdinalIgnoreCase)
            ? null : styleInput;
        var styleSet = FootprintProfileStore.ResolveStyleSet(doc, styleKey);

        return ApplyAndReport(doc, centerline, profile, styleSet);
    }

    // ── Interactive path ──────────────────────────────────────────────────────

    private static Result RunInteractive(RhinoDoc doc, Curve centerline)
    {
        // ── 2. Resolve profile ────────────────────────────────────────────────
        OffsetProfile? profile = null;

        var storedProfiles = FootprintProfileStore.ListProfiles(doc);
        string profileInput;

        if (storedProfiles.Count > 0)
        {
            var getProfile = new GetOption();
            getProfile.SetCommandPrompt("Select profile");
            foreach (var p in storedProfiles)
                getProfile.AddOption(p.Replace(" ", "_").Replace("-", "_"));
            int inlineIdx = getProfile.AddOption("InlineJSON");

            if (getProfile.Get() != GetResult.Option)
                return Result.Cancel;

            // Use stored inlineIdx directly — safe even if more options are added later
            int selectedIndex = getProfile.Option().Index;
            if (selectedIndex == inlineIdx)
            {
                profile = PromptForInlineProfile(doc);
            }
            else
            {
                profileInput = storedProfiles[selectedIndex - 1];
                profile = FootprintProfileStore.GetProfile(doc, profileInput);
            }
        }
        else
        {
            // No stored profiles — go straight to inline JSON
            profile = PromptForInlineProfile(doc);
        }

        if (profile == null)
        {
            RhinoApp.WriteLine("RC_RoadFootprint: no valid profile specified.");
            return Result.Cancel;
        }

        // ── 3. Resolve style set ──────────────────────────────────────────────
        StyleSet styleSet;

        var storedSets = FootprintProfileStore.ListStyleSets(doc);
        // Build candidate list: stored + built-ins that aren't already stored
        var allStyleNames = new List<string>(storedSets);
        foreach (var kvp in DefaultStyles.All)
            if (!allStyleNames.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
                allStyleNames.Add(kvp.Key);

        if (allStyleNames.Count > 1)
        {
            var getStyle = new GetOption();
            getStyle.SetCommandPrompt("Select style set");
            foreach (var s in allStyleNames)
                getStyle.AddOption(s.Replace(" ", "_").Replace("-", "_"));

            if (getStyle.Get() != GetResult.Option)
                return Result.Cancel;

            int sIdx = getStyle.Option().Index - 1;
            styleSet = FootprintProfileStore.ResolveStyleSet(doc,
                sIdx < allStyleNames.Count ? allStyleNames[sIdx] : null);
        }
        else
        {
            // Resolve properly (doc store first) so a stored "default_generic"
            // override is honoured rather than silently using the built-in.
            styleSet = FootprintProfileStore.ResolveStyleSet(doc,
                allStyleNames.Count == 1 ? allStyleNames[0] : null);
        }

        return ApplyAndReport(doc, centerline, profile, styleSet);
    }

    // ── Shared apply + report ─────────────────────────────────────────────────

    private static Result ApplyAndReport(RhinoDoc doc, Curve centerline,
        OffsetProfile profile, StyleSet styleSet)
    {
        doc.Views.RedrawEnabled = false;
        try
        {
            var lines = FootprintApplicator.Apply(doc, centerline, profile, styleSet);

            RhinoApp.WriteLine($"RC_RoadFootprint: {lines.Count} lines created " +
                               $"from profile '{profile.Name}' with style '{styleSet.Name}'.");
            foreach (var line in lines)
                RhinoApp.WriteLine($"  {line.FeatureId} ({line.Role}): {line.ObjectId}");
        }
        finally
        {
            doc.Views.RedrawEnabled = true;
        }

        doc.Views.Redraw();
        return Result.Success;
    }

    private static OffsetProfile? PromptForInlineProfile(RhinoDoc doc)
    {
        var gs = new GetString();
        gs.SetCommandPrompt("Paste profile JSON (minified, single line)");
        if (gs.Get() != GetResult.String)
            return null;
        return FootprintProfileStore.ResolveProfile(doc, gs.StringResult().Trim());
    }
}
