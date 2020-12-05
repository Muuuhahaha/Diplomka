using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;
using System.IO;
using System.Globalization;
using Assets.Global.Scripts.Map;

namespace Assets.Global.Scripts.Map
{
    public class RealMap : MonoBehaviour
    {
        public GeneralSettings Settings;
        public string Path { get { return Settings.MapFilePath; } }
        [HideInInspector] public GameObject NodePrefab;
        [HideInInspector] public GameObject StraightRoadPrefab;
        [Unchangable] public GameObject RoadPrefab;
        [HideInInspector] public bool ShowNodes;
        [HideInInspector] public bool ShowStraightRoads;
        [Unchangable] public List<string> WayTypes;

        private Dictionary<Int64, MapNode> Nodes;
        private Dictionary<Int64, MapWay> Paths;

        public List<Crossroad> Crossroads;
        public List<Road> Roads;
        public List<ParkingArea> ParkingSpaces;
        private GameObject CrossroadObject;

        private Vector2 origCenter = Vector2.zero;
        private Vector2 origSize = Vector2.one;

        private Vector2 targetCenter = new Vector2(0, 0);
        private Vector2 targetSize = new Vector2(1000, 1000);

        private GameObject PathParent;

        public bool Autoscale = true;

        void Awake()
        {
            Load(Path);
        }

        /// <summary>
        /// loads map from given file
        /// </summary>
        /// <param name="path"></param>
        public void Load(string path)
        {
            Nodes = new Dictionary<long, MapNode>();
            Paths = new Dictionary<long, MapWay>();

            int i = 0;

            System.Random rand = GetRandom();

            using (XmlReader reader = XmlReader.Create(path))
            {
                while (reader.Read())
                {
                    if (reader.IsStartElement())
                    {
                        if (reader.Name == "node")
                        {
                            float x = float.Parse(reader.GetAttribute("lon"), CultureInfo.InvariantCulture);
                            float z = float.Parse(reader.GetAttribute("lat"), CultureInfo.InvariantCulture);
                            Int64 id = Int64.Parse(reader.GetAttribute("id"));
                            float y = rand.NextFloat(0, Settings.MaxHeight);

                            Nodes.Add(id, new MapNode(id, x, y, z));

                        }
                        else if (reader.Name == "way")
                        {
                            MapWay p = ReadPath(reader);
                            AddPathInfo(p);
                            Paths.Add(p.ID, p);
                        }
                    }
                }

            }

            Debug.Log(Nodes.Count + " nodes");
            Debug.Log(Paths.Count + " paths");

            CalculateScale();
            if (ShowNodes) DebugPlaceNodes();
            PlacePaths();

            GenerateCrossroads();
            GenerateParkingSpaces();
            GenerateCars();
        }

        /// <summary>
        /// generates map form given nodes and path.
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="paths"></param>
        public void SetMap(Dictionary<long, MapNode> nodes, Dictionary<long, MapWay> paths)
        {
            Nodes = nodes;
            Paths = paths;

            Debug.Log(Nodes.Count + " nodes");
            Debug.Log(Paths.Count + " paths");

            if (ShowNodes) DebugPlaceNodes();
            PlacePaths();

            GenerateCrossroads();
        }

