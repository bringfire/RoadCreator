# Related work · a-b-street/osm2streets · Discussion #195 · GitHub

**URL:** https://github.com/a-b-street/osm2streets/discussions/195

---

Skip to content
Navigation Menu
Platform
Solutions
Resources
Open Source
Enterprise
Pricing
Sign in
Sign up
a-b-street
/
osm2streets
Public
Notifications
Fork 12
 Star 137
Code
Issues
33
Pull requests
2
Discussions
Actions
Projects
Security
Insights
Related work #195
dabreegster started this conversation in General
dabreegster
Maintainer

This is an issue just to keep track of other related work.

https://gisintransportation.com/media/zsnn3oou/2022_04_19-gis-t-workshop-aegist-slides.pdf, see page 18. They have a nice classification of some complex junctions:


1
Replies:
36 comments · 17 replies
Oldest
Newest
Top
dabreegster
Maintainer
Author

Apparently sharedstreets does some simplifications of small roads, but doesn't collapse dual carriageways. The most detail I've found is in the code: https://github.com/sharedstreets/sharedstreets-builder/blob/a554983e96010d32b71d7d23504fa88c6fbbad10/src/main/java/io/sharedstreets/tools/builder/transforms/BaseSegments.java#L210

1
0 replies
dabreegster
Maintainer
Author

https://github.com/gsagostini/momepy/blob/main/gsocc/notebooks/22-06-30_intersections.ipynb (ongoing google summer of code project)

1
0 replies
dabreegster
Maintainer
Author

https://wiki.openstreetmap.org/wiki/Berlin/Verkehrswende/Gehwege has been updated since I last looked -- there are some awesome diagrams there

1
0 replies
dabreegster
Maintainer
Author

https://sdna-open.readthedocs.io/en/latest/step_by_step_guides.html#modelling-a-combined-vehicle-and-cycle-network found by @fhk

1
0 replies
dabreegster
Maintainer
Author

https://hal.archives-ouvertes.fr/hal-02280105/document averages parallel rail lines

1
1
0 replies
dabreegster
Maintainer
Author

https://traffic3d.org/import_open_street_map.html (it's a new traffic simulator!)

1
2
0 replies
fhk

Also saw this - https://muffinman.io/blog/draw-svg-rope-using-javascript/

Interesting write up and geometric ideas

1
1
0 replies
dabreegster
Maintainer
Author

Someone sent along https://peertube.openstreetmap.fr/w/kF7FsouSL6UwafhwTUgPYQ?start=17m48s

1
1
0 replies
dabreegster
Maintainer
Author

https://community.openstreetmap.org/t/tagging-a-sidewalk-name-or-creating-relation-to-street/7786/23

1
0 replies
BudgieInWA
Collaborator

I haven't read this yet, but saw a recent paper tackling this problem "From road centrelines to carriageways—A
reconstruction algorithm": https://journals.plos.org/plosone/article/file?id=10.1371/journal.pone.0262801&type=printable

1
0 replies
BudgieInWA
Collaborator

"OSM-Based Automatic Road Network Geometries Generation on Unity" https://kth.diva-portal.org/smash/get/diva2:1375175/FULLTEXT01.pdf

1
0 replies
dabreegster
Maintainer
Author

Super cool finds! I skimmed through both quickly, but need to read closely later to understand. The first one's repo is https://github.com/tudelft3d/carriageways-creator, but I'm having trouble building it

1
0 replies
fhk

Nice! find @BudgieInWA

1
0 replies
dabreegster
Maintainer
Author

https://github.com/kauevestena/osm_sidewalkreator
The slide deck from SoTM is very helpful -- this is feeling like an intersection geometry algorithm:


2
1
2 replies
kauevestena

Hey, hello! I am so glad to see people noticing my plugin =D

kauevestena

But this design has its flaws; I'm working on a new algorithm for such a problem!

dabreegster
Maintainer
Author

https://github.com/anisotropi4/graph/blob/master/graph.md possibly relates to collapsing parallel ways

1
0 replies
6 hidden items
Load more…
dabreegster
Maintainer
Author

Network simplification: https://www.youtube.com/watch?v=F8yPKpUfQfU, https://github.com/achic19/micro_walking/blob/master/notebook/output/turin/data_from_osm.ipynb
@Robinlovelace, maybe useful for your current work

1
1 reply
Robinlovelace

Awesome, thank you Dustin!

tordans

https://youtu.be/V_L-CaPWk1Y?si=-WhmLp5w-Qnp96FE demo of a OSM Import to create a street network for a game with lanes and such.

