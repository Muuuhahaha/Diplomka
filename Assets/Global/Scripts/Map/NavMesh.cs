using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Assets.Global.Scripts.Map;
using Assets.Global.Scripts.Agents;

namespace Assets.Global.Scripts.Map
{
    /// <summary>
    /// Abstract class defining generic navigation behaviour.
    /// </summary>
    abstract class NavMesh
    {
        public GameObject Car;
        public GenericAgent Agent;

        public CarStatus Status { protected set; get; }
        public Road CurrentRoad;
        public Crossroad CurrentCrosroad;
        public ParkingArea CurrentParkingSpace;

        public enum CarStatus
        {
            OnRoad, 
            OnCrossroad, 
            Other
        }
    }
}
