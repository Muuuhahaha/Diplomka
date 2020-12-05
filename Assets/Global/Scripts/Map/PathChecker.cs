//using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Assets.Global.Scripts;


namespace Assets.Global.Scripts.Map
{
    /// <summary>
    /// This class monitors agent's progress on current path.
    /// </summary>
    class PathChecker : NavMesh
    {
        private List<Crossroad> PathCrossroads { get { return GeneratedPath.Crossroads; } }
        private List<Road> PathRoads { get { return GeneratedPath.Roads; } }
        private List<int> LineIds { get { return GeneratedPath.LineIds; } }
        public float TotalLength { get { return GeneratedPath.TotalLength; } }
        
        private int CurRoadLine;
        private int CurRoadId;
        public float TotalProgress { private set; get; }
        private float lastRoadProgress;

        private int BestState;
        public int CurrentState;

        private Vector3 StartingPoint { get { return GeneratedPath.StartingPoint; } }
        private Vector3 StartingDirection { get { return GeneratedPath.StartingDirection; } }
        private float StartingDistanceInRoad { get { return GeneratedPath.StartingDistanceInRoad; } }

        public Path GeneratedPath;

        public delegate void OnBehaviourChangedHandler(object sender, BehaviourState state);
        public event OnBehaviourChangedHandler BehaviourChanged;

        public void ResetCar()
        {
            if (!GeneratedPath.Type.SwitchesCarObjects())
            {
                var wv = Car.GetComponent<VehicleBehaviour.WheelVehicle>();
                wv.ResetPos();

                Car.transform.position = StartingPoint;
                Car.transform.forward = StartingDirection;
            }

            if (GeneratedPath.Type.StartsOnParkingSpace())
            {
                CurrentParkingSpace = GeneratedPath.StartingParkingSpace;
            }
            else
            {
                CurrentParkingSpace = null;
            }

            CurRoadId = 0;
            CurRoadLine = LineIds[0];
            CurrentRoad = PathRoads[0];
            lastRoadProgress = StartingDistanceInRoad;
            Status = CarStatus.OnRoad;


            if (GeneratedPath.Type == PathType.OnlyParking)
            {
                ChangeBehaviourState(BehaviourState.Parking);
                Car.GetComponent<Rigidbody>().velocity = Car.transform.localToWorldMatrix * GeneratedPath.ParkingStartVelocity;

                CurRoadId = 1;
                CurRoadLine = LineIds[1];
                CurrentRoad = PathRoads[1];
            }
            else
            {
                ChangeBehaviourState(BehaviourState.Driving);
            }
        }

        /// <summary>
        /// Returns distance from line center.
        /// </summary>
        /// <returns></returns>
        public float GetDistFromCurLine()
        {
            var pos = Car.transform.localPosition;
            pos.y = 0;

            if (Status == NavMesh.CarStatus.OnRoad)
            {
                Vector3 point = CurrentRoad.GetClosestPoint(pos, CurRoadLine, out float progress);
                point.y = 0;

                return Vector3.Distance(pos, point);
            }
            if (Status == CarStatus.OnCrossroad)
            {
                if (CurrentCrosroad.IsOnCrossroad(pos)) return 0;
                else return float.MaxValue;
            }

            return float.MaxValue;
        }

        public void SetPath(Path path)
        {
            this.GeneratedPath = path;
        }

