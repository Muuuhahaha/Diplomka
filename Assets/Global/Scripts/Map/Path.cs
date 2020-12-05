using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Global.Scripts.Map
{
    /// <summary>
    /// This class represents agent's path.
    /// </summary>
    public class Path
    {
        public float TotalLength;
        public List<Crossroad> Crossroads;
        public List<Road> Roads;
        public List<int> LineIds;
        public float StartingDistanceInRoad;
        public Vector3 StartingPoint;
        public Vector3 StartingDirection;
        public ParkingArea StartingParkingSpace;

        public float EndingDistanceInRoad; //distance in last road till ending point.
        public Vector3 EndingPointOnRoad;

        public int ParkingSpaceId; // id of parking space on given area.
        public Vector3 ParkingPosition;
        public Vector3 ParkingDirection;
        public Vector3 ParkingStartVelocity;

        public ParkingArea ParkingSpace;
        public GameObject Car; //car used in given path (if we switch car in simulation).

        public PathType Type;

        public Path()
        {
            Type = PathType.None;
        }

        public Path(List<Crossroad> crossroads, List<Road> roads, List<int> lineIds, float totalLength, float startingDistanceInRoad, Vector3 startingPoint)
        {
            this.TotalLength = totalLength;
            this.Crossroads = crossroads;
            this.Roads = roads;
            this.TotalLength = totalLength;
            this.StartingDistanceInRoad = startingDistanceInRoad;
            this.StartingPoint = startingPoint;

            this.Type = PathType.OnlyPath;
        }

        public Path(Vector3 startingPoint, Vector3 parkingPosition, Vector3 parkingDirection)
        {
            this.StartingPoint = startingPoint;
            this.ParkingPosition = parkingPosition;
            this.ParkingDirection = parkingDirection;

            this.Type = PathType.OnlyParking;
        }

        public Path(List<Crossroad> crossroads, List<Road> roads, List<int> lineIds, float totalLength, float startingDistanceInRoad, Vector3 startingPoint, 
            float endingDistanceInRoad, Vector3 endingPointOnRoad, Vector3 parkingPosition, Vector3 parkingDirection)
        {
            this.TotalLength = totalLength;
            this.Crossroads = crossroads;
            this.Roads = roads;
            this.TotalLength = totalLength;
            this.StartingDistanceInRoad = startingDistanceInRoad;
            this.StartingPoint = startingPoint;

            this.EndingPointOnRoad = endingPointOnRoad;
            this.EndingDistanceInRoad = endingDistanceInRoad;

            this.ParkingPosition = parkingPosition;
            this.ParkingDirection = parkingDirection;

            this.Type = PathType.PathWithParking;
        }
    }

    public enum PathType
    {
        None, 
        OnlyPath, 
        OnlyParking, 
        PathWithParking,
        FullSimulation,
        PathFromParkingSpace, 
        PathFromParkingToParking
    }
}
