using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Global.Scripts.Map
{
    /// <summary>
    /// This class represents single crossroad on map.
    /// </summary>
    public class Crossroad
    {
        public Vector3 Position;
        public GameObject Sprite;
        public List<Vector3> ConnectionPoints;
        public List<Vector3> CornerPoints;
        public List<int> RoadCornerIndices; // for each road there is one index that represents the first corner point of corresponding road
        public List<int> CornerRoadIndices; // for each corner there is index of road that starts with given corner
        public List<Road> ConnectedRoads;

        private Vector3[] reverseCorners;
        private MeshCollider col;
        private Mesh Mesh;

        public int id;
        private static int cross_num;

        private float Y { get { return Position.y; } }

        public Crossroad (Vector3 position)
        {
            Position = position;
            ConnectedRoads = new List<Road>();
            ConnectionPoints = new List<Vector3>();

            id = cross_num++;
        }

        /// <summary>
        /// Calculates correct corner poins for sprite polygon.
        /// Also adjust corresponding mesh points in connected roads.
        /// </summary>
        /// <param name="disableMovingCrossroadCorners"></param>
        /// <param name="easyCrossroads"></param>
        public void CalculateCornerPoins(bool disableMovingCrossroadCorners, bool easyCrossroads = true)
        {

            SortLists();
            CornerPoints = new List<Vector3>();
            List<Vector3> tmpCorners = new List<Vector3>();
            RoadCornerIndices = new List<int>();
            CornerRoadIndices = new List<int>();

            //moving road end points, so that road meshes don't overlap
            for (int i = 0; i < ConnectedRoads.Count; i++)
            {
                int j = (i + 1) % ConnectedRoads.Count;
                int a, b, c, d;

                int l = ConnectedRoads[i].meshVertices.Length;
                if (ConnectedRoads[i].Points[0] == ConnectionPoints[i]) { a = 1; b = 3; }
                else { a = l - 2; b = l - 4; }

                l = ConnectedRoads[j].meshVertices.Length;
                if (ConnectedRoads[j].Points[0] == ConnectionPoints[j]) { c = 0; d = 2; }
                else { c = l - 1; d = l - 3; }

                Vector3 intersection = GetIntersectPoint(
                    ConnectedRoads[i].meshVertices[a],
                    ref ConnectedRoads[i].meshVertices[b],
                    ConnectedRoads[j].meshVertices[c],
                    ref ConnectedRoads[j].meshVertices[d]);

                //placeAction(intersection);
                //Debug.Log("inter " + intersection);

                if (!disableMovingCrossroadCorners)
                {
                    if (Utility.IsInLine(ConnectedRoads[i].meshVertices[a],
                                            intersection,
                                            ConnectedRoads[i].meshVertices[b]))
                    {
                        ConnectedRoads[i].meshVertices[a] = intersection;
                    }
                    if (Utility.IsInLine(ConnectedRoads[j].meshVertices[c],
                                            intersection,
                                            ConnectedRoads[j].meshVertices[d]))
                    {
                        ConnectedRoads[j].meshVertices[c] = intersection;
                    }
                }

                tmpCorners.Add(intersection);
            }

            if (easyCrossroads)
            {
                List<int> correspondingRoads = new List<int>();
                List<int> correspondingRoadVertices = new List<int>();

                for (int i = 0; i < ConnectedRoads.Count; i++)
                {
                    int a, b, c, d;

                    int l = ConnectedRoads[i].meshVertices.Length;
                    if (ConnectedRoads[i].Points[0] == ConnectionPoints[i]) { a = 1; b = 3; c = 0; d = 2; }
                    else { a = l - 2; b = l - 4; c = l - 1; d = l - 3; }

                    Vector3 va = ConnectedRoads[i].meshVertices[a], vb = ConnectedRoads[i].meshVertices[b],
                        vc = ConnectedRoads[i].meshVertices[c], vd = ConnectedRoads[i].meshVertices[d];

                    Vector3 pc = Utility.ProjectOnLine(vc, va, vb);
                    Vector3 pa = Utility.ProjectOnLine(va, vc, vd);

                    RoadCornerIndices.Add(CornerPoints.Count);

                    if (Utility.IsInLine(va, pc, vb))
                    {
                        ConnectedRoads[i].meshVertices[a] = pc;
                        if (CornerPoints.Count == 0 || CornerPoints[CornerPoints.Count - 1] != vc)
                        {
                            if (CornerPoints.Count > 0) CornerRoadIndices.Add(-1);

                            CornerPoints.Add(vc);
                            correspondingRoads.Add(i);
                            correspondingRoadVertices.Add(c);
                        }

                        CornerRoadIndices.Add(i);

                        CornerPoints.Add(pc);
                        correspondingRoads.Add(i);
                        correspondingRoadVertices.Add(a);
                    }
                    else if (Utility.IsInLine(vc, pa, vd))
                    {
                        if (CornerPoints.Count > 0) CornerRoadIndices.Add(-1);
                        CornerRoadIndices.Add(i);

                        ConnectedRoads[i].meshVertices[c] = pa;

                        CornerPoints.Add(pa);
                        correspondingRoads.Add(i);
                        correspondingRoadVertices.Add(c);

                        CornerPoints.Add(va);
                        correspondingRoads.Add(i);
                        correspondingRoadVertices.Add(a);
                    }
                    else
                    {
                        if (CornerPoints.Count == 0 || CornerPoints[CornerPoints.Count - 1] != vc)
                        {
                            if (CornerPoints.Count > 0) CornerRoadIndices.Add(-1);

                            CornerPoints.Add(vc);
                            correspondingRoads.Add(i);
                            correspondingRoadVertices.Add(c);
                        }
                        CornerRoadIndices.Add(i);
                        CornerPoints.Add(va);
                        correspondingRoads.Add(i);
                        correspondingRoadVertices.Add(a);
                    }

                    ConnectedRoads[i].meshVertices[a].y = Y;
                    ConnectedRoads[i].meshVertices[c].y = Y;
                }

                if (CornerPoints[0] != CornerPoints[CornerPoints.Count - 1])
                {
                    CornerRoadIndices.Add(-1);
                }
                else
                {
                    CornerPoints.RemoveAt(CornerPoints.Count - 1);
                }

                //remove concave angles
                for (int i = 0; i < CornerPoints.Count; i++)
                {
                    var point = CornerPoints[i];
                    var prev = CornerPoints[(i - 1 + CornerPoints.Count) % CornerPoints.Count];
                    var next = CornerPoints[(i + 1) % CornerPoints.Count];

                    float side = Utility.GetSideOfLine(point, prev, next);
                    if (side > 0)
                    {
                        Vector3 projection = Utility.ProjectOnLine(point, prev, next);
                        projection.y = Y;

                        CornerPoints[i] = projection;
                        ConnectedRoads[correspondingRoads[i]].meshVertices[correspondingRoadVertices[i]] = projection;
                    }
                }


            }
            else
            {
                foreach (var c in tmpCorners)
                {
                    RoadCornerIndices.Add(CornerPoints.Count);
                    CornerRoadIndices.Add(CornerPoints.Count);
                    CornerPoints.Add(c);
                }
            }

            int s = 0;
            while (IsNotConvex() && s++ < 20)
            {
                MakeCrossroadBigger();
            }

            for (int i = 0; i < CornerPoints.Count; i++)
            {
                CornerPoints[i] = new Vector3(CornerPoints[i].x, Position.y, CornerPoints[i].z);
            }

            reverseCorners = new Vector3[CornerPoints.Count];
            for (int i = 0; i < CornerPoints.Count; i++)
            {
                reverseCorners[i] = CornerPoints[CornerPoints.Count - 1 - i];
                //placeAction(ConnectionPoints[i]);
            }
        }
        private bool IsNotConvex()
        {
            if (!Utility.IsInside(CornerPoints.ToArray(), Position, true)) return true;

            foreach (var point in CornerPoints)
            {
                var betterPoint = point + (Position - point).normalized * 0.01f;

                if (!Utility.IsInside(CornerPoints.ToArray(), betterPoint, true)) return true;
            }

            return false;
        }

        private void MakeCrossroadBigger()
        {
            for (int i = 0; i < ConnectedRoads.Count; i++)
            {
                int a, b, c, d;

                int l = ConnectedRoads[i].meshVertices.Length;
                if (ConnectedRoads[i].Points[0] == ConnectionPoints[i]) { a = 1; b = 3; c = 0; d = 2; }
                else { a = l - 2; b = l - 4; c = l - 1; d = l - 3; }

                float oldDist = Vector3.Distance(ConnectedRoads[i].meshVertices[a], ConnectedRoads[i].meshVertices[c]);

                ConnectedRoads[i].meshVertices[a] = MoveFurther(ConnectedRoads[i].meshVertices[a]);
                ConnectedRoads[i].meshVertices[c] = MoveFurther(ConnectedRoads[i].meshVertices[c]);

                Vector3 center = (ConnectedRoads[i].meshVertices[a] + ConnectedRoads[i].meshVertices[c]) / 2;

                ConnectedRoads[i].meshVertices[a] = center + (ConnectedRoads[i].meshVertices[a] - center).normalized * oldDist / 2;
                ConnectedRoads[i].meshVertices[c] = center + (ConnectedRoads[i].meshVertices[c] - center).normalized * oldDist / 2;

                CornerPoints[RoadCornerIndices[i]] = ConnectedRoads[i].meshVertices[c];
                if (RoadCornerIndices[i] + 1 < CornerPoints.Count)
                {
                    CornerPoints[RoadCornerIndices[i] + 1] = ConnectedRoads[i].meshVertices[a];
                }
            }
        }

        private Vector3 MoveFurther(Vector3 point, float distance = 1.1f)
        {
            return (point - Position) * distance + Position;
        }

        /// <summary>
        /// Correctly sorts lists representing connected roads.
        /// </summary>
        private void SortLists()
        {
            List<Vector3> newPoints = new List<Vector3>();
            List<Road> newRoads = new List<Road>();

            while (ConnectionPoints.Count > 0)
            {
                int min = 0;
                float minangle = float.MaxValue;
                for (int i = 0; i < ConnectionPoints.Count; i++)
                {
                    float angle = Vector3.SignedAngle(Vector3.forward, ConnectionPoints[i] - Position, Vector3.up);

                    if (angle < minangle)
                    {
                        min = i;
                        minangle = angle;
                    }
                }

                newPoints.Add(ConnectionPoints[min]);
                newRoads.Add(ConnectedRoads[min]);
                ConnectionPoints.RemoveAt(min);
                ConnectedRoads.RemoveAt(min);
            }

            ConnectionPoints = newPoints;
            ConnectedRoads = newRoads;
        }

        public Vector3 GetIntersectPoint(Vector3 A1, ref Vector3 A2, Vector3 B1, ref Vector3 B2)
        {
            bool found;

            Vector3 vector = Utility.GetIntersection(A1, A2, B1, B2, out found);

            if (!found) 
            {
                return (A1 + B1) / 2;
            }

            if (Utility.IsInLine(A1, A2, vector) && Vector3.Distance(A2, vector) < 20)
            {
                A2.x = vector.x;
                A2.z = vector.z;
            }

            if (Utility.IsInLine(B1, B2, vector) && Vector3.Distance(B2, vector) < 20)
            {
                B2.x = vector.x;
                B2.z = vector.z;
            }

            if (Utility.IsInLine(A1, vector, A2) && Utility.IsInLine(B1, vector, B2))
            {
                return vector;
            }

            if (Vector3.Distance(vector, A1) > 10)
            {
                return (A1 + B1) / 2;
            }


            return vector;
        }

        /// <summary>
        /// Generates crossroad sprite according to calculated corner points.
        /// </summary>
        /// <param name="parent"></param>
        public void GenerateSprite(Transform parent)
        {
            if (CornerPoints == null || CornerPoints.Count < 3) return;

            Mesh = new Mesh();
            AssignMeshComponents(parent);

            Vector3[] vertices = CornerPoints.ToArray();
            //Debug.Log(vertices.Length + " " + CornerPoints.Count);
            int[] triangles = new int[(vertices.Length - 2) * 3];
            Vector2[] uv = new Vector2[vertices.Length];
            Vector3[] normals = new Vector3[vertices.Length];

            for (int i = 0; i < vertices.Length - 2; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = 1 + i;
                triangles[i * 3 + 2] = 2 + i;
            }
            for (int i = 0; i < vertices.Length; i++)
            {
                uv[i] = new Vector2(1, (float)(i - 1) / (vertices.Length - 2));
                normals[i] = Vector3.up;
            }
            uv[0] = new Vector2(0, 0);

            Mesh.vertices = vertices;
            Mesh.triangles = triangles;
            Mesh.uv = uv;
            Mesh.normals = normals;
            Mesh.Optimize();
            Mesh.RecalculateBounds();
            col.sharedMesh = null;
            col.sharedMesh = Mesh;
        }

        private void AssignMeshComponents(Transform parent)
        {
            var meshHolder = new GameObject("crossroad");
            meshHolder.transform.parent = parent;
            var filter = meshHolder.transform.gameObject.AddComponent<MeshFilter>();
            meshHolder.transform.gameObject.AddComponent<MeshRenderer>();
            meshHolder.transform.GetComponent<MeshFilter>().mesh = Mesh;
            meshHolder.GetComponent<MeshRenderer>().material = Resources.Load("Crossroad") as Material;

            col = meshHolder.transform.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = Mesh;
            filter.sharedMesh = Mesh;
        }

        public override string ToString()
        {
            return "(cross " + id + ": " + ConnectedRoads.Count + " connections)";
        }

        /// <summary>
        /// Returns line edge distance from given point in given direction.
        /// Works recursively on connected roads that belong to given path.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="direction"></param>
        /// <param name="nextObjects"></param>
        /// <param name="previousObjects"></param>
        /// <returns></returns>
        public float GetDistanceInDirection(Vector3 start, Vector3 direction, List<object> nextObjects, List<object> previousObjects)
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

            if (!Utility.IsInside(reverseCorners, start)) return 0;
            start.y = 0;

            var tuple = Utility.GetNearestDist(reverseCorners, start, direction);

            if (tuple.Item2 < 0 || tuple.Item2 >= CornerRoadIndices.Count)
            {

                return 0;
            }

            int roadId = CornerRoadIndices[(2 * CornerRoadIndices.Count - 2 - tuple.Item2) % CornerRoadIndices.Count];
            if (roadId == -1) return tuple.Item1;

            Vector3 nextStart = start + direction.normalized * (tuple.Item1 + 0.01f);

            if (roadId < 0 || roadId >= ConnectedRoads.Count)
            {
                Debug.Log("errrr " + roadId);
                return 0;
            }

            int lineId = 0;
            if (ConnectedRoads[roadId].Points[0] == ConnectionPoints[roadId])
            {
                lineId = 1;
            }
            if (previousObjects.Count > 1 && previousObjects[1] == ConnectedRoads[roadId]) lineId = 1 - lineId;


            float distInRoad = ConnectedRoads[roadId].GetDistInDirection(nextStart, direction, lineId, nextObjects, previousObjects);

            return tuple.Item1 + distInRoad;
        }

        public bool IsOnCrossroad(Vector3 point)
        {
            return Utility.IsInside(reverseCorners, point);
        }
    }
}
    