        /// <summary>
        /// Calculates current progression on path and updates necesarry state variables.
        /// </summary>
        /// <param name="roads"></param>
        /// <param name="crossroads"></param>
        public bool UpdatePosition(bool showPathGizmos = false)
        {
            if (Agent.Settings.DebugRaysOther)
            {
                Debug.DrawRay(GeneratedPath.EndingPointOnRoad, Vector3.up * 5, Color.black);
                Debug.DrawRay(GeneratedPath.ParkingPosition, Vector3.up * 5, Color.black);
            }

            if (showPathGizmos) DebugShowPathGizmos();

            var pos = Car.transform.localPosition;

            if (Status == CarStatus.OnRoad)
            {
                //agent position changed to crossroad or parking space
                if (!CurrentRoad.IsOnRoad(pos))
                {
                    if (CurrentParkingSpace != null && CurrentParkingSpace.IsOnParkingSpace(pos))
                    {
                        return false;
                    }
                    else if (CurrentRoad.IsOnAnyParkingSpace(pos, out CurrentParkingSpace))
                    {
                        return false;
                    }
                    else if (CurRoadId < PathCrossroads.Count 
                        && PathCrossroads[CurRoadId] != null 
                        && PathCrossroads[CurRoadId].IsOnCrossroad(pos))
                    {
                        //agent position changed to next crossroad 
                        Status = CarStatus.OnCrossroad;
                        CurrentCrosroad = PathCrossroads[CurRoadId];
                        TotalProgress += CurrentRoad.GetLineLength(CurRoadLine) - lastRoadProgress;

                        CurrentState++;
                        if (CurrentState > BestState)
                        {
                            BestState++;
                            return true;
                        }
                        return false;
                    }
                    else if (CurRoadId > 0
                        && PathCrossroads[CurRoadId - 1] != null
                        && PathCrossroads[CurRoadId - 1].IsOnCrossroad(pos))
                    {
                        //agent position changed to previous crossroad 
                        Status = CarStatus.OnCrossroad;
                        CurRoadId--;
                        CurrentCrosroad = PathCrossroads[CurRoadId];
                        TotalProgress -= PathGenerator.CrossroadValue;
                        TotalProgress -= lastRoadProgress;

                        CurrentState--;
                    }
                }
                else
                {
                    //agent is still on road
                    float progress;
                    CurrentRoad.GetClosestPoint(pos, CurRoadLine, out progress);
                    float diff = progress - lastRoadProgress;
                    TotalProgress += diff;

                    CurrentParkingSpace = null;
                    lastRoadProgress = progress;
                }
            }
            else if (Status == CarStatus.OnCrossroad)
            {
                if (CurrentCrosroad.IsOnCrossroad(pos)) return false;

                //agent position changed to next road
                if (PathRoads[CurRoadId + 1] != null
                    && PathRoads[CurRoadId + 1].IsOnRoad(pos))
                {
                    CurRoadId++;
                    Status = CarStatus.OnRoad;
                    CurrentParkingSpace = null;
                    CurrentRoad = PathRoads[CurRoadId];
                    CurRoadLine = LineIds[CurRoadId];

                    lastRoadProgress = 0;
                    TotalProgress += PathGenerator.CrossroadValue;

                    CurrentState++;
                    if (CurrentState > BestState)
                    {
                        BestState++;
                        return true;
                    }
                    return false;
                }

                //agent position changed to previous road 
                else if (PathRoads[CurRoadId] != null 
                    && PathRoads[CurRoadId].IsOnRoad(pos))
                {
                    Status = CarStatus.OnRoad;
                    CurrentParkingSpace = null;
                    CurrentRoad = PathRoads[CurRoadId];
                    CurRoadLine = LineIds[CurRoadId];

                    float progress;
                    CurrentRoad.GetClosestPoint(pos, CurRoadLine, out progress);

                    TotalProgress -= CurrentRoad.GetLineLength(CurRoadLine) - progress;
                    lastRoadProgress = progress;

                    CurrentState--;
                }
            }

            return false;
        }

        private void ChangeBehaviourState(BehaviourState state)
        {
            if (Agent.CurrentBehaviourType == state) return;

            BehaviourChanged?.Invoke(this, state);
        }

        public void DebugShowPathGizmos()
        {
            Vector3 point = StartingPoint;
            Vector3[] points;

            for (int i = 0; i < PathRoads.Count; i++)
            {
                int line = LineIds[i];
                Road r = PathRoads[i];

                if (i == 0) points = r.GetLinePoints(line, StartingDistanceInRoad);
                else points = r.GetLinePoints(line);

                foreach (var next in points)
                {
                    Debug.DrawLine(point, next, Color.cyan);
                    point = next;
                }
            }
        }

