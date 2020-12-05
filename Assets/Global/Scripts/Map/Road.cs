using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using PathCreation;

namespace Assets.Global.Scripts.Map
{
    /// <summary>
    /// This class represents single road on map.
    /// </summary>
    public class Road
    {
        public List<Vector3> Points;
        public Crossroad StartCross;
        public Crossroad EndCross;
        public Vector3[] meshVertices;

        public Vector3[,] FracturePoints; //points on lines and line edges where lines curves.
        public float[] LineLengths;
        public float[,] PartLengths;
        private int NumberOfLinesInADirection = 1;
        public int NumberOfLines { get { return NumberOfLinesInADirection * 2; } }

        public int id;
        private static int idCounter;

        public float LineWidth;

        public List<ParkingArea>[] ParkingSpaces;

        public GameObject Sprite;

        private static System.Random rand = new System.Random();

        public Road(List<Vector3> points, Crossroad startCross, Crossroad endCross, float lineWidth, float crossroadSize)
        {
            Points = points;
            StartCross = startCross;
            EndCross = endCross;
            LineWidth = lineWidth;// rand.NextFloat(1f, 1f);

            AdjustEndPoints(crossroadSize);

            id = idCounter++;
            ParkingSpaces = new List<ParkingArea>[] { new List<ParkingArea>(), new List<ParkingArea>() };
        }

        public static Road FromNodeList(List<MapNode> nodes, Func<MapNode, Vector3> transform, Transform parent, GameObject prefab, float lineWidth, float crossroadSize)
        {
            
            List<Vector3> points = new List<Vector3>();
            foreach (var node in nodes)
            {
                points.Add(transform(node));
            }

            var road = new Road(points, nodes[0].Crossroad, nodes[nodes.Count - 1].Crossroad, lineWidth, crossroadSize);

            return road;
        }

        /// <summary>
        /// moves ending points in order to give enough space for crossroad sprite.
        /// </summary>
        /// <param name="crossroadSize"></param>
        private void AdjustEndPoints(float crossroadSize)
        {
            if ((object)StartCross != null)
            {
                Points[0] = MovePoint(Points[0], Points[1], crossroadSize);
                StartCross.ConnectionPoints.Add(Points[0]);
                StartCross.ConnectedRoads.Add(this);

                if (Points.Count > 2 && (Vector3.Distance(Points[0], Points[1]) < 1 || Utility.IsInLine(StartCross.Position, Points[1], Points[0])))
                {
                    Points.RemoveAt(1);
                }
            }
            if ((object)EndCross != null)
            {
                int l = Points.Count;
                Points[l - 1] = MovePoint(Points[l - 1], Points[l - 2], crossroadSize);
                EndCross.ConnectionPoints.Add(Points[l - 1]);
                EndCross.ConnectedRoads.Add(this);

                if (l > 2 && (Vector3.Distance(Points[l - 1], Points[l - 2]) < 1 || Utility.IsInLine(EndCross.Position, Points[l - 2], Points[l - 1])))
                {
                    Points.RemoveAt(l - 2);
                }

            }
        }

        private Vector3 MovePoint(Vector3 first, Vector3 second, float crossroadSize)
        {
            var dir = second - first;

            dir = dir.normalized * crossroadSize;

            return first + dir;
        }

        /// <summary>
        /// fixes errors in some turns in generated road mesh
        /// </summary>
        public void RemoveCreases()
        {
            for (int i = 0; i < meshVertices.Length - 2; i += 2)
            {
                float side = Utility.GetSideOfLine(meshVertices[i + 2], meshVertices[i], meshVertices[i + 1]);
                if (side > 0)
                {
                    meshVertices[i + 2] = meshVertices[i];
                    if (i > 0)
                    {
                        meshVertices[i + 2] += (meshVertices[i] - meshVertices[i - 2]).normalized * 0.1f;
                    }
                }

                side = Utility.GetSideOfLine(meshVertices[i + 3], meshVertices[i], meshVertices[i + 1]);
                if (side > 0)
                {
                    meshVertices[i + 3] = meshVertices[i + 1];
                    if (i > 0)
                    {
                        meshVertices[i + 3] += (meshVertices[i + 1] - meshVertices[i - 1]).normalized * 0.1f;
                    }
                }

            }
        }

