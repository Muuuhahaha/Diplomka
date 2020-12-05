using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Barracuda;
using Assets.Global.Scripts.Map;

namespace Assets.Global.Scripts.Agents.AgentBehaviours
{
    /// <summary>
    /// This class controls GenericAgent's behaviour when following Path.
    /// </summary>
    [Serializable]
    class DrivingBehaviour : GenericAgentBehaviour
    {
        [HideInInspector] public NNModel BehaviourModel { get { return Settings.DBModel; } }

        [ReadOnly] public float TotalPathLength;
        [ReadOnly] public float BestProgress;
        [ReadOnly] public float CurrentProgress;
        [ReadOnly] public float DistFromLine;
        [ReadOnly] public bool IsAligned;
        [ReadOnly] public bool IsBehindCar;
        [ReadOnly] public float ForwardSpeed;
        [ReadOnly] private bool DidGetToPath;
        [ReadOnly] private int TimeOutsidePath;

        //time reset parameters
        private int TimeSinceLastCheckpoint;
        private int TimeSinceStart;
        private int TimeOnCrossroad;
        private float LastProgressCheckpoint;

        //progress reward factor statistics
        private int FactorCount;
        private float TotalDistFactor;
        private float TotalSteeringFactor;
        private float TotalObstacleFactor;
        private float TotalAlignFactor;
        private float TotalTotalFactor;

        private float DistanceTraveledAligned;

        private bool UseCrossroadTimeout = false;

        /// <summary>
        /// Collects current observations
        /// </summary>
        public override void CollectObservations()
        {
            ResetObservations();
            CollectSpeedObservation();
            Agent.AddVectorObs(Agent.throttle, ObservationType.Other);
            Agent.AddVectorObs(Agent.steering, ObservationType.Other);
            Agent.AddVectorObs(Agent.breakRatio, ObservationType.Other);
            AddVectorObs(CurrentProgress / TotalPathLength, ObservationType.Other);

            //next points
            Vector3[] points = PathChecker.GetPointsInDistances(NextPointDistances);
            foreach (var point in points)
            {
                CollectRelativeTargetPositionObservation(point, Settings.ViewDistance);

                AddDebugRay(rayId++, point, Color.yellow, Settings.DebugRaysNextPoint && Agent.OutputCarGizmos);
            }

            //crossroad observations
            if (AddCrossroadObservations)
            {
                if (PathChecker.Status == NavMesh.CarStatus.OnCrossroad)
                {
                    AddVectorObs(1, ObservationType.Other);
                    AddVectorObs(1, ObservationType.Other);
                }
                else
                {
                    AddVectorObs(0, ObservationType.Other);
                    float d = PathChecker.GetDistanceTillCrossroad();
                    if (d > Settings.ViewDistance) d = Settings.ViewDistance;
                    AddVectorObs(InverseNormalizeValue(d, Settings.ViewDistance), ObservationType.Other);
                }
            }

            //time observation
            if (AddTimeObservations)
            {
                if (Settings.DBMaxExperimentTime > 0) AddVectorObs((float)TimeSinceStart / Settings.DBMaxExperimentTime, ObservationType.Other);
                else AddVectorObs(0, ObservationType.Other);

                if (Settings.DBMaxTimeWithoutProgress > 0) AddVectorObs(Mathf.Min(1, (float)TimeSinceLastCheckpoint / Settings.DBMaxTimeWithoutProgress), ObservationType.Other);
                else AddVectorObs(0, ObservationType.Other);

                if (UseCrossroadTimeout)
                {
                    if (Settings.DBMaxTimeOnCrossroad > 0 && Agent.IsOnCrossroad()) AddVectorObs((float)TimeOnCrossroad / Settings.DBMaxTimeOnCrossroad, ObservationType.Other);
                    else AddVectorObs(0, ObservationType.Other);
                }
            }

            if (AddCrashInfoObservations)
            {
                AddVectorObs((float)NumberOfCrashes / Settings.MaxNumberOfCrashes, ObservationType.Other);
                AddVectorObs(IsCrashing() ? 1 : 0, ObservationType.Other);
            }

            CollectRoadEdgeObservations();
            CollectCarObservations();
        }

