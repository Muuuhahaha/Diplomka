using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Barracuda;
//using UnityEditor;

namespace Assets.Global.Scripts.Agents.AgentBehaviours
{
    /// <summary>
    /// This class controls GenericAgent's behaviour during parking.
    /// </summary>
    [Serializable]
    class ParkingBehaviour : GenericAgentBehaviour
    {
        [HideInInspector] public NNModel BehaviourModel { get { return Settings.PBModel; } }

        [ReadOnly] public int Time;
        [ReadOnly] public int TimeWithoutMovement;
        private float TimeWithoutMovementTreshold { get { return Settings.PBTimeWithoutMovementTreshold; } }
        [ReadOnly] public float CurrentReward;
        private float BestReward;
        public float LastReward;
        private GameObject helperGO;

        [ReadOnly] public bool IsInCone;
        [ReadOnly] private Vector3 coor;
        [ReadOnly] private Vector3 dirrr;
        [ReadOnly] private float d1;
        [ReadOnly] private float d2;
        [ReadOnly] private float ang;
        [ReadOnly] private int TimeOutsidePath;


        private const bool RemoveRewardAfterCrash = true;

        public override void Setup(GenericAgent agent)
        {
            base.Setup(agent);
            helperGO = new GameObject();
            AddObstacleOrientationObservations = false;
        }

        public override void CollectObservations()
        {
            ResetObservations();
            CollectSpeedObservation();
            AddVectorObs(Agent.throttle, ObservationType.Other);
            AddVectorObs(Agent.steering, ObservationType.Other);
            AddVectorObs(Agent.breakRatio, ObservationType.Other);

            //Parking space observations
            CollectRelativeTargetPositionObservation(Path.ParkingPosition, Settings.ViewDistance);
            AddDebugRay(rayId++, Path.ParkingPosition, Color.yellow, Settings.DebugRaysNextPoint && Agent.OutputCarGizmos);

            float angle = Utility.TopdownSignedAngle(transform.forward, Path.ParkingDirection);
            if (angle > 90) angle -= 180;
            if (angle < -90) angle += 180;
            AddVectorObs(angle / 90, ObservationType.Path);
            ang = angle / 90;

            //time observations
            if (AddTimeObservations)
            {
                AddVectorObs((float)Time / Settings.PBMaxTime, ObservationType.Other);
                AddVectorObs(Math.Min(1, (float)TimeWithoutMovement / TimeWithoutMovementTreshold), ObservationType.Other);
            }

            if (AddCrashInfoObservations)
            {
                AddVectorObs((float)NumberOfCrashes / Settings.MaxNumberOfCrashes, ObservationType.Other);
                AddVectorObs(IsCrashing() ? 1 : 0, ObservationType.Other);
            }

            CollectRoadEdgeObservations();
            CollectCarObservations();
        }