        /// <summary>
        /// Gets line/crossoad edge from current agents position in direction given by angle.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        public float GetDistanceInDirection(Vector3 origin, float angle)
        {
            Vector3 rayDirection = Quaternion.AngleAxis(angle, Vector3.up) * Car.transform.forward;
            rayDirection = new Vector3(rayDirection.x, 0, rayDirection.z);

            return GetDistanceInDirection(origin, rayDirection);
        }

        /// <summary>
        /// Gets line/crossoad edge from current agents position in direction.
        /// </summary>
        /// <returns></returns>
        public float GetDistanceInDirection(Vector3 origin, Vector3 direction)
        {
            float distance = 0;
            List<object> nextObjects = GetNextObjectsInRoad();
            List<object> previousObjects = GetPreviousObjetsInRoad();

            switch (Status)
            {
                case CarStatus.OnRoad:
                    if (CurrentParkingSpace == null)
                    {
                        distance = CurrentRoad.GetDistInDirection(origin, direction, CurRoadLine, nextObjects, previousObjects);
                    }
                    else
                    {
                        distance = CurrentParkingSpace.GetDistanceInDirection(origin, direction, nextObjects, previousObjects);
                    }
                    break;
                case CarStatus.OnCrossroad:
                    distance = CurrentCrosroad.GetDistanceInDirection(origin, direction, nextObjects, previousObjects);
                    break;
                case CarStatus.Other:
                    break;
                default:
                    break;
            }

            return distance;
        }

        /// <summary>
        /// returns list of next roads/crossroads in agents path
        /// </summary>
        /// <returns></returns>
        private List<object> GetNextObjectsInRoad()
        {
            List<object> objects = new List<object>();

            int index = CurRoadId;
            if (Status == CarStatus.OnCrossroad)
            {
                objects.Add(PathCrossroads[index]);
                index++;
            }

            for (; index < PathCrossroads.Count; index++)
            {
                objects.Add(PathRoads[index]);
                objects.Add(PathCrossroads[index]);
            }

            objects.Add(PathRoads[index]);

            return objects;
        }

        /// <summary>
        /// returns list of previous roads/crossroads in agents path
        /// </summary>
        /// <returns></returns>
        private List<object> GetPreviousObjetsInRoad()
        {
            List<object> objects = new List<object>();

            int index = CurRoadId;
            if (Status == CarStatus.OnRoad)
            {
                objects.Add(PathRoads[index]);
                index--;
            }

            for (; index >= 0; index--)
            {
                objects.Add(PathCrossroads[index]);
                objects.Add(PathRoads[index]);
            }

            return objects;
        }

        public void Restart()
        {
            TotalProgress = 0;
            TotalProgress = 0;
            CurrentState = 0;

            ResetCar();
        }

        /// <summary>
        /// Get next fracture point in agents path.
        /// </summary>
        /// <returns></returns>
        public Vector3 GetNextPoint()
        {
            if (Status == CarStatus.OnCrossroad)
            {
                return PathRoads[CurRoadId + 1].GetLinePoints(LineIds[CurRoadId + 1])[0];
            }
            else
            {
                Road r = PathRoads[CurRoadId];
                int l = LineIds[CurRoadId];

                int id = r.GetPartId(Car.transform.localPosition, l);
                var p = r.GetLinePoints(l);

                if (id + 1 >= p.Length) return p[id];

                return p[id + 1];
            }
        }

