using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


namespace Assets.Global.Scripts.Map
{
    /// <summary>
    /// This class represents single parking area on map. Parking area consists of number of parking spaces, some of which may be filled by obstacle cars.
    /// </summary>
    public class ParkingArea
    {
        public Road ConnectedRoad;
        public int RoadPartId;
        public int RoadLineId;
        /// <summary>
        /// True if it is on side with line "0".
        /// </summary>
        public bool RoadSide;

        public Vector3[] Corners;
        public Vector3[] ReverseCorners;
        private Vector3 Direction;
        public Vector3 ParkedDirection;
        public int ConnectedSideId;

        public int Id;
        private static int IdCounter = 0;

        public Mesh Mesh;
        public MeshCollider col;
        private GameObject meshHolder;

        public Vector3[] ParkSlotCenters;
        public bool[] ObstaclesOnParkingSlots;
        public int ParkSpaceCount { get { return ParkSlotCenters.Length; } }

        private float SpaceLength = 6f;
        private float SpaceWidth = 2.5f;
        const float MinOffset = 3f;

        public SpaceType Type;
        public int[] AssignedAgents;

        private GeneralSettings Settings;

        public ParkingArea(Road parentRoad, int partId, int side, Vector3 start, Vector3 end, Vector3 direction, Transform parent, System.Random rand, GeneralSettings settings)
        {
            ConnectedRoad = parentRoad;
            RoadPartId = partId;
            RoadLineId = side;
            RoadSide = side == 0;
            Direction = direction;

            Type = (SpaceType)rand.Next(2);
            Type = SpaceType.Perpendicular;

            SpaceLength = settings.PSLength;
            SpaceWidth = settings.PSWidth;
            Settings = settings;

            SetCorners(start, end, direction, rand);
            GenerateSprite(parent);

            Id = IdCounter++;
        }

        /// <summary>
        /// Calculates corner of parking area rectangle given description.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="direction"></param>
        /// <param name="rand"></param>
        private void SetCorners(Vector3 start, Vector3 end, Vector3 direction, System.Random rand)
        {
            Corners = new Vector3[4];
            ReverseCorners = new Vector3[4];
            direction = direction.normalized;
            ConnectedSideId = 3;

            float maxWidth = Vector3.Distance(start, end);
            float targetWidth = SpaceWidth;
            float targetDepth = SpaceLength;
            ParkedDirection = -direction;

            if (Type == SpaceType.Parallel)
            {
                targetWidth = SpaceLength;
                targetDepth = SpaceWidth;
                ParkedDirection = (end - start).normalized;
            }

            int maxSpaces = Mathf.FloorToInt(maxWidth / targetWidth / 2);
            int numberOfSpaces = rand.Next(Settings.PSMinSlots, Settings.PSMaxSlots + 1);
            targetWidth *= numberOfSpaces;

            float offset = rand.NextFloat(MinOffset, maxWidth - targetWidth - MinOffset);
            Vector3 dif = (end - start).normalized;

            Corners[0] = start + dif * offset;
            Corners[1] = Corners[0] + direction * targetDepth;

            Corners[3] = Corners[0] + dif * targetWidth;
            Corners[2] = Corners[3] + direction * targetDepth;

            for (int i = 0; i < 4; i++)
            {
                ReverseCorners[i] = Corners[3 - i];
            }

            AddSpaceCenters(numberOfSpaces); 
        }

        /// <summary>
        /// Calculates correct centers of parking spaces on area.
        /// </summary>
        /// <param name="numberOfSpaces"></param>
        private void AddSpaceCenters(int numberOfSpaces)
        {
            Vector3 dif = (Corners[3] - Corners[0]) / numberOfSpaces;
            Vector3 dir = (Corners[1] - Corners[0]);

            Vector3 absCenter = (Corners[0] + Corners[3] + Corners[1] + Corners[2]) / 4;
            absCenter += dir * 0.07f;

            ParkSlotCenters = new Vector3[numberOfSpaces];
            ObstaclesOnParkingSlots = new bool[numberOfSpaces];
            AssignedAgents = new int[numberOfSpaces];
            for (int i = 0; i < numberOfSpaces; i++)
            {
                Vector3 center = absCenter + dif * (i - (numberOfSpaces - 1) / 2f);
                ParkSlotCenters[i] = center;

                center += dif;
            }
        }

