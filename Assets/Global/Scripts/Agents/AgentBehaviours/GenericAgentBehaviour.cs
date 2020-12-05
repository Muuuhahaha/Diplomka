using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Assets.Global.Scripts.Map;
using Barracuda;

namespace Assets.Global.Scripts.Agents.AgentBehaviours
{
    [Serializable]
    abstract class GenericAgentBehaviour
    {
        protected GenericAgent Agent;

        [ReadOnly] protected bool AddCrossroadObservations;
        [ReadOnly] protected bool AddTimeObservations;
        [ReadOnly] protected bool AddCrashInfoObservations;
        [ReadOnly] protected bool AddObstacleOrientationObservations;
        [HideInInspector] public float[] Angles;
        [HideInInspector] public float[] CarDetectionAngles;
        [HideInInspector] public float[] NextPointDistances;
        [HideInInspector] private float[] DetectionVectorsSubstractions;

        public float RoadDamage { get; private set; }
        public float CrossroadDamage { get; private set; }
        public float ParkingSpaceDamage { get; private set; }

        [ReadOnly] public float ClosestObstacleDistance = 1;


        protected Path Path { get { return PathChecker.GeneratedPath; } }

        public GeneralSettings Settings { get { return Agent.Settings; } }
        public PathChecker PathChecker { get { return Agent.PathChecker; } }
        protected Transform transform { get { return Agent.GetTransform(); } }
        protected Rigidbody _rb { get { return Agent._rb; } }
        protected bool OutputCarGizmos { get { return Agent.OutputCarGizmos; } }

        protected LastingDebugDay[] DebugRays;

        protected int rayId = 0;


        //crash info
        protected int TimeSinceLastCrash;
        [ReadOnly] public float NumberOfCrashes;

        public virtual void Setup(GenericAgent agent)
        {
            this.Agent = agent;

            LoadParamsFromYaml();
            CalculateSubstractions();

            DebugRays = new LastingDebugDay[GetObservationsCount()];
            for (int i = 0; i < DebugRays.Length; i++)
            {
                DebugRays[i] = new LastingDebugDay();
            }
        }


        public virtual void FixedUpdate()
        {
            DrawDebugRays();
            TimeSinceLastCrash++;
        }

        public abstract void GeneratePath();
        public virtual void ResetBehaviour()
        {
            NumberOfCrashes = 0;
            TimeSinceLastCrash = Settings.MinFramesBetweenCrashes;

            RoadDamage = 0;
            CrossroadDamage = 0;
            ParkingSpaceDamage = 0;
        }

        protected void ResetObservations()
        {
            Agent.Observations.Clear();
            Agent.RoadObservations.Clear();
            Agent.ObstacleObservations.Clear();
            Agent.PathObservations.Clear();
            Agent.OtherObservations.Clear();

            rayId = 0;
            ClosestObstacleDistance = Settings.ViewDistance;
        }


        public abstract void CollectObservations();

        protected void CollectRoadEdgeObservations()
        {
            PathChecker.UpdatePosition();

            //road edge detection observations
            foreach (var angle in Angles)
            {
                float dist = PathChecker.GetDistanceInDirection(transform.localPosition, angle);
                if (Settings.ViewDistance < dist) dist = Settings.ViewDistance;
                AddVectorObs(InverseNormalizeValue(dist, Settings.ViewDistance), ObservationType.Road);

                AddDebugRay(rayId++, angle, dist, Color.white, Settings.DebugRaysRoadDistance && OutputCarGizmos);
            }
        }