2
0 replies
dabreegster
Maintainer
Author

https://ebikecity.ch/en.htm
Scroll down to their map!

They're just offsetting and thickening road center lines, but it still looks nice

1
0 replies
dabreegster
Maintainer
Author

https://doi.org/10.1016/j.jcmr.2024.100048 section 4.4, https://github.com/lukasballo/snman for simplifying parallel roads

2
2
1
0 replies
tordans

SOTM 2024: From Complexity to Clarity: Simplifying OpenStreetMap Data for Improved Active Transportation

https://youtu.be/fW82AvpyXW0
https://2024.stateofthemap.org/sessions/LAZXYU/
Presentation https://pretalx.com/media/state-of-the-map-2024-academic-track/submissions/LAZXYU/resources/Final_presentaion__u2CXm6N.pdf
It looks like the code is in those notebooks at https://github.com/achic19/SOD/tree/master/Code/notebooks
Talk Abstract
2
2
0 replies
dabreegster
Maintainer
Author

https://codepen.io/almccon/pen/KwKwWaz from https://osmus.slack.com/archives/C01G3D28DAB/p1739430672401849

1
1
0 replies
tordans

There is a recent discussion at https://community.openstreetmap.org/t/quick-poll-lane-count/126298 and other channels around how the lanes=<number> tag should be interpreted which showed, that we cannot rely on that tag as much as we thought we could. I thought I post it here because all the projects here will somehow rely on that tag…

1
1
0 replies
edited
tordans

Simplification of street networks by @martinfleis

https://martinfleischmann.net/simplification-of-street-networks/
https://fosstodon.org/@martinfleis/114417530407685560
https://github.com/uscuni/neatnet
https://uscuni.org/neatnet/
Paper https://arxiv.org/abs/2504.16198
Slides https://uscuni.org/talks/slides/202504_GISRUK_simplification.html
A comparison of existing solutions https://uscuni.org/talks/slides/202504_GISRUK_simplification.html#/5/2
2
1 reply
tordans

I first thought that was related to #195 (comment) but it is something new / different

tordans

A global urban road network self-adaptive simplification workflow from traffic to spatial representation
https://www.nature.com/articles/s41597-025-05164-9

According to https://datasci.social/@mszll/114606288002466513 this looks similar to #195 (comment)

1
0 replies
tordans

For reference, I wanted to write down what I am looking for in our work with OSM and simplified road networks. Mainly, because it looks like many of the solutions look at only one part of this, yet:

I am looking for a way to get the data into OSM processing that will allow me to know which ways are part of the same road space.

when I process way 1, I want to know that this is a side path to road 2 and that road 2 is of type "primary"
when I visualize roads, I want to draw a lane-line line that shows way 1 next to way 2, followed by way 3

This required information about the relation of roads in their shared road space. But also about their attributes.

1
3 replies
edited
tordans

Example 1: The CQI would ideally have one geometry that represents all lane-like structure (centerline, cycleway and or footway left left and right). Right now, we have messy situations like https://www.osm-verkehrswende.org/cqi/map/?anzeige=cqi&map=18.2%2F13.45162%2F52.47809&filters=usable-yes where tagging and mapping practices result in very different visualizations that don't help the presentation.

edited
tordans

Example 2: Many external data sources use simplified road geometries (Edges in a Graph model). This make matching OSM Data very complex. We need to match the attributes but still keep data about the OSM ways and the relation to OSM. This is an example where the red+orange line are external data and blue+yellow is OSM.

dabreegster
Maintainer
Author

Your second example might be best-suited for something like map-matching. https://github.com/JosiahParry/anime is one approach.

I absolutely agree about preserving all of the semantics about the separate pieces of roads in the final simplified representation. I wrote up https://github.com/dabreegster/road-bundler with what I'm working on right now. If you want to play with it so far:

https://dabreegster.github.io/road-bundler/#16.02/52.478075/13.44795
Click the sidepath tool, then "Remove all sidepaths". Ignore the "merge" tool; I don't think this approach is the one that makes sense. Then go to the dual carriageway tool, click a purpleish area to simplify it. If you hover on the orange simplified edge, you see the original OSM edges that got "zipped" into it:

My goal is to put semantic data on that orange edge like "from left-to-right, there's a sidewalk (way 123, with tags XYZ), then the northbound road (way 456, tags ...), then southbound road, then another sidewalk".

This is still an early experiment, there are lots and lots of bugs.