        /// <summary>
        /// Returns points of agent's path in given distances from agent.
        /// </summary>
        /// <param name="distances"></param>
        /// <returns></returns>
        public Vector3[] GetPointsInDistances(float[] distances)
        {
            float[] remaining = new float[distances.Length];
            Vector3[] results = new Vector3[distances.Length];
            int done = 0;

            for (int i = 0; i < distances.Length; i++)
            {
                remaining[i] = distances[i];
            }

            Vector3 curPoint, pos = Car.transform.position;
            List<Vector3> nextPoints = new List<Vector3>();
            int roadId = CurRoadId + 1;

            //preparing path as set of fracture points
            if (Status == NavMesh.CarStatus.OnRoad)
            {
                curPoint = CurrentRoad.GetClosestPoint(pos, CurRoadLine, out float progress);

                Road r = PathRoads[CurRoadId];
                int l = LineIds[CurRoadId];

                int id = r.GetPartId(Car.transform.localPosition, l);
                var p = r.GetLinePoints(l);

                for (int i = id + 1; i < p.Length; i++)
                {
                    nextPoints.Add(p[i]);
                }
            }
            else
            {
                curPoint = pos;

                Road r = PathRoads[roadId];
                int l = LineIds[roadId];
                var p = r.GetLinePoints(l);
                foreach (var point in p)
                {
                    nextPoints.Add(point);
                }

                roadId++;
            }

            //iterating trough path fracture points
            while (done < distances.Length && nextPoints.Count > 0)
            {
                Vector3 nextPoint = nextPoints[0];
                nextPoints.RemoveAt(0);

                float d = Vector3.Distance(curPoint, nextPoint);
                Vector3 difVector = (nextPoint - curPoint).normalized;
                for (int i = 0; i < remaining.Length; i++)
                {
                    if (remaining[i] < 0) continue;

                    if (remaining[i] > d) remaining[i] -= d;
                    else
                    {
                        results[i] = curPoint + difVector * remaining[i];
                        remaining[i] = -1;
                        done++;
                    }
                }

                curPoint = nextPoint;

                if (nextPoints.Count == 0 && roadId < PathRoads.Count)
                {
                    Road r = PathRoads[roadId];
                    int l = LineIds[roadId];
                    var p = r.GetLinePoints(l);
                    foreach (var point in p)
                    {
                        nextPoints.Add(point);
                    }

                    roadId++;
                }
            }

            for (int i = 0; i < remaining.Length; i++)
            {
                if (remaining[i] >= 0)
                {
                    results[i] = curPoint;
                }
            }

            return results;
        }

        /// <summary>
        /// Returns n-th next fracture point of path. 
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public Vector3 GetNthNextPoint(int n)
        {
            if (n == 1) return GetNextPoint();

            int processed = 0;

            if (Status == CarStatus.OnRoad)
            {
                Road r = PathRoads[CurRoadId];
                int l = LineIds[CurRoadId];

                int id = r.GetPartId(Car.transform.localPosition, l);
                var p = r.GetLinePoints(l);

                for (int i = id + 1; i < p.Length; i++)
                {
                    processed++;
                    if (processed == n) return p[i];
                }
            }

            int roadId = CurRoadId + 1;
            while (roadId < PathRoads.Count)
            {
                Road r = PathRoads[roadId];
                int l = LineIds[roadId];

                var p = r.GetLinePoints(l);

                for (int i = 0; i < p.Length; i++)
                {
                    processed++;
                    if (processed == n) return p[i];
                }
            }

            var q = PathRoads[roadId - 1].GetLinePoints(LineIds[roadId - 1]);
            return q[q.Length - 1];
        }

        public bool Finished(float distFromGoal)
        {
            return TotalLength <= TotalProgress + distFromGoal;
        }

        /// <summary>
        /// returns distance of next crossroad in path before agent.
        /// </summary>
        /// <returns></returns>
        public float GetDistanceTillCrossroad()
        {
            if (Status == CarStatus.OnCrossroad) return 0;

            return CurrentRoad.GetLineLength(CurRoadLine) - lastRoadProgress;
        }
    }

    public enum BehaviourState
    {
        Driving, 
        Parking, 
        Finished, 
        None
    }
}