        protected void CollectCarObservations()
        {
            //car detection observations
            for (int i = 0; i < CarDetectionAngles.Length; i++)
            {
                Transform car;
                float dist = GetCarDistance(CarDetectionAngles[i], DetectionVectorsSubstractions[i], out car);
                AddVectorObs(InverseNormalizeValue(dist, Settings.ViewDistance), ObservationType.Obstacle);

                //if (ClosestObstacleDistance > dist) ClosestObstacleDistance = dist;

                Vector3 rayDirection = Quaternion.AngleAxis(CarDetectionAngles[i], Vector3.up) * transform.forward;
                rayDirection = new Vector3(rayDirection.x, 0, rayDirection.z).normalized;

                if (car == null)
                {
                    AddVectorObs(0, ObservationType.Obstacle);
                    AddVectorObs(0, ObservationType.Obstacle);
                    if (AddObstacleOrientationObservations)
                    {
                        AddVectorObs(0, ObservationType.Obstacle);
                    }

                    AddDebugRay(rayId++, CarDetectionAngles[i], dist, Color.green, Settings.DebugRaysCarDistance && OutputCarGizmos, DetectionVectorsSubstractions[i]);
                }
                else
                {
                    var rb = car.GetComponent<Rigidbody>();
                    Vector3 speed = rb.velocity;
                    if (Settings.UseRelativeCarSpeed) speed -= _rb.velocity;

                    Agent.helperGO.transform.rotation = Quaternion.LookRotation(rayDirection, Vector3.up);
                    Vector3 localVelocity = Agent.helperGO.transform.InverseTransformVector(speed);

                    AddScaledSpeedObservation(localVelocity.x, ObservationType.Obstacle);
                    AddScaledSpeedObservation(localVelocity.z, ObservationType.Obstacle);
                    if (AddObstacleOrientationObservations)
                    {
                        AddVectorObs(Utility.TopdownSignedAngle(transform.forward, car.forward) / 180, ObservationType.Obstacle);
                    }

                    AddDebugRay(rayId++, CarDetectionAngles[i], dist, Color.red, Settings.DebugRaysCarDistance && OutputCarGizmos, DetectionVectorsSubstractions[i]);
                }
            }
        }

        private float GetCarDistance(float angle, float startPositionOffset, out Transform trans)
        {
            const int layerMask = GenericAgent.RayCastLayerMask;

            Vector3 rayDirection = Quaternion.AngleAxis(angle, Vector3.up) * transform.forward;
            rayDirection = new Vector3(rayDirection.x, 0, rayDirection.z).normalized;
            Vector3 start = transform.position;
            if (Settings.UseSkyCarDetection) start.y = GenericAgent.SkyColliderY;
            start += rayDirection * startPositionOffset + Vector3.up * 0.3f;

            RaycastHit info;
            bool hit;
            float dist;
            trans = null;

            hit = Physics.Raycast(start, rayDirection, out info, Settings.ViewDistance, layerMask);
            if (hit)
            {
                dist = info.distance;
                trans = info.transform;
            }
            else dist = Settings.ViewDistance;

            return dist;
        }

        protected void CollectSpeedObservation(float maxSpeed = 15)
        {
            AddVectorObs(transform.InverseTransformVector(_rb.velocity) / maxSpeed, ObservationType.Other);
        }

        protected void AddScaledSpeedObservation(float speed, ObservationType type, float maxSpeed = 15)
        {
            AddVectorObs(speed / maxSpeed, type);
        }

        protected float GetSpeed()
        {
            return transform.InverseTransformVector(_rb.velocity).z;
        }

        protected void CollectPositionObservation(float areaSize)
        {
            AddVectorObs(transform.localPosition, ObservationType.Other);
        }

        /// <summary>
        /// Adds 2 floats to vector of observations.
        /// 1) Distance to target;
        /// 2) Signed angle of transform.forward and direction to target.
        /// </summary>
        /// <param name="target"></param>
        protected void CollectRelativeTargetPositionObservation(Vector3 target, float maxDistance)
        {
            float distance = Vector3.Distance(transform.localPosition, target);
            if (distance > maxDistance) distance = maxDistance;

            AddVectorObs(distance / maxDistance, ObservationType.Path);
            AddVectorObs(Utility.TopdownSignedAngle(transform.forward, target - transform.localPosition) / 180, ObservationType.Path);
        }

        private void LoadParamsFromYaml()
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict.FillFromYaml(Settings.AgentSettingsFile);
            AddCrossroadObservations = bool.Parse(dict.GetValueOrDefault("UseCrossroadObservations", "True"));
            AddTimeObservations = bool.Parse(dict.GetValueOrDefault("UseTimeObservations", "True"));
            AddCrashInfoObservations = bool.Parse(dict.GetValueOrDefault("UseCrashInfoObservations", "True"));
            AddObstacleOrientationObservations = bool.Parse(dict.GetValueOrDefault("AddObstacleOrientationObservation", "False"));