        /// <summary>
        /// generates road sprite.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="prefab"></param>
        public void GenerateSprite(Transform parent, GameObject prefab)
        {
            if (Points.Count == 2)
            {
                Points.Insert(1, (Points[0] + Points[1]) / 2);
            }

            Sprite = UnityEngine.Object.Instantiate(prefab, parent);

            var pc = Sprite.GetComponent<PathCreation.PathCreator>();
            pc.bezierPath = new PathCreation.BezierPath(Points, false, PathCreation.PathSpace.xyz);
            pc.bezierPath.AutoControlLength = 0.15f;


            var mc = Sprite.GetComponentInChildren<PathCreation.Examples.RoadMeshCreator>();
            mc.roadWidth = LineWidth * 2;
            mc.TriggerUpdate();
            meshVertices = mc.verts;
            mc.Recalculate();

            Sprite.GetComponentInChildren<MeshCollider>().sharedMesh = Sprite.GetComponentInChildren<MeshFilter>().mesh;
        }

        /// <summary>
        /// regenerates mesh. 
        /// Should be called after changes in MESHVERTICES array were made.
        /// </summary>
        public void RegenerateMesh()
        {
            if (EndCross != null && EndCross.id == 445)
            {
                Debug.Log("here");
            }

            var mc = Sprite.GetComponentInChildren<PathCreation.Examples.RoadMeshCreator>();
            mc.roadWidth = 4;
            mc.TriggerUpdate();
            mc.verts = meshVertices;
            mc.Recalculate();

            //forcing collider update
            var col = Sprite.GetComponentInChildren<MeshCollider>();
            col.sharedMesh = null;
            col.sharedMesh = Sprite.GetComponentInChildren<MeshFilter>().mesh;

            CalculateFracturePoints();
        }

        /// <summary>
        /// Calculates fracture points.
        /// Fracture points represents turn points on line centers and line edges.
        /// </summary>
        private void CalculateFracturePoints()
        {
            FracturePoints = new Vector3[meshVertices.Length / 2, NumberOfLinesInADirection * 4 + 1];
            PartLengths = new float[meshVertices.Length / 2, NumberOfLinesInADirection * 4 + 1];
            LineLengths = new float[NumberOfLinesInADirection * 4 + 1];

            for (int i = 0; i < FracturePoints.GetLength(0); i++)
            {
                Vector3 step = (meshVertices[i * 2 + 1] - meshVertices[i * 2]) / (NumberOfLinesInADirection * 4);

                for (int j = 0; j < FracturePoints.GetLength(1); j++)
                {
                    FracturePoints[i, j] = meshVertices[i * 2] + step * j;

                    if (i > 0)
                    {
                        float dist = Vector3.Distance(FracturePoints[i - 1, j], FracturePoints[i, j]);
                        LineLengths[j] += dist;
                        PartLengths[i, j] = /*PartLengths[i - 1, j] + */dist;
                    }
                }
            }
        }

        /// <summary>
        /// Merges this object with another road object
        /// </summary>
        /// <param name="r"></param>
        public void MergeWith(Road r)
        {
            if (EndCross == r.EndCross && EndCross != null) r.FlipDirection();
            else if (StartCross == r.StartCross && StartCross != null) FlipDirection();
            else if (StartCross == r.EndCross && StartCross != null)
            {
                FlipDirection();
                r.FlipDirection();
            }

            if (EndCross != r.StartCross)
            {
                throw new Exception("merge error");
            }

            Points.RemoveAt(Points.Count - 1);
            Points.Add(EndCross.Position);

            for (int i = 1; i < r.Points.Count; i++)
            {
                Points.Add(r.Points[i]);
            }

            EndCross = r.EndCross;

            if (EndCross != null) {
                for (int i = 0; i < r.EndCross.ConnectedRoads.Count; i++)
                {
                    if (r.EndCross.ConnectedRoads[i] == r)
                    {
                        r.EndCross.ConnectedRoads[i] = this;
                    }
                }
            }
        }

        /// <summary>
        /// flips direction of road
        /// </summary>
        private void FlipDirection()
        {
            Crossroad tmp = StartCross;
            StartCross = EndCross;
            EndCross = tmp;

            List<Vector3> newPoints = new List<Vector3>();
            for (int i = Points.Count - 1; i >= 0; i--)
            {
                newPoints.Add(Points[i]);
            }

            Points = newPoints;
        }