        /// <summary>
        /// Generates sprite of parking area.
        /// </summary>
        /// <param name="parent"></param>
        public void GenerateSprite(Transform parent)
        {
            Mesh = new Mesh();
            AssignMeshComponents(parent);

            Vector3[] vertices = Corners;
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
                normals[i] = Vector3.up;
            }
            uv[1] = new Vector2(0, 0);
            uv[0] = new Vector2(0, 1);
            uv[2] = new Vector2(1, 0);
            uv[3] = new Vector2(1, 1);

            Mesh.vertices = vertices;
            Mesh.triangles = triangles;
            Mesh.uv = uv;
            Mesh.normals = normals;
            Mesh.Optimize();
            Mesh.RecalculateBounds();
            col.sharedMesh = null;
            col.sharedMesh = Mesh;

            meshHolder.GetComponent<MeshRenderer>().material.mainTextureScale = new Vector3(ParkSlotCenters.Length, 1);
        }


        private void AssignMeshComponents(Transform parent)
        {
            meshHolder = new GameObject("parking space " + Id);
            meshHolder.transform.parent = parent;
            var filter = meshHolder.transform.gameObject.AddComponent<MeshFilter>();
            meshHolder.transform.gameObject.AddComponent<MeshRenderer>();
            meshHolder.transform.GetComponent<MeshFilter>().mesh = Mesh;
            var mr = meshHolder.GetComponent<MeshRenderer>();
            mr.material = Resources.Load("ParkingSpace") as Material;

            col = meshHolder.transform.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = Mesh;
            filter.sharedMesh = Mesh;
        }

        /// <summary>
        /// Recursively returns distance of path edge from given point.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="direction"></param>
        /// <param name="nextObjects"></param>
        /// <param name="previousObjects"></param>
        /// <returns></returns>
        public float GetDistanceInDirection(Vector3 start, Vector3 direction, List<object> nextObjects, List<object> previousObjects)
        {
            if (!IsOnParkingSpace(start)) return 0;
            start.y = 0;

            var tuple = Utility.GetNearestDist(ReverseCorners, start, direction);

            float distInRoad = 0;

            if (tuple.Item2 == ConnectedSideId)
            {
                Vector3 nextStart = start + direction.normalized * (tuple.Item1 + 0.01f);
                distInRoad = ConnectedRoad.GetDistInDirection(nextStart, direction, RoadLineId, nextObjects, previousObjects);
            }

            return tuple.Item1 + distInRoad;
        }

        public bool IsOnParkingSpace(Vector3 point)
        {
            return Utility.IsInside(ReverseCorners, point);
        }

        /// <summary>
        /// Returns id and center of random parking space on area.
        /// </summary>
        /// <param name="allowOccupied"></param>
        /// <returns></returns>
        public (Vector3, int) GetRandomSpace(bool allowOccupied = false)
        {
            System.Random rand = new System.Random();

            for (int i = 0; i < 1000; i++)
            {
                int id = rand.Next(ParkSpaceCount);

                if (!ObstaclesOnParkingSlots[id] && AssignedAgents[id] == 0)
                {
                    return (ParkSlotCenters[id], id);
                }
            }

            throw new Exception();
        }

        /// <summary>
        /// returns whether all parking spaces are currently filled or reserved.
        /// </summary>
        /// <returns></returns>
        public bool IsFull()
        {
            for (int i = 0; i < ParkSpaceCount; i++)
            {
                if (!ObstaclesOnParkingSlots[i] && AssignedAgents[i] == 0)
                {
                    //TODO reserved spots
                    return false;
                }
            }

            return true;
        }
    }

    public enum SpaceType
    {
        Perpendicular = 0,
        Parallel
    }

}
