Variable buffer
Figure 11:Variable buffer for robust roadway width transition.

In the special case where the intersection is only a change of roadway width, the arc of the circle transition is less realistic than a linear transition. We use a variable buffer to do this robustly. It also offers the advantage to being able to control the three most classical transitions (symmetric, left, and right) and the transition length using only the street axis.

We define the variable buffer as a buffer whose radius is defined at each vertex (i.e., points for linestring). The radius varies linearly between vertices. One easy, but inefficient solution to compute it is to build circles and isosceles trapezoids and then union the surface of these primitives. We use the easy version.

Lane, markings, street objects

Based on the street section, we can build lanes and lane separation markings. To this end, we cannot simply translate the centre axis because axis are polylines (See Fig. 12). Instead, a function similar to a buffer has to be used (”ST_OffsetCurve”).

Figure 12:Starting from center line (black), a translation would not create correct a lane (red). We must use the buffer (green).

Our input data contains an estimation of the lane number. Even when such data is missing, it can still be guessed from road width, road average speed, etc., using heuristics. The number of lane could also be retrieved from various remote sensing data. For instance, Jin et al., (2009) propose to use aerial images. We can also build pedestrian crossings along the border lines.

Using intersection surfaces and road section surfaces, we build city blocks (See Fig. 13). We define crudely a city block surface as the complementary surface to its bounding road surfaces and road intersections. However, because all the road surface surrounding a city block may not have been generated, we use the road axis instead the road surface as city block limit when road surface is missing.

Because the road axis network has been stored as a topology, getting the surface formed by the road axis surrounding the desired block is immediate. Then, we use Boolean operations to subtract the street and intersection surfaces from the face. This has the advantage that this still provides results when some of the street limiting the block have not been computed, which is often the case in practice. By definition, the universal face (”outside”) is not used as a city block!

Figure 13:We generate city blocks by computing the surface that is bounded by associated road surface, road intersection, and road axis when no road surface is available (top of illustration).
2.6Concurrency and scaling

The aim of this work are to model streets for a whole city in a concurrent way (that is several process could be generating the same street at the same time). Our choice of method is strongly influenced by those factors, and we use specific design to reach those goals, which are not accessory but essential.

One big query

We emphasize that StreetGen is one big SQL query (using various PL/pgSQL and Python functions).
The first advantage it offers is that it is entirely wrapped in one RDBMS transaction.This means that, if for any reason the output does not respect the constraints of the street data model, the result is rolled back (i.e., we come back to a state as if the transaction never happened). This offers a strong guarantee on the resulting street model as well as on the state of the input data.

Second, StreetGen uses SQL, which naturally works on sets (intrinsic SQL principle). This means that computing 
𝑛
 road surfaces is not computing 
𝑛
 times one road surface. This is paramount because computing one road surface actually requires using its one-neighbours in the road network graph. Thus, computing each road individually duplicates a lot of work.

Third, we benefit from the PostgreSQL advanced query planner, which collects and uses statistics concerning all the tables. This means that the same query on a small or big part of the network will not be executed the same way. The query planner optimises the execution plan to estimate the most effective one. This, along with extensive use of indexes, is the key to making StreetGen work seamlessly on different scales.

One coherent streets model results

One of the advantage of working with RDBMSs is the concurrency (the capacity for several users to work with the same data at the same time).
By default, this is true for StreetGen inputs (road network). Several users can simultaneously edit the road axis network with total guarantees on the integrity of the data.

However, we propose more, and exploit the RDBMS capacities so that StreetGen does not return a set of streets, but rather create or update the street modelling.
This means that we can use StreetGen on the entire Paris road axis network, and it will create a resulting streets modelling. Using StreetGen for the second time on only one road axis will simply update the parameters of the street model associated with this axis. Thus, we can guarantee at any time that the output street model is coherent and up to date.

Computing the street model for the first time corresponds to using the ‘insert’ SQL statement. When the street model has already been created, we use an ‘update’ SQL statement. In practice, we automatically mix those two statements so that when computing a part of the input road axis network, existing street models are automatically updated and non existing ones are automatically inserted. The short name for this kind of logic (if the result does not exist yet, then insert, else update) is ‘upsert’.

This mechanism works flawlessly for one user but is subject to the race condition for several users. We illustrate this problem with this synthetic example. The global streets modelling is empty. User1 and User2 both compute the street model 
𝑠
𝑖
 corresponding to a road axis 
𝑟
𝑖
. Now, both users upsert their results into the street table. The race condition creates an error (the same result is inserted twice).

Figure 14:Left, a classical upsert. Right, race condition produces an error.

We can solve this race problem with two strategies. The first strategy is that when the upsert fails, we retry it until the upsert is successful. This strategy offers no theoretical guarantee, even if, in practice, it works well. We choose a second strategy, which is based on semaphore, and works by avoiding computing streets that are already being computed.

When using StreetGen on a set of road axes, we use semaphores to tag the road axes that are being processed. StreetGen only considers working on road axes that are not already tagged. When the computing is finished, StreetGen releases the semaphore. Thus, any other user wanting to compute the same road axis will simply do nothing as long as those streets are already being computed by another StreetGen user. This strategy offers theoretically sound guarantees, but uses a lot of memory.

2.7Generating basic Traffic information
2.7.1Introduction

StreetGen is based on tables in a RDBMS. As such, its model is extremely flexible and adaptable. We use this capacity to generate basic geometric information needed for traffic simulation. The world of traffic simulation is complex, various methods may require widely different data, depending on the method and the scale of the simulation.

For instance, a method simulating traffic nation-wide (macro simulation) would not require the same data as a method trying to simulate traffic in a city, neither as a method simulating precise trajectory of vehicle in one intersection.