        /// <summary>
        /// returns closest point on given line to given point
        /// </summary>
        /// <param name="point"></param>
        /// <param name="line"></param>
        /// <param name="distInLine"></param>
        /// <returns></returns>
        public Vector3 GetClosestPoint(Vector3 point, int line, out float distInLine)
        {
            return GetClosestPointOnLine(point, line * 2 + 1, out distInLine);
        }

        /// <summary>
        /// returns closest point on given line to given point
        /// </summary>
        private Vector3 GetClosestPointOnLine(Vector3 point, int lineId, out float distInLine)
        {
            int partId = GetSegmentId(point, lineId);

            if (partId < 0) partId = 0;
            if (partId >= FracturePoints.GetLength(0) - 1) partId = FracturePoints.GetLength(0) - 2;

            distInLine = 0;
            for (int i = 0; i < partId + 1; i++) distInLine += PartLengths[i, lineId];

            Vector3 projection = Vector3.Project(
                point - FracturePoints[partId, lineId],
                FracturePoints[partId + 1, lineId] - FracturePoints[partId, lineId]);

            Vector3 outcome = FracturePoints[partId, lineId] + projection;
            distInLine += projection.magnitude;

            if (IsReverseLine(lineId / 2)) distInLine = LineLengths[lineId] - distInLine;

            return outcome;
        }

        /// <summary>
        /// returns id of mesh rectangle that contains given point.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="lineId"></param>
        /// <returns></returns>
        private int GetSegmentId(Vector3 point, int lineId)
        {
            int c = NumberOfLinesInADirection * 4;

            for (int i = 0; i < FracturePoints.GetLength(0) - 1; i++)
            {
                if (Utility.GetSideOfLine(point, FracturePoints[i, 0], FracturePoints[i, c]) > 0.00001) continue;
                if (Utility.GetSideOfLine(point, FracturePoints[i+1, 0], FracturePoints[i+1, c]) < -0.00001) continue;

                return i;
            }

            int closest = 0;
            float minDist = Vector3.Distance(point, FracturePoints[closest, lineId]);

            for (int i = 1; i < FracturePoints.GetLength(0); i++)
            {
                float dist = Vector3.Distance(point, FracturePoints[i, lineId]);
                if (dist < minDist)
                {
                    closest = i;
                    minDist = dist;
                }
            }

            //getting id of corresponding part of road
            int partId = closest;
            
            float side = Utility.GetSideOfLine(point, meshVertices[closest * 2], meshVertices[closest * 2 + 1]);
            if (side > 0) partId--;

            return partId;
        }


        /// <summary>
        /// returns id of mesh rectangle that contains given point.
        /// </summary>
        public int GetPartId(Vector3 point, int lineId)
        {
            int id = GetSegmentId(point, lineId);

            if (IsReverseLine(lineId))
            {
                return FracturePoints.GetLength(0) - id - 2;
            }

            return id;
        }

        /// <summary>
        /// Returns line edge distance from given point in given direction.
        /// Works recursively on connected crossroads and parking spaces that belong to given path.
        /// </summary>
        public float GetDistInDirection(Vector3 start, Vector3 direciton, int line, List<object> nextObjects, List<object> previousObjects)
        {
            int id = line;
            int partId = GetSegmentId(start, id);

            return GetDistInDirection(start, direciton, id, partId, nextObjects, previousObjects);
        }

