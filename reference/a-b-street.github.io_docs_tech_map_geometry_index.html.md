
**URL:** https://a-b-street.github.io/docs/tech/map/geometry/index.html

---

1. Homepage
2. Software
2.1. A/B Street
2.2. Ungap the Map
2.2.1. User guide
2.2.2. Motivation
2.2.3. Project plan
2.2.4. Technical details
2.3. 15-minute neighborhoods explorer
2.4. 15-minute Santa
2.5. Low-traffic neighborhoods
2.5.1. Technical details
2.6. OpenStreetMap viewer
2.7. Mapping on-street parking
3. User guide
3.1. Importing a new city
3.2. ASU Lab guide
4. Proposals
4.1. Seattle bike network vision
4.2. Allow bike and foot traffic through Broadmoor
4.3. Lake Washington Blvd Stay Healthy Street
5. Technical details
5.1. Developer guide
5.1.1. Misc developer tricks
5.1.2. API
5.1.3. Testing
5.1.4. Data organization
5.1.5. Release process
5.1.6. Data formats
5.1.6.1. Scenarios
5.1.6.2. Traffic signals
5.1.7. widgetry UI
5.2. Map model
5.2.1. Intersection geometry
5.2.2. Details
5.2.3. Importing
5.2.3.1. convert_osm
5.2.3.2. Road/intersection geometry
5.2.3.3. The rest
5.2.3.4. Misc
5.2.4. Live edits
5.2.5. Exporting
5.3. Traffic simulation
5.3.1. Discrete event simulation
5.3.2. Travel demand
5.3.3. Gridlock
5.3.4. Multi-modal trips
5.3.5. Live edits
5.3.6. Parking
6. Project
6.1. Team
6.2. Contributing
6.3. Funding
6.4. Motivations
6.5. History
6.5.1. Backstory
6.5.2. Year 1
6.5.3. Year 2
6.5.4. Year 3
6.5.5. 3 year retrospective
6.5.6. 2022 retrospective
6.5.7. Full CHANGELOG
6.6. Users
6.7. References
6.8. Presentations
A/B Street
  
Deep dive into devilish details: Intersection geometry

By Dustin Carlino, last updated September 2021

Some of the things in A/B Street that seem the simplest have taken tremendous effort. Determining the shape of roads and intersections is one of those problems, so this article is a deep-dive into how it works and why it's so hard.

Note: The approach I'll describe has many flaws -- I'm not claiming this is a good solution, just the one that A/B Street uses. If you see a way to improve anything here, let me know about it!

Trying this out
Background
Desired output
The main process
Part 1: Thickening the infinitesimal
Projecting a polyline
Part 2: Counting coup
Part 3: The clockwise walk
Sorting roads around a center
Interlude: problems so far
Funky sidewalks
Lovecraftian geometry
Bad OSM data
Highway on/off-ramps
Intersection consolidation
Where short roads conspire
Why we want to do something about it
Goal
A solution: two passes