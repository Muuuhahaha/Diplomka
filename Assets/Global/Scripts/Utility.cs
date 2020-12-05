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
    /// This class contains some usefull static methods that we use on various place in our project.
    /// </summary>
    class Utility
    {
        /// <summary>
        /// This method returns distance and id of first side of polygon given by CORNERS variable of
        /// first side that we intersect if we start on START point and go in DIRECTION.
        /// </summary>
        /// <param name="corners"></param>
        /// <param name="start"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public static Tuple<float, int> GetNearestDist(Vector3[] corners, Vector3 start, Vector3 direction)
        {
            float min = float.MaxValue;
            int index = -1;
            float[] distances = GetDistancesInDirection(corners, start, direction);

            for (int i = 0; i < distances.Length; i++)
            {
                if (distances[i] < min && distances[i] > 0.0001)
                {
                    min = distances[i];
                    index = i;
                }
            }

            if (min == float.MaxValue) return new Tuple<float, int>(0, -1);

            return new Tuple<float, int>(min, index);
        }


        /// <summary>
        /// Returns closest distance to each side of given polygon if we start from START and go in DIRECTION.
        /// </summary>
        /// <param name="corners"></param>
        /// <param name="start"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public static float[] GetDistancesInDirection(Vector3[] corners, Vector3 start, Vector3 direction)
        {
            float[] distances = new float[corners.Length];

            if (!IsInside(corners, start)) return Enumerable.Repeat(float.MaxValue, corners.Length).ToArray();
            
            for (int i = 0; i < corners.Length; i++)
            {
                distances[i] = GetIntersecionBetweenPoints(start, direction, corners[i], corners[(i + 1) % corners.Length], out bool good);
            }

            return distances;
        }

        /// <summary>
        /// Returns intersection point of lines given by (A,B) and (START, DIRECTION)
        /// </summary>
        /// <param name="start"></param>
        /// <param name="direction"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="isBetween">returns true in intersection is between A and B</param>
        /// <returns></returns>
        public static float GetIntersecionBetweenPoints(Vector3 start, Vector3 direction, Vector3 a, Vector3 b, out bool isBetween)
        {
            bool found;
            isBetween = false;
            Vector3 point = GetIntersection(start, start + direction, a, b, out found);
            Vector3 error = (a - b).normalized * 0.01f;

            if (!found) return float.MaxValue;
            if (!IsInLine(a + error, point, b - error)) return float.MaxValue;
            if (IsInLine(point, start, start + direction)) return float.MaxValue;

            isBetween = true;
            return Vector3.Distance(point, start);
        }

        /// <summary>
        /// Returns intersection point of two lines
        /// </summary>
        /// <param name="A1"></param>
        /// <param name="A2"></param>
        /// <param name="B1"></param>
        /// <param name="B2"></param>
        /// <param name="found"></param>
        /// <returns></returns>
        public static Vector3 GetIntersection(Vector3 A1, Vector3 A2, Vector3 B1, Vector3 B2, out bool found)
        {
            float tmp = (B2.x - B1.x) * (A2.z - A1.z) - (B2.z - B1.z) * (A2.x - A1.x);

            if (tmp == 0)
            {
                // No solution!
                found = false;
                return Vector2.zero;
            }

            float mu = ((A1.x - B1.x) * (A2.z - A1.z) - (A1.z - B1.z) * (A2.x - A1.x)) / tmp;

            found = true;

            float y = 0;

            return new Vector3(
                B1.x + (B2.x - B1.x) * mu,
                y,
                B1.z + (B2.z - B1.z) * mu
            );
        }

        /// <summary>
        /// Returns whether given point is in given polygon.
        /// </summary>
        /// <param name="corners"></param>
        /// <param name="point"></param>
        /// <param name="reverse"></param>
        /// <returns></returns>
        public static bool IsInside(Vector3[] corners, Vector3 point, bool reverse = false)
        {
            for (int i = 0; i < corners.Length; i++)
            {
                float side = GetSideOfLine(point, corners[i], corners[(i + 1) % corners.Length]);

                if ((side > 0 && !reverse) || (side < 0 && reverse)) return false;
            }

            return true;
        }

        /// <summary>
        /// Returns number representing on which side of given line (A,B) is given point.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static float GetSideOfLine(Vector3 point, Vector3 a, Vector3 b)
        {
            return (point.x - a.x) * (b.z - a.z) - (point.z - a.z) * (b.x - a.x);
        }

        /// <summary>
        /// Returns distanceof given point from given line.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static float GetDistFromLine(Vector3 point, Vector3 a, Vector3 b)
        {
            return Vector3.Distance(ProjectOnLine(point, a, b), point);
        }

        /// <summary>
        /// Gets closest point on line (A,B) from POINT.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Vector3 ProjectOnLine(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 translation = Vector3.Project(point - a, b - a);
            Vector3 projection = a + translation;

            return projection;
        }

        /// <summary>
        /// Returns if POINT is on line (A,B).
        /// </summary>
        /// <param name="point"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool IsOnLine(Vector3 point, Vector3 a, Vector3 b)
        {
            return GetDistFromLine(point, a, b) == 0;
        }

        /// <summary>
        /// Assuming A, B and C are on the same line preforms a quick check if they are correctly ordered.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public static bool IsInLine(Vector3 a, Vector3 b, Vector3 c)
        {
            if (a.x > b.x && b.x > c.x) return true;
            if (a.x < b.x && b.x < c.x) return true;
            if (a.z > b.z && b.z > c.z) return true;
            if (a.z < b.z && b.z < c.z) return true;

            return false;
        }

        /// <summary>
        /// Returns random number form normal distribution
        /// </summary>
        /// <param name="mean"></param>
        /// <param name="stdDev"></param>
        /// <param name="rand"></param>
        /// <returns></returns>
        public static float RandNormal(float mean, float stdDev, System.Random rand)
        {
            double u1 = 1.0 - rand.NextDouble();
            double u2 = 1.0 - rand.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)

            return (float)(mean + stdDev * randStdNormal); //random normal(mean,stdDev^2)
        }

        /// <summary>
        /// Returns signed angle of given 2 vectors from bird perspective.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static float TopdownSignedAngle(Vector3 first, Vector3 second)
        {
            first.y = 0;
            second.y = 0;

            return Vector3.SignedAngle(first, second, Vector3.up);
        }

        /// <summary>
        /// Retruns F^P with the same sign as F.
        /// </summary>
        /// <param name="f"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static float SignedPow(float f, float p)
        {
            if (f < 0) return -Mathf.Pow(-f, p);
            else return Mathf.Pow(f, p);
        }
    }
}