        public override void FixedUpdate()
        {
            if (Agent.IsDone()) return;

            base.FixedUpdate();

            Time++;
            Agent.AddReward(Settings.PBTimeRewardPerFrame, "Time");

            if (Agent.IsOutsidePath())
            {
                TimeOutsidePath++;
                //SceneView.lastActiveSceneView.pivot = Agent.GetPosition();
                //SceneView.lastActiveSceneView.Repaint();
                //Debug.Break();
                if (!Settings.EasyFailureCheck || TimeOutsidePath > Settings.MaxTimeOutsideMap)
                {
                    End(EndingType.OutOfPath, -1);
                }
            }
            else
            {
                TimeOutsidePath = 0;
            }

            //getting current reward
            if (Settings.PBUseConeReward) CurrentReward = GetCurrentRewardCone();
            else CurrentReward = GetCurrentRewardSimple();

            //adding current reward
            if (Settings.PBBaseRewardOnBestPositionOnly)
            {
                if (CurrentReward > BestReward)
                {
                    Agent.AddReward(CurrentReward - BestReward, "position");
                    BestReward = CurrentReward;
                }
            }
            else
            {
                Agent.AddReward(CurrentReward - LastReward, "position");
                LastReward = CurrentReward;
            }

            PathChecker.UpdatePosition();

            if (Mathf.Abs(Agent.ForwardSpeed) < 0.02f) TimeWithoutMovement++;
            else TimeWithoutMovement = 0;

            if (CurrentReward >= 0.4 && TimeWithoutMovement >= Settings.PBTimeWithoutMovementTreshold) { 
            
                //Agent.AddReward(CurrentReward, "position");
                //Agent.AddReward(CurrentReward - BestReward, "endPosition");
                //Agent.Done(Settings.DebugEpochEndReason, "Finished", false);
                End(EndingType.Finished, 0, true);
                return;
            }

            if (Time >= Settings.PBMaxTime)
            {
                End(EndingType.GlobalTimeout, -1f);
                //Agent.AddReward(-1f, "end");
                //Agent.AddReward(CurrentReward, "position");
                //Agent.AddReward(CurrentReward - BestReward, "endPosition");
                //Agent.Done(Settings.DebugEpochEndReason, "Time", false);
                return;
            }

            if (transform.localPosition.y < 0.25f)
            {
                End(EndingType.Stuck, Settings.DBFailureReward, false, RemoveRewardAfterCrash);
                //Agent.AddReward(Settings.TooFarReward, "end");
                //Agent.AddReward(CurrentReward - BestReward, "endPosition");
                //Agent.Done(Settings.DebugEpochEndReason, "stuck", false);
                return;
            }
        }

        private void End(EndingType endingType, float endReward, bool success = false, bool removeProgressReward = false)
        {
            Agent.AddReward(endReward, "end");

            if (!removeProgressReward)
            {
                CurrentReward = GetCurrentRewardSimple();

                if (Settings.PBBaseRewardOnBestPositionOnly) Agent.AddReward(CurrentReward - BestReward, "endPosition");
                else Agent.AddReward(CurrentReward - LastReward, "position");
            }
            else
            {
                Agent.AddReward(-CurrentReward, "position");
            }

            Agent.Done(Settings.DebugEpisodeSummary, endingType, "", success);
        }

        private float GetCurrentRewardSimple()
        {
            float dist = Vector3.Distance(transform.position, Path.ParkingPosition);
            float angle = Vector3.Angle(transform.forward, Path.ParkingDirection);
            if (angle > 90) angle = 180 - angle;

            float maxDist = Settings.PBDistanceTreshold;
            float maxAngle = Settings.PBAngleTreshold;
            const float minAngleCoef = 0.1f;

            if (dist > maxDist) dist = maxDist;
            if (angle > maxAngle) angle = maxAngle;

            float distCoef = (maxDist - dist) / maxDist;
            float angleCoef = (maxAngle - angle) / maxAngle;
            if (angleCoef < minAngleCoef) angleCoef = minAngleCoef;

            return Mathf.Pow(distCoef, Settings.PBDistanceRewardPower) * angleCoef;
        }

        private float GetCurrentRewardCone()
        {
            IsInCone = AgentIsInCone();
            if (!IsInCone) return 0;

            float dist = Vector3.Distance(transform.position, Path.ParkingPosition);
            float maxDist = Settings.PBDistanceTreshold;
            if (dist > maxDist) dist = maxDist;

            return (maxDist - dist) / maxDist;
        }

        public override void GeneratePath()
        {
            throw new NotImplementedException();
        }

        public override void ResetBehaviour()
        {
            base.ResetBehaviour();

            helperGO.transform.position = Path.ParkingPosition;
            if (Path.ParkingDirection.magnitude > 0)
            {
                helperGO.transform.forward = Path.ParkingDirection;
            }

            LastReward = 0;
            BestReward = 0;

            Time = 0;
            TimeOutsidePath = 0;
        }

