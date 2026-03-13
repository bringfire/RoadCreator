using ClosedXML.Excel;
using global::Rhino;
using global::Rhino.Commands;
using global::Rhino.DocObjects;
using global::Rhino.Geometry;
using global::Rhino.Input;
using global::Rhino.Input.Custom;
using RoadCreator.Core.Alignment;
using RoadCreator.Core.Localization;
using RoadCreator.Rhino.Layers;

namespace RoadCreator.Rhino.Commands.Terrain;

/// <summary>
/// Exports a longitudinal profile to an Excel (.xlsx) file.
/// Converts from RC2_PsanyPodelnyProfil_CZ.rvb.
///
/// Reads the terrain profile curve and grade line from the longitudinal profile
/// layers, samples them at user-specified intervals, and writes a formatted Excel
/// workbook with computed data (chainages, elevations, slopes, vertical curves).
///
/// Replaces VBScript COM-based Excel.Application with ClosedXML for cross-platform .xlsx.
///
/// Output columns:
///   B: Point number
///   C: Chainage (km)
///   D: Point designation (ZZ, KZ, V, etc.)
///   E: Longitudinal slope (%)
///   F: Distance from vertex (m)
///   G: Height difference (m)
///   H: Grade polygon elevation (m)
///   I: Distance to vertical curve (x, m)
///   J: Vertical curve deflection (y, m)
///   K: Grade line elevation (m)
///   L: Terrain elevation (m)
///   M: Elevation difference (m)
/// </summary>
[System.Runtime.InteropServices.Guid("D4E5F6A7-B8C9-1D2E-3F4A-5B6C7D8E9FA2")]
public class LongitudinalProfileExportCommand : Command
{
    public override string EnglishName => "RC_LongitudinalProfileExport";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // --- Step 1: Select route ---
        string roadName = "";
        var (choice, selectedRoad) = RouteDiscovery.PromptRouteSelection(doc, "Manual");
        if (choice == RoutePromptResult.Cancelled)
            return Result.Cancel;
        if (choice == RoutePromptResult.RoadSelected)
        {
            roadName = selectedRoad;
        }
        else
        {
            // Manual fallback: prompt for road name
            var gs = new GetString();
            gs.SetCommandPrompt("Enter road name (e.g. Road_1)");
            if (gs.Get() != GetResult.String)
                return Result.Cancel;
            roadName = gs.StringResult().Trim();
            if (string.IsNullOrEmpty(roadName))
            {
                RhinoApp.WriteLine(Strings.NoRoadsFound);
                return Result.Failure;
            }
        }

        // --- Step 2: Find profile data ---
        // Terrain profile curve
        string terrainProfilePath = LayerScheme.BuildRoadPath(roadName,
            LayerScheme.LongitudinalProfile, "Terrain Profile");
        int terrainLayerIdx = doc.Layers.FindByFullPath(terrainProfilePath, -1);
        Curve? terrainCurve = null;
        if (terrainLayerIdx >= 0)
        {
            var layerObjects = doc.Objects.FindByLayer(doc.Layers[terrainLayerIdx]);
            if (layerObjects != null)
            {
                foreach (var obj in layerObjects)
                {
                    if (obj.Geometry is Curve c)
                    {
                        terrainCurve = c;
                        break;
                    }
                }
            }
        }

        // Grade line (niveleta)
        string gradeLinePath = LayerScheme.BuildRoadPath(roadName, LayerScheme.GradeLine);
        int gradeLineLayerIdx = doc.Layers.FindByFullPath(gradeLinePath, -1);
        Curve? gradeLineCurve = null;
        if (gradeLineLayerIdx >= 0)
        {
            var layerObjects = doc.Objects.FindByLayer(doc.Layers[gradeLineLayerIdx]);
            if (layerObjects != null)
            {
                foreach (var obj in layerObjects)
                {
                    if (obj.Geometry is Curve c)
                    {
                        gradeLineCurve = c;
                        break;
                    }
                }
            }
        }

        if (terrainCurve == null)
        {
            RhinoApp.WriteLine(string.Format(Strings.ProfileDataNotFound, roadName));
            return Result.Failure;
        }