        /// <summary>
        /// Checks arent's performance and adds rewards and/or ends episode if certain conditions are satisfied.
        /// </summary>
        public override void FixedUpdate()
        {
            if (Agent.AgentDone) return;

            base.FixedUpdate();

            ForwardSpeed = Agent.ForwardSpeed;
            IsBehindCar = Agent.IsBehindCar(out ClosestObstacleDistance);

            if (!IsBehindCar && ForwardSpeed < 1) TimeSinceLastCheckpoint++;
            TimeSinceStart++;

            Agent.AddReward(Settings.DBBaseTimeRewardPerFrame / PathChecker.TotalLength, "Time");
            Agent.AddReward(Settings.DBSteeringRewardRatio * Math.Abs(Agent.steering), "steering");

            //Global timeout check
            if (TimeSinceStart > Settings.DBMaxExperimentTime)
            {
                Done(-1f, EndingType.GlobalTimeout);
                return;
            }

            if (PathChecker.Finished(Settings.DBDistFromGoal))
            {
                Done(Settings.DBFinishedReward, EndingType.Finished, true);
                return;
            }

            PathChecker.UpdatePosition();


            //Time on crossroad check   
            if (UseCrossroadTimeout)
            {
                if (Agent.IsOnCrossroad())
                {
                    TimeOnCrossroad++;
                    if (TimeOnCrossroad > Settings.DBMaxTimeOnCrossroad)
                    {
                        Done(-1f, EndingType.CrossroadTimeout);
                    }

                }
                else
                {
                    TimeOnCrossroad = 0;
                }
            }

            //distance from line center check
            DistFromLine = PathChecker.GetDistFromCurLine();
            if (DistFromLine < 0.1f) DidGetToPath = true;

            //Check if agent is too far from line.
            if (DidGetToPath
                  && PathChecker.Status == NavMesh.CarStatus.OnRoad
                  && DistFromLine > PathChecker.CurrentRoad.LineWidth - Settings.DBMinDistanceFromLineEdge
                  && !Settings.EasyFailureCheck)
            {
                Done(Settings.DBFailureReward, EndingType.TooFarFromLine);
                return;
            }
            else if (DistFromLine > Settings.DistFromCenterTreshold)
            {
                Agent.AddReward((DistFromLine - Settings.DistFromCenterTreshold) * Settings.DistFromCenterRewardMult, "distance from line");
            }

            //agent is outside map
            if (transform.localPosition.y < 0.2)
            {
                Done(Settings.DBFailureReward, EndingType.Stuck);
                return;
            }

            //agent is outside path
            if (Agent.IsOutsidePath())
            {
                TimeOutsidePath++;

                if (!Settings.EasyFailureCheck || TimeOutsidePath > Settings.MaxTimeOutsideMap)
                {
                    Done(Settings.DBFailureReward, EndingType.TooFarFromLine);
                    return;
                }
            }
            else
            {
                TimeOutsidePath = 0;
            }

            //adding reward for progress
            IsAligned = AgentIsAligned();
            CurrentProgress = PathChecker.TotalProgress;
            if (CurrentProgress > BestProgress)
            {
                float distFactor = 1 - DistFromLine / Settings.DBMaxDistanceFromLineCenter;// currently not used
                float obstacleFactor = Mathf.Clamp(ClosestObstacleDistance / 4, 0, 1);
                float steeringFactor = 1 - Math.Abs(Agent.steering) / 2; //currently not used
                float alignFactor = IsAligned ? 2 : 1;
                //float totalFactor = distFactor * obstacleFactor * steeringFactor * alignFactor;
                float totalFactor = obstacleFactor * alignFactor;

                TotalDistFactor += distFactor;
                TotalObstacleFactor += obstacleFactor;
                TotalSteeringFactor += steeringFactor;
                TotalAlignFactor += alignFactor;
                TotalTotalFactor += totalFactor;
                FactorCount++;
                if (IsAligned) DistanceTraveledAligned += (CurrentProgress - BestProgress);

                Agent.AddReward(totalFactor * (CurrentProgress - BestProgress) * Settings.DistToRewardMult, "progress");
                Agent.AddReward(totalFactor * (CurrentProgress - BestProgress) * Settings.DBTotalDistanceReward / PathChecker.TotalLength, "progress");
                BestProgress = CurrentProgress;



                if (BestProgress - Settings.DBMinProgressIncrement >= LastProgressCheckpoint)
                {
                    LastProgressCheckpoint = BestProgress;
                    TimeSinceLastCheckpoint = 0;
                }
            }


            //time reset because of lack of progress
            if (TimeSinceLastCheckpoint >= Settings.DBMaxTimeWithoutProgress)
            {
                if (!Settings.EasyFailureCheck
                    || TimeSinceLastCheckpoint >= Settings.DBMaxTimeWithoutProgress * 2)
                {
                    Done(-1f, EndingType.ProgressTimeout);
                    return;
                }
            }

            TotalPathLength = PathChecker.TotalLength;
        }

