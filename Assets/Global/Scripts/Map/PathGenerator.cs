using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Assets.Global.Scripts.Agents;

namespace Assets.Global.Scripts.Map
{
    /// <summary>
    /// This class serves to generate path for given agent according to given settings.
    /// </summary>
    public class PathGenerator : MonoBehaviour
    {
        private bool AllowOneRoadPaths = false;
        public bool PreventSpawnCrashes = true;

        public RealMap Map;
        public GeneralSettings Settings;
        private IdleCarManager idleCarManager;

        [Header("Fixed start setting for neuroevolution")]
        public bool EnableFixedRoad;
        public int FixedStartRoadId;
        public bool EnableFixedLine;
        public int FixedStartLineId;
        public bool EnableFixedDistanceInLine;
        public float FixedStartDistanceInLine;

        private const float StartingPointOffsetY = 0.32f;
        private const float StartingPointMaxOffsetXZ = 0.2f;
        private const float StartingPointMaxAngleOffset = 8;

        public const float CrossroadValue = 2; // ad hoc length value for crossroads
        public const float ParkingStartPointOffset = 4; // distance before parking space where agents stars.
        private GenericAgent[] Agents;

        private static System.Random RandomSeedGenerator = new System.Random();

        public void Awake()
        {
            CollectAgentInstances();
            idleCarManager = GameObject.FindObjectOfType<IdleCarManager>();
        }

        private void CollectAgentInstances()
        {
            Agents = GameObject.FindObjectsOfType<GenericAgent>();
        }

        /// <summary>
        /// generates random path type according to current settings
        /// </summary>
        /// <returns></returns>
        private PathType GeneratePathType()
        {
            if (RandomSeedGenerator.NextDouble() < Settings.SecondaryTypeProbability)
            {
                return Settings.SecondaryType;
            }

            return Settings.PathType;
        }

        public Path GenerateRandomPath(float minLength)
        {
            return GeneratePathFromSeed(minLength, RandomSeedGenerator.Next(), GeneratePathType());
        }

        public Path GeneratePathFromSeed(float minLength, int seed)
        {
            return GeneratePathFromSeed(minLength, seed, GeneratePathType());
        }

