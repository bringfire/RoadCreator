using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Input.Custom;

namespace RoadCreator.Rhino.Commands.Road;

[System.Runtime.InteropServices.Guid("6E1A5A4D-8A58-4B4D-B4C1-5A6175D63B2E")]
public sealed class ResolveIntersectionCommand : Command
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(120),
    };

    private static string _lastProfileA = "arterial_with_bike_lanes";
    private static string _lastProfileB = "collector_one_side_bike";
    private static string _lastAsymmetricSideA = "";
    private static string _lastAsymmetricSideB = "right";
    private static string _lastCandidateMode = "single";

    public override string EnglishName => "RC_ResolveIntersection";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (!TryGetSelectedCurves(doc, out var centerlineA, out var centerlineB))
            return Result.Cancel;

        if (!PromptForString("Profile A", ref _lastProfileA))
            return Result.Cancel;
        if (!PromptForString("Profile B", ref _lastProfileB))
            return Result.Cancel;

        if (!PromptForInitialCandidateMode(ref _lastCandidateMode))
            return Result.Cancel;

        var nativeBaseUri = ResolveNativeBaseUri();
        var targetLayerRoot = BuildTargetLayerRoot();
        string? candidateId = null;
        var candidateMode = _lastCandidateMode;
        var asymmetricSideA = _lastAsymmetricSideA;
        var asymmetricSideB = _lastAsymmetricSideB;

        while (true)
        {
            var request = new JsonObject
            {
                ["centerlineA"] = centerlineA,
                ["centerlineB"] = centerlineB,
                ["candidateMode"] = candidateMode,
                ["profileA"] = _lastProfileA,
                ["profileB"] = _lastProfileB,
                ["targetLayerRoot"] = targetLayerRoot,
            };

            if (!string.IsNullOrWhiteSpace(candidateId))
                request["candidateId"] = candidateId;
            if (!string.IsNullOrWhiteSpace(asymmetricSideA))
                request["asymmetricSideA"] = asymmetricSideA;
            if (!string.IsNullOrWhiteSpace(asymmetricSideB))
                request["asymmetricSideB"] = asymmetricSideB;

            JsonNode responseNode;
            try
            {
                responseNode = PostJson($"{nativeBaseUri}/road/intersection/resolve", request);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"RC_ResolveIntersection: request failed: {ex.Message}");
                return Result.Failure;
            }

            if (!responseNode["success"]?.GetValue<bool>() ?? true)
            {
                var error = responseNode["data"]?.ToString() ?? "Unknown error";
                RhinoApp.WriteLine($"RC_ResolveIntersection: {error}");
                return Result.Failure;
            }

            var data = responseNode["data"]?.AsObject();
            if (data == null)
            {
                RhinoApp.WriteLine("RC_ResolveIntersection: response did not contain a data object.");
                return Result.Failure;
            }

            var executionMode = data["executionMode"]?.GetValue<string>() ?? "";
            if (executionMode == "candidate-selection-required")
            {
                if (!PromptForCandidateSelection(data, ref candidateMode, ref candidateId))
                    return Result.Cancel;
                continue;
            }

            if (executionMode == "side-selection-required"
                || data["requiresSideSelection"]?.GetValue<bool>() == true)
            {
                if (!PromptForRequiredSides(data, ref asymmetricSideA, ref asymmetricSideB))
                    return Result.Cancel;

                _lastAsymmetricSideA = asymmetricSideA;
                _lastAsymmetricSideB = asymmetricSideB;
                continue;
            }

            WriteResolveSummary(data, targetLayerRoot);
            doc.Views.Redraw();
            return Result.Success;
        }
    }

    private static bool TryGetSelectedCurves(
        RhinoDoc doc,
        out string centerlineA,
        out string centerlineB)
    {
        centerlineA = "";
        centerlineB = "";

        var getCurves = new GetObject();
        getCurves.SetCommandPrompt("Select two centerline curves");
        getCurves.GeometryFilter = ObjectType.Curve;
        getCurves.EnablePreSelect(true, true);
        getCurves.SubObjectSelect = false;
        getCurves.GroupSelect = false;
        var result = getCurves.GetMultiple(2, 2);
        if (result != global::Rhino.Input.GetResult.Object)
            return false;

        centerlineA = getCurves.Object(0).ObjectId.ToString();
        centerlineB = getCurves.Object(1).ObjectId.ToString();
        return true;
    }

    private static bool PromptForString(string prompt, ref string value)
    {
        var get = new GetString();
        get.SetCommandPrompt(prompt);
        if (!string.IsNullOrWhiteSpace(value))
            get.SetDefaultString(value);
        if (get.Get() != global::Rhino.Input.GetResult.String)
            return false;

        value = get.StringResult().Trim();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool PromptForInitialCandidateMode(ref string candidateMode)
    {
        var get = new GetOption();
        get.SetCommandPrompt("Candidate mode");
        var singleOption = get.AddOption("Single");
        var allOption = get.AddOption("All");
        get.SetDefaultString(string.Equals(candidateMode, "all", StringComparison.OrdinalIgnoreCase) ? "All" : "Single");

        var result = get.Get();
        if (result == global::Rhino.Input.GetResult.Nothing)
            return true;
        if (result != global::Rhino.Input.GetResult.Option)
            return false;

        candidateMode = get.Option().Index == allOption ? "all" : "single";
        return true;
    }

    private static bool PromptForCandidateSelection(
        JsonObject data,
        ref string candidateMode,
        ref string? candidateId)
    {
        var candidates = data["candidates"]?.AsArray();
        if (candidates == null || candidates.Count == 0)
            return false;

        RhinoApp.WriteLine("RC_ResolveIntersection: multiple candidates found.");
        foreach (var node in candidates)
        {
            if (node is not JsonObject candidate)
                continue;

            var id = candidate["candidateId"]?.ToString() ?? "(unknown)";
            var point = candidate["point"]?.AsArray();
            var x = point?.Count > 0 ? point[0]?.ToString() : "?";
            var y = point?.Count > 1 ? point[1]?.ToString() : "?";
            var angle = candidate["angleDegrees"]?.ToString() ?? "?";
            RhinoApp.WriteLine($"  {id}: angle {angle}, point ({x}, {y})");
        }

        var get = new GetOption();
        get.SetCommandPrompt("Select candidate");
        var optionMap = new Dictionary<int, string>();
        foreach (var node in candidates)
        {
            if (node is not JsonObject candidate)
                continue;

            var id = candidate["candidateId"]?.ToString();
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var optionName = id.Replace("-", "_", StringComparison.Ordinal);
            var index = get.AddOption(optionName);
            optionMap[index] = id;
        }

        var allOption = get.AddOption("All");
        var result = get.Get();
        if (result != global::Rhino.Input.GetResult.Option)
            return false;

        if (get.Option().Index == allOption)
        {
            candidateMode = "all";
            candidateId = null;
            return true;
        }

        if (!optionMap.TryGetValue(get.Option().Index, out var selectedCandidateId))
            return false;

        candidateMode = "single";
        candidateId = selectedCandidateId;
        return true;
    }

    private static bool PromptForRequiredSides(
        JsonObject data,
        ref string asymmetricSideA,
        ref string asymmetricSideB)
    {
        var required = data["requiredSideSelections"]?.AsArray();
        if (required == null || required.Count == 0)
            return false;

        foreach (var entryNode in required)
        {
            if (entryNode is not JsonObject entry)
                continue;

            var road = entry["road"]?.ToString();
            if (string.IsNullOrWhiteSpace(road))
                continue;

            var current = road == "A" ? asymmetricSideA : asymmetricSideB;
            if (!PromptForSide($"Select asymmetric side for road {road}", ref current))
                return false;

            if (road == "A")
                asymmetricSideA = current;
            else if (road == "B")
                asymmetricSideB = current;
        }

        return true;
    }

    private static bool PromptForSide(string prompt, ref string side)
    {
        var get = new GetOption();
        get.SetCommandPrompt(prompt);
        var leftOption = get.AddOption("Left");
        var rightOption = get.AddOption("Right");
        get.SetDefaultString(string.Equals(side, "left", StringComparison.OrdinalIgnoreCase) ? "Left" : "Right");

        var result = get.Get();
        if (result == global::Rhino.Input.GetResult.Nothing)
            return true;
        if (result != global::Rhino.Input.GetResult.Option)
            return false;

        side = get.Option().Index == leftOption ? "left" : "right";
        return true;
    }

    private static void WriteResolveSummary(JsonObject data, string targetLayerRoot)
    {
        var successCount = data["successfulCandidateCount"]?.GetValue<int>() ?? 0;
        var failCount = data["failedCandidateCount"]?.GetValue<int>() ?? 0;
        RhinoApp.WriteLine($"RC_ResolveIntersection: {successCount} candidate(s) succeeded, {failCount} failed.");
        RhinoApp.WriteLine($"  Layer root: {targetLayerRoot}");

        if (data["results"] is not JsonArray results)
            return;

        foreach (var entryNode in results)
        {
            if (entryNode is not JsonObject entry)
                continue;

            var candidateId = entry["candidateId"]?.ToString() ?? "(unknown)";
            var commit = entry["commit"]?.AsObject();
            if (commit == null)
            {
                var error = entry["error"]?.ToString() ?? "No commit result.";
                RhinoApp.WriteLine($"  {candidateId}: {error}");
                continue;
            }

            var created = commit["created"]?.AsObject();
            var summaries = commit["summaries"]?.AsObject();
            RhinoApp.WriteLine(
                $"  {candidateId}: boundary {created?["realizedBoundaryId"]}, surface {created?["realizedSurfaceId"]}, " +
                $"area {summaries?["surfaceArea"]}, boundary length {summaries?["boundaryLineLength"]}");
        }
    }

    private static string ResolveNativeBaseUri()
    {
        try
        {
            var discoveryFolder = Path.Combine(Path.GetTempPath(), "rook");
            var currentPid = System.Environment.ProcessId;
            var manifestPath = Path.Combine(discoveryFolder, $"instance-{currentPid}-native.json");
            if (File.Exists(manifestPath))
            {
                var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject();
                if (manifest?["port"]?.GetValue<int>() is int port && port > 0)
                    return $"http://localhost:{port}";
            }
        }
        catch
        {
        }

        return "http://localhost:9950";
    }

    private static string BuildTargetLayerRoot()
    {
        return $"RoadCreator::ResolveIntersection_{DateTime.Now:yyyyMMdd_HHmmss}";
    }

    private static JsonNode PostJson(string url, JsonObject body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        using var response = Http.Send(request);
        var payload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(payload))
            throw new InvalidOperationException($"Empty response from {url}.");

        return JsonNode.Parse(payload) ?? throw new InvalidOperationException($"Invalid JSON response from {url}.");
    }
}