            Angles = dict.GetValueOrDefault("PathDetectionAngles", "")
                .Trim()
                .Split(new char[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .ToFloatArray();

            CarDetectionAngles = dict.GetValueOrDefault("CarDetectionAngles", "")
                .Trim()
                .Split(new char[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .ToFloatArray();

            NextPointDistances = dict.GetValueOrDefault("NextPointDistances", "1 2 5")
                .Trim()
                .Split(new char[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .ToFloatArray();
        }


        private void CalculateSubstractions()
        {
            DetectionVectorsSubstractions = new float[CarDetectionAngles.Length];

            Vector3 center = Vector3.zero;
            center.y += 0.3f;
            center -= Vector3.forward * 0.07f;

            Vector3 f = Vector3.forward * 1.45f;
            Vector3 r = Vector3.right * 0.635f;

            Vector3[] reverseCorners = new Vector3[4];
            reverseCorners[3] = center + f - r;
            reverseCorners[2] = center + f + r;
            reverseCorners[1] = center - f + r;
            reverseCorners[0] = center - f - r;

            for (int i = 0; i < CarDetectionAngles.Length; i++)
            {
                Vector3 rayDirection = Quaternion.AngleAxis(CarDetectionAngles[i], Vector3.up) * Vector3.forward;
                rayDirection = new Vector3(rayDirection.x, 0, rayDirection.z).normalized;
                //Vector3 start = transform.position + Vector3.up * 0.3f;

                float dist = Utility.GetNearestDist(reverseCorners, Vector3.zero, rayDirection).Item1;

                DetectionVectorsSubstractions[i] = dist;
            }
        }

        protected void AddVectorObs(float f, ObservationType type)
        {
            Agent.AddVectorObs(f, type);
        }

        protected void AddVectorObs(Vector3 v, ObservationType type)
        {
            Agent.AddVectorObs(v, type);
        }

        protected float NormalizeValue(float val, float max)
        {
            return Mathf.Clamp(val / max, -1, 1);
        }

        protected float InverseNormalizeValue(float val, float max)
        {
            return Mathf.Clamp((max - val) / max, -1, 1);
        }


        public void AddDebugRay(int id, float angle, float distance, Color color, bool show, float startPositionOffset = 0)
        {
            if (!show)
            {
                DebugRays[id].Hide();
                return;
            }

            Vector3 rayDirection = Quaternion.AngleAxis(angle, Vector3.up) * transform.forward;
            rayDirection = new Vector3(rayDirection.x, 0, rayDirection.z).normalized;

            DebugRays[id].Set(transform.position + rayDirection * startPositionOffset, rayDirection * distance, color, true);
        }

        public void AddDebugRay(int id, Vector3 target, Color color, bool show)
        {
            if (!show)
            {
                DebugRays[id].Hide();
                return;
            }

            DebugRays[id].Set(transform.position, target - transform.position, color, true);
        }

        public void DrawDebugRays()
        {
            for (int i = 0; i < DebugRays.Length; i++)
            {
                DebugRays[i].Draw();
            }
        }


        public virtual void AddCrash()
        {
            if (Agent.IsDone()) return;

            AddCrash(1);
        }

        public virtual void AddPersistingContact()
        {
            if (Agent.IsDone()) return;
            if (TimeSinceLastCrash == 0) return;

            AddCrash(Settings.PersistingCrashRatio);
        }

        /// <summary>
        /// Adds crash info of given magnitude.
        /// </summary>
        /// <param name="magnitude"></param>
        private void AddCrash(float magnitude)
        {
            NumberOfCrashes += magnitude;
            Agent.AddReward(magnitude * Settings.DBFailureReward / Settings.MaxNumberOfCrashes, "crash");

            TimeSinceLastCrash = 0;

            if (PathChecker.Status == NavMesh.CarStatus.OnCrossroad)
            {
                CrossroadDamage += magnitude;
            }
            else if (PathChecker.CurrentParkingSpace != null)
            {
                ParkingSpaceDamage += magnitude;
            }
            else
            {
                RoadDamage += magnitude;
            }

            if (NumberOfCrashes >= Settings.MaxNumberOfCrashes)
            {
                Done(0, EndingType.Crash, false);
            }
        }

        public abstract string GetBehaviourName();
        public abstract int GetObservationsCount();
        public abstract NNModel GetBehaiourModel();
        public abstract int GetTime();

        public bool IsCrashing()
        {
            return TimeSinceLastCrash < 2;
        }

        public virtual void Done(float reward, EndingType endingType, bool success = false)
        {
            Agent.AddReward(reward, "end");
            Agent.Done(Settings.DebugEpisodeSummary, endingType, "", success);
        }



    }

    public enum EndingType
    {
        Finished,
        Crash, 
        Stuck, 
        TooFarFromLine,
        OutOfPath,
        GlobalTimeout, 
        ProgressTimeout, 
        CrossroadTimeout
    }
}