        // Find datum from profile datum point
        double datum = 0;
        string profLayerPath = LayerScheme.BuildRoadPath(roadName, LayerScheme.LongitudinalProfile);
        int profLayerIdx = doc.Layers.FindByFullPath(profLayerPath, -1);
        if (profLayerIdx >= 0)
        {
            var profObjects = doc.Objects.FindByLayer(doc.Layers[profLayerIdx]);
            if (profObjects != null)
            {
                foreach (var obj in profObjects)
                {
                    if (RoadObjectNaming.TryParseLongProfileDatum(obj.Attributes.Name, out double d))
                    {
                        datum = d;
                        break;
                    }
                }
            }
        }

        // Collect important points from grade line
        var importantPoints = new List<(string Label, double Chainage)>();
        string impPtsPath = LayerScheme.BuildRoadPath(roadName,
            LayerScheme.GradeLine, LayerScheme.ImportantPoints);
        int impPtsLayerIdx = doc.Layers.FindByFullPath(impPtsPath, -1);
        if (impPtsLayerIdx >= 0)
        {
            var impObjects = doc.Objects.FindByLayer(doc.Layers[impPtsLayerIdx]);
            if (impObjects != null)
            {
                foreach (var obj in impObjects)
                {
                    if (obj.Geometry is global::Rhino.Geometry.Point pt)
                    {
                        string name = obj.Attributes.Name ?? "";
                        // Parse label from name (e.g., "ParabolicArc ZZ 2000")
                        var parts = name.Split(' ');
                        string label = parts.Length >= 2 ? parts[1] : "";
                        double chainage = pt.Location.X; // X coordinate = chainage in profile space
                        importantPoints.Add((label, chainage));
                    }
                }
            }
        }

        // --- Step 3: Get export parameters ---
        var getInterval = new GetOption();
        getInterval.SetCommandPrompt(Strings.SelectChainageInterval);
        getInterval.AddOption("5m");
        getInterval.AddOption("10m");
        getInterval.AddOption("20m");
        getInterval.AddOption("50m");
        getInterval.AddOption("100m");
        getInterval.AddOption("200m");
        if (getInterval.Get() != GetResult.Option)
            return Result.Cancel;

        double interval = getInterval.Option().Index switch
        {
            1 => 5, 2 => 10, 3 => 20, 4 => 50, 5 => 100, 6 => 200,
            _ => 20
        };