        /// <summary>
        /// Returns line edge distance from given point in given direction.
        /// Works recursively on connected crossroads and parking spaces that belong to given path.
        /// </summary>
        private float GetDistInDirection(Vector3 start, Vector3 direction, int lineId, int partId, List<object> nextObjects, List<object> previousObjects)
        {
            if (nextObjects.Count > 1 && this == nextObjects[1])
            {
                nextObjects.RemoveAt(0);
                previousObjects.Clear();
            }
            else if (previousObjects.Count > 1 && this == previousObjects[1])
            {
                previousObjects.RemoveAt(0);
                nextObjects.Clear();
            }
            else if ((nextObjects.Count > 0 && this != nextObjects[0]) || (previousObjects.Count > 0 && this != previousObjects[0])) return 0;

            start.y = 0;
            if (partId < 0) {
                if (StartCross != null) return StartCross.GetDistanceInDirection(start, direction, nextObjects, previousObjects);
                else return 0;
            }
            else if (partId >= FracturePoints.GetLength(0) - 1)
            {
                if (EndCross != null) return EndCross.GetDistanceInDirection(start, direction, nextObjects, previousObjects);
                else return 0;
            }

            Vector3[] corners = GetPartCorners(lineId, partId);
            if (!Utility.IsInside(corners, start)) return 0;
                
            var tuple = Utility.GetNearestDist(corners, start, direction);

            Vector3 nextStart = start + direction.normalized * (tuple.Item1 + 0.01f);
            tuple = Utility.GetNearestDist(corners, start, direction);
            nextStart.y = 0;

            float distInRoad = 0;

            if (tuple.Item2 == 0) distInRoad = GetDistInDirection(nextStart, direction, lineId, partId - 1, nextObjects, previousObjects);
            else if (tuple.Item2 == 2) distInRoad = GetDistInDirection(nextStart, direction, lineId, partId + 1, nextObjects, previousObjects);
            else distInRoad = GetDistOnParkingSpace(nextStart, direction, lineId, partId, nextObjects, previousObjects);

            return tuple.Item1 + distInRoad;
        }

        private float GetDistOnParkingSpace(Vector3 start, Vector3 direction, int side, int partId, List<object> nextObjects, List<object> previousObjects)
        {
            foreach (var space in ParkingSpaces[side])
            {
                if (space.RoadPartId == partId && space.IsOnParkingSpace(start))
                {
                    return space.GetDistanceInDirection(start, direction, nextObjects, previousObjects);
                }
            }

            return 0;
        }

        private Vector3[] GetPartCorners(int lineId, int partId)
        {
            return new Vector3[]
            {
                FracturePoints[partId,lineId * 2],
                FracturePoints[partId,lineId * 2 + 2],
                FracturePoints[partId + 1,lineId * 2 + 2],
                FracturePoints[partId + 1,lineId * 2]
            };
        }

