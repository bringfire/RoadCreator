# Key Research Findings: Road Intersection Geometry from Centerlines

## Paper: "Method of Extracting Intersection Polygons Based on Centerline Junctions" (Cai et al., 2024)
- **Source**: Sensors and Materials, Vol. 36, No. 10 (2024), pp. 4441-4453
- **URL**: https://sensors.myu-group.co.jp/sm_pdf/SM3810.pdf
- **Algorithm Steps**:
  1. Topological preprocessing of road centerline to extract dangling nodes; convert road polygon data into line rings
  2. Match the nearest line ring within a certain threshold range of dangling nodes to obtain splitting points
  3. Calculate the set of splitting points associated with the line rings; divide the line rings to obtain boundary arcs
  4. Extract the boundary arcs associated with the current intersection from the intersection nodes of the road centerline
  5. Calculate the primary nearest point set and secondary nearest point set in turn
  6. Use the secondary nearest point set to extract the resulting intersection polygons
- **Key Insight**: Works on existing road surface polygons + centerlines; splits road polygons at intersection nodes

## Paper: "From road centrelines to carriageways—A reconstruction algorithm" (Vitalis et al., 2022)
- **Source**: PLoS ONE, DOI: 10.1371/journal.pone.0262801
- **GitHub**: https://github.com/tudelft3d/carriageways-creator
- **Algorithm**: Uses OSM centerlines + areal dataset (existing road polygons) to reconstruct carriageways
- **Tools**: Python, geopandas, osmnx
- **Key Insight**: Requires existing road polygon data as input alongside centerlines

## Paper: "Reconstructing a high-detailed 2D areal representation of road network from OSM data" (Rao, 2024)
- **Source**: MSc thesis, TU Delft, October 2024
- **URL**: https://3d.bk.tudelft.nl/ken/files/24_chengzhi.pdf
- **Key Insight**: Lane-level road networks from OSM centerlines; intersection geometry with realistic corner shapes

## A/B Street osm2streets (Rust library)
- **GitHub**: https://github.com/a-b-street/osm2streets
- **Documentation**: https://a-b-street.github.io/docs/tech/map/geometry/index.html
- **Algorithm (3 phases)**:
  1. **Thicken roads**: Offset centerline left and right by half-width to get road boundary polylines
  2. **Trim roads**: For each pair of roads meeting at an intersection, find where their thickened boundaries overlap and trim back
  3. **Clockwise walk**: Walk clockwise around the intersection to generate the intersection polygon
- **Language**: Rust
- **Key Insight**: Most complete open-source implementation of the full algorithm

## CavalierContours (C++ / Rust)
- **GitHub**: https://github.com/jbuckmccready/CavalierContours
- **Key Features**: 2D polyline offset with arc segments, handles self-intersections, uses arc joins between offset segments
- **Language**: C++ with Rust port

## Offroad (Rust)
- **GitHub**: https://github.com/radevgit/offroad
- **Key Features**: 2D offsetting for arc-segment polylines/polygons
- **Language**: Rust

## Clipper2 / pyclipr
- **GitHub**: https://github.com/AngusJohnson/Clipper2
- **Python bindings**: https://github.com/drlukeparry/pyclipr
- **Key Features**: Polygon clipping (union, difference, intersection, XOR) + polygon offsetting
- **Language**: C++ with Python, C#, Delphi bindings

## CGAL Straight Skeleton and Polygon Offsetting
- **Docs**: https://doc.cgal.org/latest/Straight_skeleton_2/index.html
- **Key Features**: Straight skeleton for 2D polygons with holes; polygon offsetting via skeleton
- **Language**: C++

## GEOS / Shapely (libgeos)
- **GitHub**: https://github.com/libgeos/geos
- **Python**: Shapely (wraps GEOS)
- **Key Features**: Buffer/offset curves for lines, polygon boolean operations
- **Language**: C++ (GEOS), Python (Shapely)

## JTS Topology Suite
- **GitHub**: https://github.com/locationtech/jts
- **Key Features**: Java spatial operations library; buffer, union, difference, intersection; Polygonizer class
- **Language**: Java

## StreetGen (Cura et al., 2018)
- **Paper**: https://arxiv.org/abs/1801.05741
- **Key Features**: City-scale procedural generation of streets from GIS data; intersection surface model
- **Language**: SQL/PostGIS-based

## OSM SidewalKreator (QGIS Plugin)
- **GitHub**: https://github.com/kauevestena/osm_sidewalkreator
- **Key Features**: Creates sidewalk geometries from OSM streets; intersection geometry handling
- **Language**: Python (QGIS plugin)

## Interactive Procedural Street Modeling (Chen et al., SIGGRAPH 2008)
- **Paper**: https://web.engr.oregonstate.edu/~zhange/images/street_sig08.pdf
- **Key Features**: Tensor field-based road network generation; intersection polygon from road graph
- **Language**: C++ (research code)

## KTH Thesis: OSM-Based Automatic Road Network Geometry Generation in Unity (Yu, 2019)
- **URL**: https://kth.diva-portal.org/smash/get/diva2:1375175/FULLTEXT01.pdf
- **Key Features**: 3-stage method: OSM data → road segments → intersection meshes; polygon-buffer structure for intersections
- **Language**: C# (Unity)