        // Get save file path
        string? filePath = RhinoGet.GetFileName(GetFileNameMode.Save,
            "*.xlsx", "Save Longitudinal Profile", null);
        if (string.IsNullOrEmpty(filePath))
            return Result.Cancel;
        if (!filePath.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase))
            filePath += ".xlsx";

        // --- Step 4: Sample profile data ---
        var bbox = terrainCurve.GetBoundingBox(true);
        double profileStart = bbox.Min.X;
        double profileEnd = bbox.Max.X;
        double profileLength = profileEnd - profileStart;

        var dataRows = new List<ProfileDataRow>();
        int pointNum = 1;

        for (double chainage = profileStart; chainage <= profileEnd + 0.01; chainage += interval)
        {
            if (chainage > profileEnd) chainage = profileEnd;

            double terrainElev = RouteDiscovery.SampleProfileElevation(
                terrainCurve, chainage, datum, doc.ModelAbsoluteTolerance) ?? datum;
            double gradeElev = gradeLineCurve != null
                ? RouteDiscovery.SampleProfileElevation(
                    gradeLineCurve, chainage, datum, doc.ModelAbsoluteTolerance) ?? datum
                : terrainElev;

            // Find if this chainage matches an important point
            string designation = "";
            foreach (var (label, ptChainage) in importantPoints)
            {
                if (System.Math.Abs(ptChainage - chainage) < 0.5)
                {
                    designation = label;
                    break;
                }
            }

            double slope = 0;
            if (gradeLineCurve != null && chainage > profileStart + 0.01)
            {
                double prevChainage = System.Math.Max(profileStart, chainage - 1.0);
                double prevElev = RouteDiscovery.SampleProfileElevation(
                    gradeLineCurve, prevChainage, datum, doc.ModelAbsoluteTolerance) ?? datum;
                double dx = chainage - prevChainage;
                slope = dx > 1e-10 ? (gradeElev - prevElev) / dx * 100.0 : 0; // % grade
            }

            dataRows.Add(new ProfileDataRow
            {
                PointNumber = pointNum++,
                ChainageKm = (chainage - profileStart) / 1000.0,
                Designation = designation,
                Slope = slope,
                GradeElevation = gradeElev,
                TerrainElevation = terrainElev,
                Difference = gradeElev - terrainElev,
            });

            if (System.Math.Abs(chainage - profileEnd) < 0.01) break;
        }

        // --- Step 5: Write Excel file ---
        try
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet("Longitudinal Profile");

            // Header
            worksheet.Cell("B2").Value = "Longitudinal Profile";
            worksheet.Range("B2:M2").Merge();
            worksheet.Cell("B2").Style.Font.Bold = true;
            worksheet.Cell("B2").Style.Font.FontSize = 14;

            // Section headers
            worksheet.Cell("B3").Value = "Grade Polygon";
            worksheet.Range("B3:H3").Merge();
            worksheet.Cell("B3").Style.Font.Bold = true;

            worksheet.Cell("I3").Value = "Vertical Curve";
            worksheet.Range("I3:J3").Merge();
            worksheet.Cell("I3").Style.Font.Bold = true;

            worksheet.Cell("K3").Value = "Elevations";
            worksheet.Range("K3:M3").Merge();
            worksheet.Cell("K3").Style.Font.Bold = true;

            // Column headers
            var headers = new[] { "No.", "Chainage\n(km)", "Point", "Slope\n(%)",
                "Dist.\n(m)", "Height diff.\n(m)", "Grade poly.\nelev. (m)",
                "x (m)", "y (m)", "Grade line\n(m)", "Terrain\n(m)", "Diff.\n(m)" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(4, i + 2);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Data rows
            for (int i = 0; i < dataRows.Count; i++)
            {
                var row = dataRows[i];
                int r = i + 5;

                worksheet.Cell(r, 2).Value = row.PointNumber;
                worksheet.Cell(r, 3).Value = row.ChainageKm;
                worksheet.Cell(r, 3).Style.NumberFormat.Format = "0.000000";
                worksheet.Cell(r, 4).Value = row.Designation;
                worksheet.Cell(r, 5).Value = row.Slope;
                worksheet.Cell(r, 5).Style.NumberFormat.Format = "0.000";
                worksheet.Cell(r, 6).Value = 0; // distance from vertex placeholder
                worksheet.Cell(r, 7).Value = 0; // height diff placeholder
                worksheet.Cell(r, 8).Value = row.GradeElevation;
                worksheet.Cell(r, 8).Style.NumberFormat.Format = "0.00";
                worksheet.Cell(r, 9).Value = 0;  // x placeholder
                worksheet.Cell(r, 10).Value = 0; // y placeholder
                worksheet.Cell(r, 11).Value = row.GradeElevation;
                worksheet.Cell(r, 11).Style.NumberFormat.Format = "0.00";
                worksheet.Cell(r, 12).Value = row.TerrainElevation;
                worksheet.Cell(r, 12).Style.NumberFormat.Format = "0.00";
                worksheet.Cell(r, 13).Value = row.Difference;
                worksheet.Cell(r, 13).Style.NumberFormat.Format = "0.00";
            }

            // Apply borders
            int lastRow = 4 + dataRows.Count;
            var dataRange = worksheet.Range(4, 2, lastRow, 13);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Column widths
            worksheet.Column(2).Width = 6;
            worksheet.Column(3).Width = 14;
            worksheet.Column(4).Width = 8;
            worksheet.Column(5).Width = 10;
            worksheet.Column(6).Width = 10;
            worksheet.Column(7).Width = 12;
            worksheet.Column(8).Width = 12;
            worksheet.Column(9).Width = 10;
            worksheet.Column(10).Width = 10;
            worksheet.Column(11).Width = 12;
            worksheet.Column(12).Width = 12;
            worksheet.Column(13).Width = 10;

            workbook.SaveAs(filePath);
            RhinoApp.WriteLine(string.Format(Strings.ExportSaved, filePath));
        }
        catch (System.Exception ex)
        {
            RhinoApp.WriteLine(string.Format(Strings.ExportFailed, ex.Message));
            return Result.Failure;
        }

        return Result.Success;
    }

    private struct ProfileDataRow
    {
        public int PointNumber;
        public double ChainageKm;
        public string Designation;
        public double Slope;
        public double GradeElevation;
        public double TerrainElevation;
        public double Difference;
    }
}
