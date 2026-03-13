# Programmatic 2D Road Intersection Generation: A Guide to Open-Source Solutions

**Author:** Manus AI
**Date:** March 10, 2026

## Introduction

The automated generation of accurate and realistic 2D road intersections from centerline data is a complex challenge in computational geometry, with significant applications in urban planning, traffic simulation, and geographic information systems (GIS). The problem involves translating simple centerline representations of roads, which define their path and connectivity, into detailed polygonal surfaces that accurately model the physical space of an intersection, including road edges, sidewalks, and curb returns. This guide provides a comprehensive overview of the open-source libraries, algorithms, and academic research available for tackling this problem, with a focus on practical implementation and adaptation for CAD and GIS workflows.

The core of the challenge lies in resolving the geometry where multiple road profiles, defined as offsets from their respective centerlines, meet. This requires not only the generation of offset curves but also sophisticated methods for trimming, joining, and filleting these curves to create a topologically correct and geometrically plausible intersection polygon. The provided CAD image of a masterplan intersection, with its complex arrangement of centerlines, road edges, crosswalks, and curb radii, serves as a prime example of the desired output.

This document will delve into the fundamental algorithms that form the basis of most intersection generation techniques, explore a wide range of open-source libraries across different programming languages that implement these algorithms, and summarize key academic papers and research that offer further insights and advanced methods. The focus remains strictly on open-source and freely available resources, providing developers and researchers with the tools and knowledge necessary to build their own solutions or adapt existing ones.

## Core Algorithms

The programmatic generation of 2D road intersections from centerline data can be broken down into a series of geometric operations. While specific implementations vary, a common and effective algorithmic pattern has emerged, which can be described as a three-phase process: **thicken**, **trim**, and **walk**. This section details this core algorithm, along with the fundamental geometry of creating filleted curb returns.

### The "Thicken, Trim, and Clockwise Walk" Algorithm

One of the most well-documented and complete open-source implementations of a full intersection generation algorithm is found in the `osm2streets` library, which is part of the A/B Street traffic simulation project [1]. Their detailed explanation [2] provides an excellent foundation for understanding the process.

#### Phase 1: Thicken Roads

The initial input consists of road centerlines, which are essentially 1D polylines. The first step is to "thicken" these centerlines into 2D polygons that represent the full width of the road. This is achieved through a geometric operation known as **offsetting** or **buffering**.

> The road centerline is offset to both the left and the right by a distance equal to half of the road's total width. This creates two new polylines that represent the road's edges. These two edge polylines, along with connecting segments at the ends, form a closed polygon for each road approaching the intersection.

The calculation of the offset curve for a polyline is a non-trivial task, especially when dealing with sharp corners and self-intersections. A robust polyline offsetting algorithm, such as the one described by Rye Terrell [3] and implemented in libraries like CavalierContours [4], is crucial. This process typically involves:

1.  **Offsetting individual segments:** Each straight or arc segment of the centerline is offset individually.
2.  **Resolving joins:** At the vertices of the polyline, the offset segments will either overlap or have a gap. These must be resolved by trimming them to their intersection point (a miter join) or by adding a new segment to bridge the gap (a bevel or round join).

#### Phase 2: Trim Roads

After thickening, the road polygons from different directions will overlap in the area of the intersection. The next phase is to trim these polygons back to create a clear, empty space where the final intersection polygon will be constructed.

> The core idea is to find the intersection points between the offset boundary polylines of every pair of roads that meet at the junction. These collision points define the extent of the overlap. Each road is then "trimmed" back to a line defined by these collision points, effectively removing the overlapping portions.

The `osm2streets` documentation explains this process in detail, using the collision points to determine how far back along the original centerline each road should be cut [2]. The goal is to have the road edges terminate cleanly at the boundary of the not-yet-created intersection polygon.

#### Phase 3: Clockwise Walk

With the overlapping road sections removed, a void is left in the middle. The final phase is to construct the intersection polygon that fills this void. This is accomplished by a "clockwise walk."

> The algorithm gathers all of the endpoints of the trimmed road edges that now form the boundary of the intersection void. It also includes the original collision points that were used for trimming. These points are then sorted in a clockwise order around a central point (often the original intersection point of the centerlines). By connecting these sorted points in sequence, a closed polygon representing the intersection surface is formed.

This method ensures that the resulting polygon perfectly abuts the trimmed road sections, creating a seamless and topologically correct road network.

### Curb Return and Fillet Geometry