        /// <summary>
        /// Generates random path according to current settings.
        /// </summary>
        /// <param name="minLength"></param>
        /// <param name="seed"></param>
        /// <param name="type"></param>
        /// <param name="tryId"></param>
        /// <returns></returns>
        private Path GeneratePathFromSeed(float minLength, int seed, PathType type, int tryId = 0)
        {
            if (Map == null) throw new Exception("PathChecker or RealMap params were not set.");

            if (tryId > 20) type = PathType.OnlyPath;

            System.Random rand = new System.Random(seed);
            Path path = new Path();

            string str = "";
            path.Crossroads = new List<Crossroad>();
            path.Roads = new List<Road>();
            path.LineIds = new List<int>();
            path.TotalLength = 0;
            path.Type = type;

            Road r = null;
            int roadLine = -1;
            bool foundStartingPoint = false;
            int startingPointTries = 0;
            IdleCar idleCar = null;

            //looking for correct starting point
            if (type == PathType.OnlyParking)
            {
                r = Map.Roads.GetRandomMember(rand);
                roadLine = rand.Next(r.NumberOfLines);
                path.Roads.Add(r);
                str += r.id;
                path.LineIds.Add(roadLine);
            }
            else
            {
                if (type.SwitchesCarObjects())
                {
                    //looking for an idle car

                    idleCar = idleCarManager.GetRandomIdleCar();

                    r = idleCar.ParkingSpace.ConnectedRoad;
                    roadLine = idleCar.ParkingSpace.RoadLineId;

                    path.StartingPoint = idleCar.Car.transform.position;
                    path.StartingDirection = idleCar.ParkingSpace.ParkedDirection;
                    path.StartingParkingSpace = idleCar.ParkingSpace;
                    r.GetClosestPoint(path.StartingPoint, roadLine, out path.StartingDistanceInRoad);

                    path.TotalLength = r.GetLineLength(roadLine) - path.StartingDistanceInRoad;

                    path.Car = idleCar.Car;
                }
                else if (type.StartsOnEmptyParkingSpace())
                {
                    int numberOfTries = 0;

                    //search for empty starting parking space
                    while (!foundStartingPoint)
                    {
                        if (numberOfTries++ > 200)
                        {
                            Debug.Log("couldn't find valid starting parking space");
                            return GeneratePathFromSeed(minLength, seed, PathType.OnlyPath);
                        }

                        int psid = rand.Next((int)Map.ParkingSpaces.Count);
                        ParkingArea ps = Map.ParkingSpaces[psid];

                        if (ps.IsFull()) continue;

                        int slotId;
                        (path.StartingPoint, slotId) = ps.GetRandomSpace();
                        path.StartingPoint += Vector3.up * 0.38f;

                        if (!IsValidStartingPoint(path.StartingPoint)) continue;

                        path.StartingDirection = ps.ParkedDirection;
                        path.StartingParkingSpace = ps;

                        r = ps.ConnectedRoad;
                        roadLine = ps.RoadLineId;

                        r.GetClosestPoint(path.StartingPoint, roadLine, out path.StartingDistanceInRoad);
                        path.TotalLength += r.GetLineLength(roadLine) - path.StartingDistanceInRoad;

                        foundStartingPoint = true;
                    }
                }
                else
                {
                    // looking for random road and random starting point on that road
                    while (!foundStartingPoint)
                    {
                        //random road
                        int initRoadId = FixedStartRoadId;
                        if (!EnableFixedRoad) initRoadId = rand.Next((int)Map.Roads.Count);
                        r = Map.Roads[initRoadId];

                        //random road line
                        roadLine = FixedStartLineId;
                        if (!EnableFixedLine)
                        {
                            roadLine = rand.Next(r.NumberOfLines);
                            if (!AllowOneRoadPaths && r.GetEndingCrossroad(roadLine) == null)
                            {
                                continue;
                            }
                        }

                        //random distance on line
                        if (EnableFixedDistanceInLine)
                        {
                            path.StartingDistanceInRoad = FixedStartDistanceInLine;
                            if (FixedStartDistanceInLine > r.GetLineLength(roadLine))
                            {
                                path.StartingDistanceInRoad = r.GetLineLength(roadLine) - 1;
                            }
                            if (path.StartingDistanceInRoad < 0)
                            {
                                path.StartingDistanceInRoad = r.GetLineLength(roadLine) / 2;
                            }
                        }
                        else
                        {
                            if (r.GetLineLength(roadLine) > 3)
                            {
                                path.StartingDistanceInRoad = rand.NextFloat(1, r.GetLineLength(roadLine) - 1);
                            }
                            else
                            {
                                path.StartingDistanceInRoad = rand.Next(0, Mathf.FloorToInt(r.GetLineLength(roadLine)));
                            }
                        }

                        path.TotalLength = r.GetLineLength(roadLine) - path.StartingDistanceInRoad;

                        path.StartingPoint = r.GetPointOnLine(roadLine, path.StartingDistanceInRoad, out path.StartingDirection);
                        path.StartingPoint.y += 0.32f;

                        foundStartingPoint = IsValidStartingPoint(path.StartingPoint);

                        startingPointTries++;
                        if (startingPointTries > 50)
                        {
                            foundStartingPoint = true;
                        }
                    }
                }

                //adding first road to path list
                path.Roads.Add(r);
                str += r.id;
                path.LineIds.Add(roadLine);

                //generating next crossroad-road pairs
                while (path.TotalLength < minLength || path.Roads.Count < 2)
                {
                    Crossroad cross = r.GetEndingCrossroad(roadLine);
                    if (cross == null) break;
                    int c = cross.ConnectedRoads.Count;
                    Road nextRoad = r;
                    while (nextRoad == r) //TOOD check for bad map
                    {
                        nextRoad = cross.ConnectedRoads[rand.Next(c)];
                    }

                    r = nextRoad;
                    roadLine = r.GetLineFromCrossroad(cross);

                    path.TotalLength += CrossroadValue;
                    path.TotalLength += nextRoad.GetLineLength(roadLine);

                    path.Roads.Add(r);
                    path.LineIds.Add(roadLine);
                    path.Crossroads.Add(cross);
                    str += " " + cross.id + "/ " + r.id;
                }
            }

            //ending path with random parking spot
            if (type.EndsOnParkingSpace())
            {
                int side = 0;
                if (roadLine > 0) side = 1;

                //TODO check if parking spaces were generated;
                if (r.ParkingSpaces[side].Count == 0)
                {
                    if (idleCar != null) idleCarManager.AddIdleCar(idleCar);
                    return GeneratePathFromSeed(minLength, seed + 1);
                }

                int spaceId, tries = 0;
                ParkingArea space;

                do
                {
                    if (tries++ >= 50)
                    {
                        if (idleCar != null) idleCarManager.AddIdleCar(idleCar);
                        return GeneratePathFromSeed(minLength, seed + 1);
                    }

                    spaceId = rand.Next(r.ParkingSpaces[side].Count);
                    space = r.ParkingSpaces[side][spaceId];
                } while (space.IsFull());
                path.ParkingSpace = space;

                Vector3 parkingPoint;
                (parkingPoint, path.ParkingSpaceId) = space.GetRandomSpace();
                Vector3 parkingDirection = space.ParkedDirection;

                Vector3 roadPoint = r.GetClosestPoint(parkingPoint, roadLine, out float progress);

                if (type == PathType.OnlyParking) progress += ParkingStartPointOffset * rand.NextFloat(-1.5f, -0.5f);
                else progress -= ParkingStartPointOffset;

                if (progress < 0.1) progress = 0.1f;
                if (progress >= r.GetLineLength(roadLine) - 0.1f) progress = r.GetLineLength(roadLine) - 0.1f;

                path.TotalLength -= r.GetLineLength(roadLine);
                path.TotalLength += progress;
                path.EndingDistanceInRoad = progress;
                path.EndingPointOnRoad = r.GetPointOnLine(roadLine, progress, out _);

                path.ParkingPosition = parkingPoint;
                path.ParkingDirection = parkingDirection;

                if (type == PathType.OnlyParking)
                {
                    path.StartingPoint = path.EndingPointOnRoad;
                    path.StartingPoint += new Vector3(rand.NextFloat(-StartingPointMaxOffsetXZ, StartingPointMaxOffsetXZ), 
                                                        StartingPointOffsetY, 
                                                        rand.NextFloat(-StartingPointMaxOffsetXZ, StartingPointMaxOffsetXZ));
                    
                    path.StartingDistanceInRoad = progress;

                    r.GetPointOnLine(roadLine, progress, out path.StartingDirection);
                    path.StartingDirection = Quaternion.Euler(0, rand.NextFloat(-StartingPointMaxAngleOffset, StartingPointMaxAngleOffset), 0) * path.StartingDirection;

                    path.ParkingStartVelocity = new Vector3(0, 0, rand.NextFloat(0, Settings.PBMaxInitSpeed));

                    path.Roads.Insert(0, null);
                    path.Roads.Add(null);
                    path.LineIds.Insert(0, 0);
                    path.LineIds.Add(0);
                    path.Crossroads.Add(r.GetStartingCrossroad(roadLine));
                    path.Crossroads.Add(r.GetEndingCrossroad(roadLine));

                    if (!IsValidStartingPoint(path.StartingPoint) || !IsValidStartingPoint(path.ParkingPosition))
                    {
                        if (idleCar != null) idleCarManager.AddIdleCar(idleCar);
                        return GeneratePathFromSeed(minLength, seed + 1, type, tryId + 1);
                    }
                }
            }

            return path;
        }

        /// <summary>
        /// Returns true if given point is far enough from all spawned agents.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        private bool IsValidStartingPoint(Vector3 point)
        {
            if (Agents == null || !PreventSpawnCrashes) return true;

            foreach (var agent in Agents)
            {
                if (agent.carObject == null) continue;

                if (agent.gameObject.activeSelf && Vector3.Distance(agent.GetPosition(), point) < 5) return false;
            }

            return true;
        }
    }
}
