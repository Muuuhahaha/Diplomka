using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Assets.Global.Scripts.Map;

namespace Assets.Global.Scripts
{
    /// <summary>
    /// This class is mainly used for switching car sprites in FullSimulation path type.
    /// </summary>
    class IdleCarManager : MonoBehaviour
    {
        private List<IdleCar> IdleCars = new List<IdleCar>();
        private System.Random rand = new System.Random();
        public GeneralSettings Settings;

        public IdleCar GetRandomIdleCar()
        {
            return GetIdleCar(rand.Next(IdleCars.Count));
        }

        public IdleCar GetIdleCar(int index)
        {
            IdleCar idleCar = IdleCars[index];
            IdleCars.RemoveAt(index);

            //Debug.Log("removed idle car from " + idleCar.ParkingSpace.ConnectedRoad.id + "/" + idleCar.ParkingSpace.Id + "/" + idleCar.ParkingSlotId);

            idleCar.Car.GetComponent<Rigidbody>().isKinematic = false;
            idleCar.ParkingSpace.ObstaclesOnParkingSlots[idleCar.ParkingSlotId] = false;

            return idleCar;
        }

        /// <summary>
        /// Returns IdleCar object of given GameObject.
        /// </summary>
        /// <param name="car"></param>
        /// <returns></returns>
        public IdleCar GetCar(GameObject car)
        {
            for (int i = 0; i < IdleCars.Count; i++)
            {
                if (IdleCars[i].Car == car)
                {
                    return GetIdleCar(i);
                }
            }

            return null;
        }

        /// <summary>
        /// Adds given GameObject to idle car list.
        /// </summary>
        public void AddIdleCar(GameObject car, ParkingArea parkingSpace, int parkingSlotId)
        {
            AddIdleCar(new IdleCar(car, parkingSpace, parkingSlotId));
        }

        /// <summary>
        /// Adds given GameObject to idle car list.
        /// </summary>
        /// <param name="idleCar"></param>
        public void AddIdleCar(IdleCar idleCar)
        {
            IdleCars.Add(idleCar);
            idleCar.Car.GetComponent<Rigidbody>().isKinematic = true;
            idleCar.Car.transform.parent = transform;

            Rigidbody rb = idleCar.Car.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            idleCar.ParkingSpace.ObstaclesOnParkingSlots[idleCar.ParkingSlotId] = true;
        }

        public void FixedUpdate()
        {
            //DebugManagerIntegrity();
        }

        /// <summary>
        /// Debug function
        /// </summary>
        private void DebugManagerIntegrity()
        {
            foreach (var ic in IdleCars)
            {
                var pos = ic.ParkingSpace.ParkSlotCenters[ic.ParkingSlotId];
                var cpos = ic.Car.transform.position;


                Debug.DrawLine(pos, pos + Vector3.up, Color.blue);
                Debug.DrawLine(cpos, cpos + Vector3.up, Color.cyan);


                var ps = ic.ParkingSpace;

                if (!ps.ObstaclesOnParkingSlots[ic.ParkingSlotId])
                {
                    Debug.DrawLine(ps.ParkSlotCenters[ic.ParkingSlotId], Vector3.zero);
                    Debug.Log("something wrong on " + ic.ParkingSpace.ConnectedRoad.id + "/" + ic.ParkingSpace.Id + "/" + ic.ParkingSlotId);
                    Debug.Break();
                }

                for (int i = 0; i < ps.ParkSpaceCount; i++)
                {
                    Vector3 center = ps.ParkSlotCenters[i] + Vector3.forward * 0.2f;

                    Color c = Color.green;
                    if (ps.ObstaclesOnParkingSlots[i]) c = Color.red;

                    if (Settings.ShowParkingSpaceAvailibility)
                    {
                        Debug.DrawLine(center, center + Vector3.up * 10, c);
                    }
                }
            }
        }

        /// <summary>
        /// Debug function.
        /// </summary>
        public void DebugCalculateEmptySpaces()
        {
            int empty = 0;
            int half = 0;
            string str = "";

            RealMap map = GameObject.FindObjectOfType<RealMap>();

            foreach (var ps in map.ParkingSpaces)
            {
                for (int i = 0; i < ps.ParkSpaceCount; i++)
                {
                    if (!ps.ObstaclesOnParkingSlots[i])
                    {
                        half++;

                    }

                    if (ps.AssignedAgents[i] == 0)
                    {
                        empty++;
                    }
                    else
                    {
                        str += " " + ps.AssignedAgents[i];
                    }
                }
            }

            Debug.Log("empty spaces: " + half + "/" + empty + str);
        }
    }

    /// <summary>
    /// Instances of this class represent specific idle cars.
    /// </summary>
    public class IdleCar
    {
        public GameObject Car;
        public ParkingArea ParkingSpace;
        public int ParkingSlotId;

        public IdleCar(GameObject car, ParkingArea parkingSpace, int parkingSlotId)
        {
            Car = car;
            ParkingSpace = parkingSpace;
            ParkingSlotId = parkingSlotId;
        }
    }
}