        private bool AgentIsInCone()
        {
            /*const int num = 30;
            const float step = 0.2f;

            for (int x = 0; x < num; x++)
            {
                for (int y = 0; y < num; y++)
                {
                    PosIsInCone(transform.position + new Vector3(step * (x - num/2), 0, step * (y - num / 2)),
                        transform.forward);
                }
            }*/

            return PosIsInCone(transform.position, transform.forward);
        }

        private bool PosIsInCone(Vector3 position, Vector3 dir)
        {
            const float circleRadius = 3.8f;
            const float errorMargin = 0.15f;

            Vector3 coordinates = helperGO.transform.InverseTransformPoint(position);
            Vector3 direction = helperGO.transform.InverseTransformVector(dir);
            coordinates.y = 0;
            direction.y = 0;
            //note: forward = (0,0,1)

            //        Debug.Log(coordinates + " " + angleDifference + " " + angle);

            coor = coordinates;
            dirrr = direction;
            if (coordinates.x < 0)
            {
                direction.x *= -1;
                coordinates.x *= -1;
            }
            if (coordinates.z > 0)
            {
                direction.x *= -1;
                coordinates.z *= -1;
            }

            if (direction.z > 0)
            {
                direction.z *= -1;
            }

            Vector3 left = new Vector3(-direction.z, 0, direction.x);

            //right side of target
            Vector3 targetCircleCenter = new Vector3(circleRadius + errorMargin, 0, 0);
            Vector3 agentCircleCenter = coordinates - left.normalized * circleRadius;
            float distance = Vector3.Distance(targetCircleCenter, agentCircleCenter);
            bool isInCone = distance > 2 * circleRadius;
            //Debug.DrawRay(helperGO.transform.TransformPoint(targetCircleCenter), Vector3.up);
            //Debug.DrawRay(helperGO.transform.TransformPoint(agentCircleCenter), Vector3.up);
            d1 = distance;

            //left side of target
            if (isInCone)
            {
                targetCircleCenter = -targetCircleCenter;
                agentCircleCenter = coordinates + left.normalized * circleRadius;
                distance = Vector3.Distance(targetCircleCenter, agentCircleCenter);
                isInCone = distance > 2 * circleRadius;
                //Debug.DrawRay(helperGO.transform.TransformPoint(targetCircleCenter) + Vector3.up, Vector3.up, Color.cyan);
                //Debug.DrawRay(helperGO.transform.TransformPoint(agentCircleCenter) + Vector3.up, Vector3.up, Color.cyan);
                d2 = distance;
            }



            /*if (t++ % 10 == 0)
            {
                Color c = Color.red;
                if (isInCone) c = Color.green;

                Debug.DrawRay(position + Vector3.up * 2, transform.forward, c, 0.5f);
            }*/

            return isInCone;
        }
        //int t = 0;

        public override string GetBehaviourName()
        {
            return "ParkingBrain";
        }

        public override int GetObservationsCount()
        {
            int count = 9;
            if (AddTimeObservations) count += 2;
            if (AddCrashInfoObservations) count+=2;
            count += Angles.Length;
            count += CarDetectionAngles.Length * 3;
            if (AddObstacleOrientationObservations) count += CarDetectionAngles.Length;

            return count;
        }

        public override void AddCrash()
        {
            if (Agent.IsDone()) return;
            if (TimeSinceLastCrash == 0) return;

            base.AddCrash();

            if (NumberOfCrashes >= Settings.MaxNumberOfCrashes && RemoveRewardAfterCrash)
            {
                Agent.AddReward(-CurrentReward, "position");
            }
        }

        public override void AddPersistingContact()
        {
            if (Agent.IsDone()) return;
            if (TimeSinceLastCrash == 0) return;

            base.AddPersistingContact();

            if (NumberOfCrashes >= Settings.MaxNumberOfCrashes && RemoveRewardAfterCrash)
            {
                Agent.AddReward(-CurrentReward, "position");
            }
        }

        public override NNModel GetBehaiourModel()
        {
            return BehaviourModel;
        }

        public override int GetTime()
        {
            return Time;
        }
    }
}