A key detail in creating realistic intersections is the generation of curved curb returns (fillets) instead of sharp, angular corners. This is a standard geometric construction problem.

Given two intersecting lines (representing the road edges at a corner) and a desired radius `r`, the goal is to find the circular arc that is tangent to both lines. The solution involves the following steps, as described in a Stack Exchange discussion on the topic [5]:

1.  **Find the Angle:** Calculate the angle `θ` between the two intersecting road edge lines.
2.  **Calculate Tangent Distance:** The distance from the intersection point of the two lines to the tangent points (where the arc begins and ends) is given by the formula: `d = r / tan(θ / 2)`.
3.  **Locate Tangent Points:** Measure this distance `d` back from the intersection point along each of the two lines. These are the start and end points of the fillet arc.
4.  **Find the Arc Center:** The center of the fillet arc lies on the angle bisector of the two lines, at a distance of `r / sin(θ / 2)` from the intersection point. Alternatively, it can be found by constructing lines perpendicular to the original road edges at the tangent points; the intersection of these perpendiculars is the arc center.

This procedure allows for the creation of smooth, rounded corners that accurately model real-world curb geometry.

## Open-Source Libraries for Road Geometry

A variety of open-source libraries provide the fundamental tools required for generating road intersection geometry. These libraries offer functionalities ranging from basic polygon offsetting and boolean operations to more comprehensive solutions that handle complex road network data. The choice of library will often depend on the specific programming language of the project and the level of abstraction required.

The following table summarizes some of the most relevant and powerful libraries available for this task.

