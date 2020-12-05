using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Global.Scripts.Map
{
    /// <summary>
    /// Temporary class used for loading OSM XML files.
    /// </summary>
    public class MapWay
    {
        public Int64 ID;
        public List<MapNode> Nodes;
        public string Type;

        public MapWay(Int64 id)
        {
            ID = id;
            Nodes = new List<MapNode>();
            Type = "";
        }

        public void AddNode(MapNode node)
        {
            Nodes.Add(node);
        }
    }
}
