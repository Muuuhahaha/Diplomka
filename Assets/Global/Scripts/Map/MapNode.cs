using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Assets.Global.Scripts.Map;

namespace Assets.Global.Scripts.Map
{
    /// <summary>
    /// Temporary class used for loading OSM XML files.
    /// </summary>
    public class MapNode
    {
        public Int64 ID;
        public float X;
        public float Y;
        public float Z;
        public int WayParts;
        public Crossroad Crossroad;

        public MapNode(Int64 id, float x, float z)
        {
            ID = id;
            X = x;
            Y = 0;
            Z = z;
            WayParts = 0;
        }

        public MapNode(Int64 id, float x, float y, float z)
        {
            ID = id;
            X = x;
            Y = y;
            Z = z;
            WayParts = 0;
        }

        public Vector3 ToVector3 ()
        {
            return new Vector3(X, Y, Z);
        }
    }
}