        /// <summary>
        /// Decodes path from given input file.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private MapWay ReadPath(XmlReader reader)
        {
            Int64 id = Int64.Parse(reader.GetAttribute("id"));
            MapWay path = new MapWay(id);

            while (reader.Read())
            {
                if (reader.Name == "nd")
                {
                    Int64 nodeID = Int64.Parse(reader.GetAttribute("ref"));
                    MapNode node = Nodes[nodeID];
                    path.AddNode(node);

                }
                else if (reader.Name == "tag")
                {
                    if (reader.GetAttribute("k") == "highway")
                    {
                        path.Type = reader.GetAttribute("v");
                    }
                }
                else if (reader.Name == "way") { return path; }
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        private void AddPathInfo(MapWay path)
        {
            if (!IsCorrectType(path)) return;

            MapNode lastNode = null;

            foreach (var node in path.Nodes)
            {
                if (lastNode != null)
                {
                    lastNode.WayParts++;
                    node.WayParts++;
                }
                lastNode = node;
            }
        }

        private void CalculateScale()
        {
            float minx, miny, maxx, maxy;

            minx = miny = float.MaxValue;
            maxx = maxy = float.MinValue;


            foreach (var pair in Nodes)
            {
                MapNode n = pair.Value;

                if (n.X > maxx) maxx = n.X;
                if (n.Z > maxy) maxy = n.Z;
                if (n.X < minx) minx = n.X;
                if (n.Z < miny) miny = n.Z;
            }

            origCenter = new Vector2((maxx + minx) / 2, (maxy + miny) / 2);
            origSize = new Vector2(maxx - minx, maxy - miny);

            float r_earth = 6378000;
            float lat = (maxy + miny) / 2;

            float height = (maxy - miny) * Mathf.PI * 2 / 360 * r_earth;
            float width = (maxx - minx) * Mathf.PI * 2 / 360 * r_earth * Mathf.Cos(lat);

            targetSize = new Vector2(width, height);
        }

        private Vector3 Transform(MapNode orig)
        {
            if (!Autoscale) return new Vector3(orig.X, 0, orig.Z);

            float x = targetCenter.x + targetSize.x * (orig.X - origCenter.x) / origSize.x;
            float z = targetCenter.y + targetSize.y * (orig.Z - origCenter.y) / origSize.y;
            float y = orig.Y;

            return new Vector3(x, y, z);
        }

        private void DebugPlaceNodes()
        {
            foreach (var pair in Nodes)
            {
                MapNode n = pair.Value;

                var obj = Instantiate(NodePrefab, transform);
                Vector3 pos = Transform(n);

                obj.transform.localPosition = pos;
            }
        }

        /// <summary>
        /// Places simple roads using given map description
        /// </summary>
        private void PlacePaths()
        {
            foreach (var pair in Paths)
            {
                MapWay path = pair.Value;

                if (!IsCorrectType(path))
                {
                    continue;
                }

                for (int i = 0; i < path.Nodes.Count - 1; i++)
                {
                    PlacePathSegment(path.Nodes[i], path.Nodes[i + 1]);
                }
            }
        }

        /// <summary>
        /// Returns if given OSM way type should be included in generated map
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private bool IsCorrectType(MapWay path)
        {
            return WayTypes.Contains(path.Type);

        }

        private void PlacePathSegment(MapNode start, MapNode end)
        {
            PlacePathSegment(Transform(start), Transform(end));
        }

        /// <summary>
        /// Places simple rectangle road segment on given position.
        /// Only works if allowed by settings.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        private void PlacePathSegment(Vector3 start, Vector3 end)
        {
            if (!ShowStraightRoads) return; 

            float roadWidth = 5;
            float overlapLength = 1;

            Vector3 center = (start + end) / 2;
            Vector3 direction = start - end;
            float angle = Vector3.SignedAngle(direction, Vector3.forward, Vector3.up);
            float length = Vector3.Distance(start, end);

            var obj = Instantiate(StraightRoadPrefab, transform);
            obj.transform.localPosition = center;
            obj.transform.Rotate(Vector3.up, -angle);
            obj.transform.localScale = new Vector3(roadWidth, 1, length + overlapLength);
        }

        /// <summary>
        /// Calculates correct crossroad corner points and generates crossroad sprites.
        /// Also Fixes problems with road meshes.
        /// </summary>
        private void GenerateCrossroads()
        {
            Crossroads = new List<Crossroad>();
            Roads = new List<Road>();
            System.Random ran = GetRandom();

            //decoding paths and generating required crossroads.
            foreach (var pair in Paths)
            {
                var path = pair.Value;

                if (!IsCorrectType(path))
                {
                    continue;
                }

                List<MapNode> middleNodes = new List<MapNode>();

                var last = path.Nodes[path.Nodes.Count - 1];
                var first = path.Nodes[0];
                foreach (var node in path.Nodes)
                {
                    middleNodes.Add(node);

                    //There should be crossroad on given map node.
                    if (node.WayParts > 2 || (node.WayParts <= 2 && (node == last || node == first)))
                    {
                        if (node.Crossroad == null)
                        {
                            node.Crossroad = new Crossroad(Transform(node));
                            Crossroads.Add(node.Crossroad);
                        }

                        //don't generate new map path for first node
                        if (middleNodes.Count > 1)
                        {   
                            Roads.Add(Road.FromNodeList(middleNodes, Transform, transform, RoadPrefab, ran.NextFloat(Settings.MinLineWidth, Settings.MaxLineWidth), Settings.CrossroadSize));
                            middleNodes = new List<MapNode>();
                            middleNodes.Add(node);
                        }
                    }
                }

                if (middleNodes.Count > 1)
                {
                    Roads.Add(Road.FromNodeList(middleNodes, Transform, transform, RoadPrefab, ran.NextFloat(Settings.MinLineWidth, Settings.MaxLineWidth), Settings.CrossroadSize));
                }
            }

            CrossroadObject = new GameObject("crossroads");
            CrossroadObject.transform.parent = transform;

            //Destroy crossroads with only 2 connected roads
            List<Crossroad> destroyedCrossroads = new List<Crossroad>();
            foreach (var cross in Crossroads)
            {
                if (cross.ConnectedRoads.Count == 2)
                {
                    var r = cross.ConnectedRoads[0];
                    var s = cross.ConnectedRoads[1];
                    if (r == s) continue;

                    if (r.StartCross == s.EndCross && r.StartCross != null)
                    {
                        r = s;
                        s = cross.ConnectedRoads[0];
                    }

                    Roads.Remove(s);
                    destroyedCrossroads.Add(cross);

                    r.MergeWith(s);
                }
                else if (cross.ConnectedRoads.Count == 1)
                {
                    var r = cross.ConnectedRoads[0];
                    if (r.StartCross == cross) r.StartCross = null;
                    if (r.EndCross == cross) r.EndCross = null;

                    destroyedCrossroads.Add(cross);
                }
            }
            foreach (var cross in destroyedCrossroads)
            {
                Crossroads.Remove(cross);
            }

            //generating road sprites.
            foreach (var road in Roads)
            {
                road.GenerateSprite(transform, RoadPrefab);
            }
            
            //calculating corner points of crossroads
            foreach (var cross in Crossroads)
            {
                cross.CalculateCornerPoins(Settings.DisableMovingCrossroadCorners);
                cross.GenerateSprite(CrossroadObject.transform);
            }

            //fixes wrong generation on road meshes.
            foreach (var road in Roads)
            {
                road.RemoveCreases();
                road.RemoveCreases();
                road.RegenerateMesh();
            }
        }

        /// <summary>
        /// generates random parking spaces on roads.
        /// </summary>
        public void GenerateParkingSpaces()
        {
            System.Random rand = GetRandom();
            ParkingSpaces = new List<ParkingArea>();

            foreach (var road in Roads)
            {
                for (int i = 0; i < Settings.ParkingAreasPerRoad; i++)
                {
                    road.AddRandomParkingSpace(road.Sprite.transform, rand, i % 2, Settings);
                }

                foreach (var ps in road.ParkingSpaces[0].Union(road.ParkingSpaces[1]))
                {
                    ParkingSpaces.Add(ps);
                }
            }
        }

        /// <summary>
        /// Places obstacles on random parking spaces.
        /// </summary>
        public void GenerateCars()
        {
            System.Random rand = GetRandom();
            IdleCarManager carManager = GameObject.FindObjectOfType<IdleCarManager>();

            foreach (var parkingSpace in ParkingSpaces)
            {
                int size = parkingSpace.ParkSpaceCount;
                bool[] obstacles = new bool[size];
                int carsCount = Settings.PSInitCars;
                if (carsCount >= size) carsCount = size - 1;

                while (carsCount > 0)
                {
                    int index = rand.Next(size);
                    if (obstacles[index]) continue;

                    obstacles[index] = true;
                    carsCount--;
                }

                for (int i = 0; i < size; i++)
                {
                    if (!obstacles[i]) continue;

                    GameObject car = Instantiate(Settings.CarPrefab, transform);
                    car.transform.position = parkingSpace.ParkSlotCenters[i] + Vector3.up * 0.38f;
                    car.transform.forward = parkingSpace.ParkedDirection;

                    carManager.AddIdleCar(car, parkingSpace, i);
                }
            }
        }

        /// <summary>
        /// returns random number generator with either random or fixed seed according to current setings.
        /// </summary>
        /// <returns></returns>
        private System.Random GetRandom()
        {
            if (Settings.UseFixedMapSeedInEditor && Application.isEditor)
            {
                return new System.Random(0);
            }
            else
            {
                return new System.Random();
            }
        }

        public void DestroyMap()
        {
            if (Crossroads != null)
            {
                foreach (var cross in Crossroads)
                {
                    Destroy(cross.Sprite);
                }
                Destroy(CrossroadObject);
            }

            if (Roads != null)
            {
                foreach (var road in Roads)
                {
                    Destroy(road.Sprite);
                }
            }

            Crossroads = null;
            Roads = null;
        }
    }
}