| Library | Language(s) | Key Features | Repository |
| :--- | :--- | :--- | :--- |
| **osm2streets** | Rust | Complete intersection geometry generation from OpenStreetMap (OSM) data; implements the "thicken, trim, walk" algorithm; produces lane-level detail. | [a-b-street/osm2streets](https://github.com/a-b-street/osm2streets) [1] |
| **CavalierContours** | C++, Rust | Robust 2D polyline offsetting with arc support; handles self-intersections; provides boolean operations (union, intersection, etc.) for closed polylines. | [jbuckmccready/CavalierContours](https://github.com/jbuckmccready/CavalierContours) [4] |
| **flatten-js** | JavaScript | Comprehensive 2D geometry library for manipulating shapes including points, lines, arcs, and polygons; includes modules for polygon offsetting and boolean operations. | [alexbol99/flatten-js](https://github.com/alexbol99/flatten-js) [6] |
| **Shapely (GEOS)** | Python (C++) | Python wrapper for the powerful GEOS library; provides extensive geometric operations including buffering (offsetting), intersection, union, and other set-theoretic analyses. | [shapely/shapely](https://github.com/shapely/shapely) [7] |
| **Clipper2** | C++, C#, Delphi | A highly robust and widely used library for polygon clipping (intersection, union, difference, XOR) and offsetting. Known for its speed and reliability. | [AngusJohnson/Clipper2](https://github.com/AngusJohnson/Clipper2) [8] |
| **JTS Topology Suite** | Java | A mature and powerful Java library for 2D spatial operations, providing a complete model for geometric objects and a rich set of functions, including buffering and boolean operations. | [locationtech/jts](https://github.com/locationtech/jts) [9] |
| **CGAL** | C++ | The Computational Geometry Algorithms Library is a massive collection of efficient and reliable geometric algorithms, including straight skeleton generation for polygon offsetting. | [CGAL/cgal](https://github.com/CGAL/cgal) [10] |
| **StreetGen** | SQL (PostGIS) | A research project that uses a database-centric approach (PostgreSQL/PostGIS) for procedural road generation, including an intersection surface model based on buffering and cutting road axes. | [Paper Link](https://ar5iv.labs.arxiv.org/html/1801.05741) [11] |

### Library Deep Dive

While the table above provides a high-level overview, a deeper look into a few key libraries reveals their specific strengths and approaches to the problem of intersection generation.

#### osm2streets (Rust)

For a project that requires a complete, end-to-end solution for generating road networks from real-world data, `osm2streets` stands out. It is not just a geometry library but a full pipeline for converting OpenStreetMap data into a detailed, lane-accurate map model. Its implementation of the "thicken, trim, and clockwise walk" algorithm is robust and handles a vast number of edge cases found in messy real-world data. Because it is written in Rust, it offers excellent performance, which is critical when processing large map areas. The primary advantage of `osm2streets` is that it solves the entire problem, from raw data parsing to final polygon generation, including lane markings and turn lanes.

#### CavalierContours (C++/Rust)

If the goal is to build a custom solution or integrate robust offsetting capabilities into an existing application, `CavalierContours` is an excellent choice. Its core strength is its high-quality implementation of polyline offsetting. Unlike many libraries that approximate arcs with straight line segments, `CavalierContours` treats them as true arcs, which is essential for accurately representing the curved profiles in CAD data. It also provides the necessary boolean operations (union, intersection, difference) to combine the offset shapes and construct the final intersection polygon. Its availability in both C++ and Rust makes it versatile for high-performance applications.

#### Shapely (Python)

For developers working in the Python ecosystem, particularly in GIS and data science, Shapely is the de facto standard for geometric operations. It provides a clean, Pythonic interface to the underlying GEOS library. The workflow for generating an intersection using Shapely would involve:

1.  **Buffering:** Using the `buffer()` method on the centerline `LineString` objects to create the thickened road `Polygon` objects.
2.  **Union and Difference:** Using `unary_union()` to merge all the road polygons into a single shape, and then using `difference()` to subtract this from a larger polygon representing the intersection's bounding box, or by using a sequence of intersection and union operations to build up the final polygon piece by piece.

Shapely's power lies in its integration with the broader scientific Python stack, including libraries like GeoPandas, which simplifies the handling of large geospatial datasets.

#### flatten-js (JavaScript)

For web-based applications, `flatten-js` provides a comprehensive and well-structured set of tools for 2D geometry. It has a rich object model that includes not just polygons but also arcs, which is crucial for this task. The library is modular, with separate packages for core geometry, polygon offsetting (`@flatten-js/polygon-offset`), and boolean operations (`@flatten-js/boolean-op`). This allows developers to include only the functionality they need, keeping the application lightweight. A typical workflow in `flatten-js` would mirror the core algorithm: create offset polygons, use boolean operations to trim and merge them, and construct the final intersection shape.

## Academic Research and Advanced Techniques

Beyond the established libraries, academic research provides deeper insights into the algorithms and explores more advanced and robust methods for road network generation. These papers often contain detailed pseudocode and theoretical foundations that can be invaluable for developing custom solutions.

### Database-Centric Approach: StreetGen

The **StreetGen** project presents a unique approach by implementing the entire road generation workflow within a relational database management system (RDBMS), specifically PostgreSQL with the PostGIS extension [11]. This method leverages the power of SQL for set-based operations and the robust geometric functions of PostGIS.

> The core of the StreetGen intersection algorithm is based on morphological and boolean operations. Instead of explicit calculations, it finds the center of the curb return arc by finding the intersection of the boundaries of two buffered road axes. The final intersection surface is then constructed using PostGIS's `ST_BuildArea` function on the collection of trimmed road surfaces and the generated corner arcs.

This approach is notable for its robustness and scalability. By encapsulating the logic in database functions, it can process entire city-scale networks efficiently and concurrently.

### Centerline-to-Carriageway Reconstruction

Several papers from TU Delft focus on the problem of reconstructing full carriageway polygons from simple centerlines, often using existing areal datasets (such as building footprints or land use polygons) to inform the process. The "From road centrelines to carriageways" paper and its associated `carriageways-creator` repository [12] provide a methodology that relies on `osmnx` and `geopandas` to merge OSM data with external polygon data to define road widths and boundaries.

A more recent master's thesis from the same group, "Reconstructing a high-detailed 2D areal representation of road network from OSM data" [13], delves into generating lane-level detail and creating realistic corner shapes at intersections, providing a wealth of algorithmic detail.

### Spline-Based Road Generation

For applications requiring smooth, spline-based road networks, such as in video games or high-fidelity simulations, the challenge of creating intersections between curved roads becomes more complex. A bachelor's thesis, "Finding Junctions in Spline-based Road Generation" [14], explores this specific problem. It discusses using the convex hull property of Bézier curves to simplify the intersection problem and leverages algorithms like the Separating Axis Theorem (SAT) and polygon clipping to find and resolve overlaps between the road meshes generated from the splines.

## Conclusion

The programmatic generation of 2D road intersections from centerline data is a solvable, albeit complex, problem. The research reveals that a well-defined algorithmic pattern—thickening centerlines via offsetting, trimming the resulting overlapping polygons, and constructing the final intersection polygon from the remaining edges—serves as the foundation for most successful implementations. The primary challenges lie in the robustness of the geometric operations, particularly polyline offsetting and boolean calculations, which must handle a wide variety of edge cases presented by real-world data.

A rich ecosystem of open-source libraries is available to tackle this challenge. For a comprehensive, out-of-the-box solution, **`osm2streets`** in Rust is the most complete, handling the entire pipeline from raw OSM data to detailed, lane-accurate intersection polygons. For developers seeking to build custom solutions, libraries like **CavalierContours** (C++/Rust), **Shapely** (Python), and **flatten-js** (JavaScript) provide the essential, high-quality geometric primitives for offsetting and boolean operations.

Academic research, particularly from the A/B Street project, TU Delft, and the StreetGen paper, offers invaluable, in-depth algorithmic details and advanced techniques. These resources provide the theoretical underpinnings and pseudocode necessary for developing novel or highly specialized intersection generation systems.

Ultimately, the choice of tools and the depth of implementation will depend on the specific requirements of the project. Whether adapting an existing library or building a new system from scratch, the resources and algorithms outlined in this guide provide a solid foundation for programmatically creating the complex and detailed road intersection geometry required for modern CAD and GIS applications.

## References

[1] a-b-street, “osm2streets,” GitHub. [Online]. Available: [https://github.com/a-b-street/osm2streets](https://github.com/a-b-street/osm2streets)

[2] D. Carlino, “Deep dive into devilish details: Intersection geometry,” A/B Street, Sep. 2021. [Online]. Available: [https://a-b-street.github.io/docs/tech/map/geometry/index.html](https://a-b-street.github.io/docs/tech/map/geometry/index.html)

[3] R. Terrell, “The offset polygon problem, part 1,” Rye Terrell's blog, 2011. [Online]. Available: [https://ryeterrell.com/2011/03/13/the-offset-polygon-problem-part-1/](https://ryeterrell.com/2011/03/13/the-offset-polygon-problem-part-1/)

[4] jbuckmccready, “CavalierContours,” GitHub. [Online]. Available: [https://github.com/jbuckmccready/CavalierContours](https://github.com/jbuckmccready/CavalierContours)

[5] “Geometric construction of a fillet?,” Mathematics Stack Exchange, Nov. 20, 2019. [Online]. Available: [https://math.stackexchange.com/questions/3444019/geometric-construction-of-a-fillet](https://math.stackexchange.com/questions/3444019/geometric-construction-of-a-fillet)

[6] alexbol99, “flatten-js,” GitHub. [Online]. Available: [https://github.com/alexbol99/flatten-js](https://github.com/alexbol99/flatten-js)

[7] Shapely contributors, “shapely,” GitHub. [Online]. Available: [https://github.com/shapely/shapely](https://github.com/shapely/shapely)

[8] A. Johnson, “Clipper2,” GitHub. [Online]. Available: [https://github.com/AngusJohnson/Clipper2](https://github.com/AngusJohnson/Clipper2)

[9] LocationTech, “JTS Topology Suite,” GitHub. [Online]. Available: [https://github.com/locationtech/jts](https://github.com/locationtech/jts)

[10] The CGAL Project, “The Computational Geometry Algorithms Library,” GitHub. [Online]. Available: [https://github.com/CGAL/cgal](https://github.com/CGAL/cgal)

[11] R. Cura, J. Perret, and N. Paparoditis, “StreetGen : In base city scale procedural generation of streets: road network, road surface and street objects,” arXiv:1801.05741 [cs], Jan. 2018. [Online]. Available: [https://ar5iv.labs.arxiv.org/html/1801.05741](https://ar5iv.labs.arxiv.org/html/1801.05741)

[12] S. Vitalis, A. Labetski, H. Ledoux, and J. Stoter, “From road centrelines to carriageways—A reconstruction algorithm,” PLoS ONE, vol. 17, no. 2, p. e0262801, Feb. 2022. [Online]. Available: [https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0262801](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0262801)

[13] C. Rao, “Reconstructing a high-detailed 2D areal representation of road network from OSM data,” M.S. thesis, Delft University of Technology, Delft, Netherlands, 2024. [Online]. Available: [https://3d.bk.tudelft.nl/ken/files/24_chengzhi.pdf](https://3d.bk.tudelft.nl/ken/files/24_chengzhi.pdf)

[14] D. Darwiche and I. Nyström, “Finding Junctions in Spline-based Road Generation,” B.S. thesis, Malmö University, Malmö, Sweden, 2022. [Online]. Available: [https://www.diva-portal.org/smash/get/diva2:1675311/FULLTEXT01.pdf](https://www.diva-portal.org/smash/get/diva2:1675311/FULLTEXT01.pdf)