        public override void GeneratePath()
        {
            throw new NotImplementedException();
        }

        public override void ResetBehaviour()
        {
            base.ResetBehaviour();

            TimeSinceLastCheckpoint = 0;
            LastProgressCheckpoint = 0;
            CurrentProgress = 0;
            TimeSinceStart = 0;

            BestProgress = 0;

            TotalAlignFactor = 0;
            TotalDistFactor = 0;
            TotalObstacleFactor = 0;
            TotalSteeringFactor = 0;
            TotalTotalFactor = 0;
            FactorCount = 0;
            DistanceTraveledAligned = 0;

            DidGetToPath = false;
            TimeOutsidePath = 0;
        }

        public override string GetBehaviourName()
        {
            return "DrivingBrain";
        }

        public override int GetObservationsCount()
        {
            int count = 6;
            count += NextPointDistances.Length * 2;
            if (AddCrossroadObservations) count += 2;
            if (AddTimeObservations)
            {
                count += 2;
                if (UseCrossroadTimeout) count++;
            }
            if (AddCrashInfoObservations) count += 2;
            count += Angles.Length;
            count += CarDetectionAngles.Length * 3;
            if (AddObstacleOrientationObservations) count += CarDetectionAngles.Length + 1;

            return count;
        }

        public override NNModel GetBehaiourModel()
        {
            return BehaviourModel;
        }

        public override int GetTime()
        {
            return TimeSinceStart;
        }

        private static float[] distances = new float[] { 3, 5 };
        private float AlignAngle1;
        private float AlignAngle2;
        /// <summary>
        /// returns true if agent is in middle of line and correctly aligned
        /// </summary>
        /// <returns></returns>
        private bool AgentIsAligned()
        {
            var points = PathChecker.GetPointsInDistances(distances);

            if (Settings.DebugRaysOther)
            {
                Debug.DrawRay(points[0], Vector3.up);
                Debug.DrawRay(points[1], Vector3.up, Color.cyan);
            }

            AlignAngle1 = Mathf.Abs(Utility.TopdownSignedAngle(transform.forward, points[0] - transform.localPosition));
            AlignAngle2 = Mathf.Abs(Utility.TopdownSignedAngle(transform.forward, points[1] - transform.localPosition));

            if (AlignAngle1 > Settings.DBMaxAlignRewardAngle) return false;
            if (AlignAngle2 > Settings.DBMaxAlignRewardAngle) return false;

            return true;
        }

        public override void Done(float reward, EndingType endingType, bool success = false)
        {
            Agent.AddReward(reward, "end");

            string additionalMessage = GetFactorStats();
            Agent.Done(Settings.DebugEpisodeSummary, endingType, additionalMessage, success);
        }

        public float GetDistancePercentageTraveledAligned()
        {
            if (BestProgress == 0) return 0;

            return DistanceTraveledAligned / BestProgress;
        }

        /// <summary>
        /// Returns printable string of average factor magnitude
        /// </summary>
        /// <returns></returns>
        private string GetFactorStats()
        {
            return "Factor stats:   "
                    + "  Obstacle: " + (TotalObstacleFactor / FactorCount)
                    + "  Align: " + (TotalAlignFactor / FactorCount)
                    + "  Total: " + (TotalTotalFactor / FactorCount);
        }
    }
}
