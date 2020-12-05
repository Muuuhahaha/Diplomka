using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using Assets.Global.Scripts.Map;

namespace Assets.Global.Scripts
{

    /// <summary>
    /// This class some extension methods used in our project.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// retruns random float value from given range.
        /// </summary>
        /// <param name="rand"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static float NextFloat(this System.Random rand, float min, float max)
        {
            return (float)(min + rand.NextDouble() * (max - min));
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
        {
            if (dict.ContainsKey(key))
            {
                return dict[key];
            }
            else
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// fills dictionary with values from given yaml file.
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="file"></param>
        public static void FillFromYaml(this Dictionary<string, string> dict, string file)
        {
            StreamReader sr = new StreamReader(file);

            string line = sr.ReadLine();

            while (line != null)
            {
                var splited = line.Split(':');

                if (splited.Length != 2) continue;

                dict.Add(splited[0].Trim(), splited[1].Trim());

                line = sr.ReadLine();
            }

            sr.Close();
        }

        public static float[] ToFloatArray(this string[] array)
        {
            List<float> values = new List<float>();

            foreach (var str in array)
            {
                float f;

                //if (!float.TryParse(str, System.Globalization.NumberStyles.Float,   out f)) throw new FormatException("Can't convert to float: " + str + ".");
                
                f = float.Parse(str, System.Globalization.CultureInfo.InvariantCulture);

                values.Add(f);
            }   

            return values.ToArray();
        }

        public static string ToFullString(this Vector3 vec)
        {
            return "(" + vec.x + ", " + vec.y + ", " + vec.z + ")";
        }

        public static T GetRandomMember<T>(this List<T> list, System.Random rand)
        {
            return list[rand.Next(list.Count)];
        }

        public static T GetMiddleMember<T>(this List<T> list)
        {
            return list[list.Count / 2];
        }







        /// <summary>
        /// This extension method describes whether it is needed to switch brain in given path type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool SwitchToParkingBehaviour(this PathType type)
        {
            return type == PathType.FullSimulation 
                || type == PathType.PathWithParking
                || type == PathType.PathFromParkingToParking;
        }

        /// <summary>
        /// This extension method describes whether it is needed to switch car objects in given path type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool SwitchesCarObjects(this PathType type)
        {
            return type == PathType.FullSimulation;
        }

        /// <summary>
        /// This extension method describes initial behaviour state of given path type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static BehaviourState InititialBehaviourState(this PathType type)
        {
            if (type == PathType.FullSimulation 
                || type == PathType.OnlyPath 
                || type == PathType.PathWithParking
                || type == PathType.PathFromParkingSpace
                || type == PathType.PathFromParkingToParking)
            {
                return BehaviourState.Driving;
            }

            return BehaviourState.Parking;
        }

        /// <summary>
        /// This extension method describes whether given path type winishes on parking space.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool EndsOnParkingSpace(this PathType type)
        {
            return type == PathType.FullSimulation
                || type == PathType.OnlyParking
                || type == PathType.PathWithParking
                || type == PathType.PathFromParkingToParking;
        }

        /// <summary>
        /// This extension method describes parameters of given PathType value.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool StartsOnEmptyParkingSpace(this PathType type)
        {
            return type == PathType.PathFromParkingSpace
                || type == PathType.PathFromParkingToParking;
        }

        /// <summary>
        /// This extension method describes parameters of given PathType value.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool StartsOnParkingSpace(this PathType type)
        {
            return type == PathType.PathFromParkingSpace
                || type == PathType.FullSimulation
                || type == PathType.PathFromParkingToParking;
        }
    }
}