        /// <summary>
        /// returns true if given point is on this road
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool IsOnRoad(Vector3 point)
        {
            Vector3[] corners = new Vector3[4];

            for (int i = 0; i < meshVertices.Length / 2 - 1; i++)
            {
                corners[0] = meshVertices[i * 2];
                corners[1] = meshVertices[i * 2 + 1];
                corners[2] = meshVertices[i * 2 + 3];
                corners[3] = meshVertices[i * 2 + 2];

                if (Utility.IsInside(corners, point))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// returns true if given point is on any parking space adjacent to this road
        /// </summary>
        /// <param name="point"></param>
        /// <param name="parkingSpace"></param>
        /// <returns></returns>
        public bool IsOnAnyParkingSpace(Vector3 point, out ParkingArea parkingSpace)
        {
            parkingSpace = null;

            foreach (ParkingArea space in ParkingSpaces[0])
            {
                if (space.IsOnParkingSpace(point))
                {
                    parkingSpace = space;
                    return true;
                }
            }
            foreach (ParkingArea space in ParkingSpaces[1])
            {
                if (space.IsOnParkingSpace(point))
                {
                    parkingSpace = space;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// returns point on given LINE in given DISTANCE from line start.
        /// </summary>
        /// <param name="line"></param>
        /// <param name="distance"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public Vector3 GetPointOnLine(int line, float distance, out Vector3 direction)
        {
            int length = FracturePoints.GetLength(0);
            int id = line * 2 + 1;

            bool reverse = IsReverseLine(line);
            if (reverse)
            {
                distance = LineLengths[id] - distance;
            }

            if (distance >= LineLengths[id])
            {
                direction = (FracturePoints[length - 1, id] - FracturePoints[length - 2, id]).normalized;
                if (reverse) direction = -direction;
                return FracturePoints[length - 1, id];
            }
            if (distance <= 0)
            {
                direction = (FracturePoints[1, id] - FracturePoints[0, id]).normalized;
                if (reverse) direction = -direction;
                return FracturePoints[0, id];
            }

            float remaining = distance;
            int part = 0;
            while (remaining > PartLengths[part + 1, id] && part < length - 2)
            {
                remaining -= PartLengths[part + 1, id];
                part++;
            }

            direction = (FracturePoints[part + 1, id] - FracturePoints[part, id]).normalized;
            Vector3 point = FracturePoints[part, id] + direction * remaining;
            if (reverse) direction = -direction;

            return point;
        }

        public Crossroad GetEndingCrossroad(int line)
        {
            if (IsReverseLine(line)) return StartCross;
            return EndCross;
        }

        public Crossroad GetStartingCrossroad(int line)
        {
            if (IsReverseLine(line)) return EndCross;
            return StartCross;
        }

        /// <summary>
        /// Returns whether given line is in reverse direction on this road.
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public bool IsReverseLine(int line)
        {
            return line < NumberOfLinesInADirection;
        }

        public float GetLineLength(int line)
        {
            return LineLengths[line * 2 + 1];
        }

        /// <summary>
        /// Returns id of line originating from given crossroad
        /// </summary>
        /// <param name="cross"></param>
        /// <returns></returns>
        public int GetLineFromCrossroad(Crossroad cross)
        {
            if (cross == StartCross) return NumberOfLinesInADirection;
            else return 0;
        }

        /// <summary>
        /// Returns fracture points of center of given line starting from DISTINLINE
        /// </summary>
        /// <param name="line"></param>
        /// <param name="distInLine"></param>
        /// <returns></returns>
        public Vector3[] GetLinePoints(int line, float distInLine)
        {
            Vector3[] points = GetLinePoints(line);
            float dist = 0;

            int i = 0;
            while (dist < distInLine && i < points.Length - 1)
            {
                dist += Vector3.Distance(points[i], points[i + 1]);
                i++;
            }

            Vector3[] newPoints = new Vector3[points.Length + 1 - i];

            newPoints[0] = points[i - 1] + (points[i] - points[i - 1]).normalized * (dist - distInLine);

            for (int j = 1; j < newPoints.Length; j++)
            {
                newPoints[j] = points[i + j - 1];
            }

            return newPoints;
        }

        /// <summary>
        /// Returns fracture points of center of given line.
        /// </summary>
        public Vector3[] GetLinePoints(int line)
        {
            int id = line * 2 + 1;
            int length = FracturePoints.GetLength(0);

            Vector3[] points = new Vector3[length];

            if (!IsReverseLine(line)) {
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = FracturePoints[i, id];
                }
            }
            else
            {
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = FracturePoints[length - i - 1, id];
                }
            }

            return points;
        }

        const float minEdgeLenghtForParkingSpace = 10f;

        /// <summary>
        /// Adds random parking space on given side of the road if possible.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="rand"></param>
        /// <param name="side"></param>
        /// <param name="settings"></param>
        public void AddRandomParkingSpace(Transform parent, System.Random rand, int side, GeneralSettings settings)
        {
            int edgeId = 0;
            int partId = 0;
            int tries = 0;

            while (tries == 0
                    || Vector3.Distance(FracturePoints[partId, edgeId], FracturePoints[partId + 1, edgeId]) < minEdgeLenghtForParkingSpace
                    || PartHasParkingSpace(partId, side))
            {
                partId = rand.Next(FracturePoints.GetLength(0) - 1);
                tries++;

                edgeId = (FracturePoints.GetLength(1) - 1) * side;

                if (tries > 1000)
                {
                    return;
                }
            }

            Vector3 direction = FracturePoints[partId, edgeId] - FracturePoints[partId + 1, edgeId];
            direction = Quaternion.Euler(0, 90, 0) * direction.normalized;

            ParkingArea ps;
            if (side == 1)
            {
                direction = -direction;
                ps = new ParkingArea(this, partId, side, FracturePoints[partId + 1, edgeId], FracturePoints[partId, edgeId], direction, parent, rand, settings);
            }
            else
            {
                ps = new ParkingArea(this, partId, side, FracturePoints[partId, edgeId], FracturePoints[partId + 1, edgeId], direction, parent, rand, settings);
            }
            ParkingSpaces[side].Add(ps);
        }

        private bool PartHasParkingSpace(int partId, int side)
        {
            foreach (var space in ParkingSpaces[side])
            {
                if (space.RoadPartId == partId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
