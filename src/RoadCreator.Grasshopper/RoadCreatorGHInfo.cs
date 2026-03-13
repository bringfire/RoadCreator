using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace RoadCreator.Grasshopper;

public class RoadCreatorGHInfo : GH_AssemblyInfo
{
    public override string Name => "RoadCreator";
    public override string Description => "Road design toolkit — alignment, profiles, intersections, terrain, and accessories.";
    public override Guid Id => new("7C4E8A2F-3D19-4B5E-A6F1-9E2D7C8B4A3F");
    public override string AuthorName => "RoadCreator";
    public override string AuthorContact => "";
    public override string Version => "0.1.0";
    public override Bitmap? Icon => null;
}