1
tordans

https://www-openstreetmap-org.translate.goog/user/Mikhail%20Kuzin/diary/406938?_x_tr_sl=auto&_x_tr_tl=de&_x_tr_hl=de&_x_tr_pto=wapp
(Non-translated)

4
5 replies
dabreegster
Maintainer
Author

Oh wow, this looks incredible. I couldn't find a Github repo anywhere or a link to the tool; maybe I missed it in the translation?

kuzinmv

Hello! Nice to meet you.
This app is not released yet, we plan to publish the app this fall, God willing. There is still a lot of work to do on the editor, the engine is more or less stable.

I am publishing chapters in the diary for now to evaluate the community's reaction.

Other draft articles will soon appear in the diaries. No translation yet, but by the official release they will be in English.

I will be happy to answer questions.

2
3
dabreegster
Maintainer
Author

I look forward to the release then! It's a hugely difficult technical problem,and I'm very impressed from your first post. An app like this can help people fix OSM lane tagging so much faster than anything else, and demonstrate the power of detailed tagging. Let us know if you want any help with testing or feedback. https://osm2streets.org/ has a list of example areas that have proven to be difficult in this project, if you're looking for test cases.

1
kuzinmv

Thank you very much.
To make the wait less tedious, I published several articles in the diary.
https://www.openstreetmap.org/user/Mikhail%20Kuzin/diary

It would be interesting to know your opinion on the proposals for new tags. There is still time to fix something.

kuzinmv

I look forward to the release then! It's a hugely difficult technical problem,and I'm very impressed from your first post. An app like this can help people fix OSM lane tagging so much faster than anything else, and demonstrate the power of detailed tagging. Let us know if you want any help with testing or feedback. https://osm2streets.org/ has a list of example areas that have proven to be difficult in this project, if you're looking for test cases.

Thank you for your feedback, and I want to please you a little. We are working on the official launch of OSMPIE

The application itself is now available online, as well as some documentation, descriptions and cool examples.

https://osmpie.org/

I’m also actively trying to introduce corrections to the OSM roads near to me . There you can see links to changesets to compare before and after and see which tags were used where.

There are still a lot of bugs in the editor and engine, but it is already quite usable.

That is why we suggest you try it now and of course we are waiting for your comments and issues on GitHub.

Robinlovelace

Map visualising how Zurich would look after reallocating 50% space for active travel:

Source: https://www.ebikecity.ch/en.htm

1
2 replies
Robinlovelace

In conversation with @Hussein-Mahfouz

Robinlovelace

I now realise this is a duplicate, apologies! #195 (comment)

Robinlovelace

Just came across this open access paper on methods for extracting road space usage info from 3D point cloud LiDAR data: https://www.sciencedirect.com/science/article/pii/S1569843225004509

It demonstrates methods for classifying space at different levels of granularity with reference to OSM data

Thanks @valenca13 and heads-up @wangzhao0217 as related to the work you did on road width estimation.

1
1
1
0 replies
dabreegster
Maintainer
Author

From https://blog.opencagedata.com/post/openstreetmap-interview-osmpie-mikhail-kuzin, some webmaps with both a high level of road detail and great UX for selecting POIs / buildings:

https://yandex.com/maps/66/omsk/?indoorLevel=1&ll=73.372979%2C54.993904&mode=poi&poi%5Bpoint%5D=73.367743%2C54.994473&poi%5Buri%5D=ymapsbm1%3A%2F%2Forg%3Foid%3D208868946265&z=19.01

https://2gis.ru/omsk/geo/282213711094197/73.382814%2C54.970556?m=73.379572%2C54.972465%2F18.5&immersive=on

1
1 reply
tordans

Also https://community.openstreetmap.org/t/complaint-and-criticism-against-osmpie-for-redefining-and-promoting-poorly-thought-tags-without-discussing-and-annoucing-clearly/136885/5 and kuzinmv/osmpie-doc#9

dabreegster
Maintainer
Author

https://community.openstreetmap.org/t/complex-ways-this-isnt-about-sidewalks/137109/19 discussion on the data model

1
0 replies
Sign up for free to join this conversation on GitHub. Already have an account? Sign in to comment
Category
General
Labels
None yet
7 participants
Converted from issue

This discussion was converted from issue #37 on February 16, 2023 10:02.

Footer
© 2026 GitHub, Inc.
Footer navigation
Terms
Privacy
Security
Status
Community
Docs
Contact
Manage cookies
Do not share my personal information