Moreover, traffic simulation may require semantic data. For instance an ordinary traffic lane and the same lane reserved to bus may be geometrically identical but have a very different impact in the simulation.

Traffic simulation may require traffic light sequencing, statistics about car speed and density, visibility of objects, lighting, etc.

Therefore, we do not pretend to provide data for all kind of traffic simulations, but rather to provide basic geometric data at the scale of a city. The basic geometric information we choose to provide are lane and lane interconnection. Because lane and interconnection are integrated into StreetGen, the links between lane, interconnection and road network (road axis, intersection) is always available if necessary.

We define lane as the geometric path a vehicle could follow in a road section. A lane is strictly oriented and is to be used one-way. The intersections are trajectories a vehicle could follow while in an intersection, to go from one road section (lane) to another road section (lane). Similarly, interconnections are one-way.

2.7.2Generating Lanes

Our data contains an approximate number of lane per road axis. Even in absence of such data, it could be estimated based on the road width and importance.

We compute the lanes of an axis using the buffer operation (formaly Minkowsky sum with disk), as a simple translation would not produce correct result (See Fig. 12). We create lane axis and lane separator, the second being a useful representation, and potential base to generate lane separation markings. The lane generation then depends on the parity of the number of lane, and is iterative (See Fig. 15). Special care must be taken so that all polylines generated have a coherent geometric direction.

Figure 15:Generating various number of lanes, displayed in QGIS with dotted lines.

Our data set also gives approximate information direction for each road axis. The road axis direction may be ’Direct’, ’Reverse’ or ’Both’. ’Direct’ and ’Reverse’ are both for one-way roads, with the global direction being relative to the road axis geometry direction (i.e. order of points). In ’Both’ case we only know that the road is not one-way.

Please note that this simple information are very lacking to describe even moderately complex real roads (for instance, 3 lane in one direction, and one lane in the other). For lack of better solution, we have to make strong assumptions.

In the case of ’Reverse’ or ’Direct’, all lanes shall have the same direction. In the ’Both’ case, lanes on the right of the road axis should have same direction as road axis, and lanes on the left opposite direction. In odd case, the center lane will be considered on the right of the road axis. Lane are numbered by distance to road axis, side (right first). Figure 16 gives an overview of possible lane directions.

Figure 16:Default possible direction for lanes.

.

2.7.3Generating trajectories in interconnection

Our dataset lacks any information about lane interconnection, i.e. which connexion between lanes are possible and what trajectory those connections have. For instance, being on the right lane of street X, is it possible to go to the right lane of street Y at tne next intersection, and following which trajectory?

Strong assumptions are necessary. We use the orientation of lanes and consider that interconnection can only join lanes having opposite input direction in an intersection. Considering an intersection, each lane either comes in or out of this intersection (intersection input direction). Furthermore, we consider that lanes of the same road section are not directly connected (no turn around). Please note that in real life usage such trajectory may be possible. We create an interconnection for each pair of lanes respecting those conditions.

Actual vehicle trajectories in intersections are very complex, depending both on kinematic parameters, driver perceptual parameters, driver profile, vehicle, weather condition, etc. For instance Wolfermann et al., (2011) study a simple case and model only the speed profile.

We generate a plausible and simple trajectory using Bezier curves. Moreover, we isolated the part responsible for trajectory computing so it can be easily replaced by a more adapted solution than Bezier curve.

Figure 17:interconnection trajectory, Bezier curve influenced by start/end and possibly intersection centre.

Bezier control points are the points where lane center enter/exit the intersection. The third control point depends on the situation. It usually is the barycentre of lanes intersection and intersection centre. However, when lanes are parallel, lane intersection is replaced by enter/exit barycentre. In special case when lanes are parallel and opposite, the centre of the intersection is not considered to obtain a straight line trajectory. Figure 17 presents interconnection trajectory generation in various situations.

2.8Roundabout detection

StreetGen has been used for traffic simulation. StreetGen does not consider semantic difference for any intersection. However traffic simulation tools make a strong difference between intersection and round-about.

Still, the traffic modelling is widely different between roundabout and classical intersection. Thus we need a method to detect roundabouts. We face a problem similar to (Touya, (2010), Section 3.1). The main issue is that round-about definition is based on the driving rules in the intersection (type of priority, no traffic light,…). Yet those details are not available on the road axis network we use. If we use a strict geometric definition (round about are rounds), we could try to extract the information from aerial images (Ravanbakhsh and Fraser, (2009)) or from vehicle trajectory Zinoune et al., (2012). Yet both this example are not in street settings, where round-abouts may be much smaller, and much harder to see on aerial images. Moreover, vehicle trajectory would be much less precise because buildings mask GPS.

Of course we are far from having this level of information, therefore we used the little information available, that is geometrical shape of street axis and street names. We need a way to characterize a round-about that can be used for detection. We cannot define round-about only based on the topology of the road network (such as : a small loop), nor purely based on geometry (road axis is forming a circle) because round abouts are not necessary round. We noticed that road axis in a roundabout tends to have the same name, and/or contain the word ’PL’ or ’RPT’ (IGN short for ’Place’ and ’Rond-Point’ (roundabout)).

Therefore we use two criterias to define a potential roundabout : its road axis may be round (geometric criteria), and the road axis might have the same name or contain ’PL’ or ’RPT’ in their name (toponym criteria). We use Hough transform (Duda and Hart, (1972)) to detect quadruplets of successive points in road axis that are a good support for an arc of circle, then perform unsupervised clustering wia DBSCAN algorithm (Pedregosa et al., (2011), Ester et al., (1996)). To exploit road-name we explore the road network face by face while considering if all the road of a face have the same name and/or some contains special ’PL’ or ’RPT’ key words.

The final results are weighted, and are used by an user to quickly detect roundabouts (See Figure 18).
