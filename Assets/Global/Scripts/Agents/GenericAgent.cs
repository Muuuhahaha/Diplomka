using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using MLAgents;
using VehicleBehaviour;
using Assets.Global.Scripts.Map;
using Assets.Global.Scripts.Agents.AgentBehaviours;



namespace Assets.Global.Scripts.Agents
{
    /// <summary>
    /// This class represents a single agent. Agent's actions are defined by currently used Behaviour class.
    /// </summary>
    class GenericAgent : MonoBehaviour
    {
        [Header("Agent params")]
        [Unchangable] public StatsWrapper Statistics;
        [Unchangable] public RealMap Map;
        [Unchangable] public GeneralSettings Settings;
        [HideInInspector] public GameObject CrashCollider;

        private GameObject SkyCollider; //collider used for car detection in hilly environments; currently not used
        public const float SkyColliderY = 10;


        //car gameobjects
        [HideInInspector] public Rigidbody _rb;
        protected WheelVehicle carScript;
        protected WheelCollider[] wheels;
        [HideInInspector] public GameObject helperGO; //helper object we use to easily switch between coordinate systems
        [HideInInspector] public GameObject carObject;
        protected RLAgent RLAgentScript;

        public PathChecker PathChecker;
        public PathGenerator PathGenerator;
        public Path GeneratedPath { get { return PathChecker.GeneratedPath; } }
        private int PathSeed;
        private bool UsePathSeed = false;

        //debug info
        [Header("Debug")]
        public bool BreakPoint = false;
        [ReadOnly] public float TotalReward;
        [ReadOnly] public float RewardMult;
        [ReadOnly] public bool AgentDone;
        [ReadOnly] private bool ExperimentEnded;
        [ReadOnly] public bool AgentSuccessful;

        [ReadOnly] public List<float> Observations;
        [ReadOnly] public List<float> RoadObservations;
        [ReadOnly] public List<float> ObstacleObservations;
        [ReadOnly] public List<float> PathObservations;
        [ReadOnly] public List<float> OtherObservations;


        [ReadOnly] public float throttle;
        [ReadOnly] public float steering;
        [ReadOnly] private bool breaking;
        [ReadOnly] public float breakRatio;


        public Action DoneAction; // action to be called when agent is finished
        public Action<float> AddRewardAction; //action to be called when adding reward
        private SortedDictionary<string, float> Rewards; // sums of rewards of given types

        [ReadOnly] public BehaviourState CurrentBehaviourType = BehaviourState.None;
        public GenericAgentBehaviour CurrentBehaviour;
        public DrivingBehaviour DrivingBehaviour;
        public ParkingBehaviour ParkingBehaviour;

        //discrete action params
        private const int branchSize = 5;
        private const int branchWidth = (branchSize - 1) / 2;
        private const float difConst = 0.05f;

        public const int RayCastLayerMask = 5 << 15;

        public virtual void Awake()
        {
            RLAgentScript = GetComponent<RLAgent>();

            Rewards = new SortedDictionary<string, float>();
            helperGO = new GameObject();

            ParkingBehaviour.Setup(this);
            DrivingBehaviour.Setup(this);
            CurrentBehaviourType = BehaviourState.None;

            PathChecker = new PathChecker();
            PathChecker.Agent = this;
            PathChecker.BehaviourChanged += ChangeBehaviour;
            Observations = new List<float>();
            UpdateCarScriptsReferences();

            AgentDone = true;
            ExperimentEnded = true;

            if (!Settings.PathType.SwitchesCarObjects() && carObject == null)
            {
                Debug.Log("assigned agent objects");
                carObject = Instantiate(Settings.CarPrefab, transform);
                UpdateCarScriptsReferences();
            }

            if (Settings.UseSkyCarDetection)
            {
                GenerateSkyCollider();
            }
        }

        /// <summary>
        /// updates script reference after assigning new carObject
        /// </summary>
        public void UpdateCarScriptsReferences()
        {
            if (carObject == null) return;

            _rb = carObject.GetComponent<Rigidbody>();
            carScript = carObject.GetComponent<WheelVehicle>();
            wheels = carObject.GetComponentsInChildren<WheelCollider>();
            PathChecker.Car = carObject;
        }

        /// <summary>
        /// returns current carObject to IdleCarManager
        /// </summary>
        public void ReturnCarToManager()
        {
            if (carObject != null)
            {
                IdleCarManager manager = GameObject.FindObjectOfType<IdleCarManager>();
                if (PathChecker.GeneratedPath == null) throw new Exception();
                manager.AddIdleCar(carObject, PathChecker.GeneratedPath.ParkingSpace, PathChecker.GeneratedPath.ParkingSpaceId);
            }
        }

        /// <summary>
        /// gets given car object from IdleCarManager
        /// </summary>
        public void GetCarFromManager(GameObject newCar)
        {
            if (newCar == null)
            {
                if (carObject != null)
                {
                    IdleCarManager manager = GameObject.FindObjectOfType<IdleCarManager>();
                    newCar = manager.GetCar(carObject).Car;
                }
                else
                {
                    return;
                }
            }
            
            carObject = newCar;
            carObject.transform.parent = transform;

            UpdateCarScriptsReferences();
        }

        private void GenerateSkyCollider()
        {
            SkyCollider = Instantiate(CrashCollider, transform);
            PlaceSkyCollider();
        }

        /// <summary>
        /// updates position of sky collider
        /// currently not used.
        /// </summary>
        private void PlaceSkyCollider()
        {
            if (!Settings.UseSkyCarDetection) return;

            Vector3 pos = carObject.transform.localPosition;
            Vector3 forward = carObject.transform.forward;

            pos.y = SkyColliderY;
            forward.y = 0;

            SkyCollider.transform.position = pos;
            SkyCollider.transform.forward = forward;
        }

        public void FixedUpdate()
        {
            PlaceSkyCollider();
            if (IsDone()) return;

            CurrentBehaviour.FixedUpdate();
        }

        public void CollectObservations()
        {
            CurrentBehaviour.CollectObservations();
        }

        /// <summary>
        /// preformes action according to policy decision
        /// </summary>
        /// <param name="vectorAction"></param>
        public void AgentAction(float[] vectorAction)
        {
            if (!Settings.ContinuousBrain)
            {
                throttle = (vectorAction[0] - branchWidth) / (float)branchWidth;
                steering = (vectorAction[1] - branchWidth) / (float)branchWidth;
                breaking = vectorAction[2] == 1;

                if (!breaking) carScript.Throttle = throttle;
                else
                {
                    if (ForwardSpeed > 0) carScript.Throttle = -1;
                    else if (ForwardSpeed < 0) carScript.Throttle = 1;
                    else carScript.Throttle = 0;
                }

                carScript.Steering = steering * Settings.SteerAngle;
            }
            else
            {
                float lastSteering = steering;

                throttle = Mathf.Clamp(vectorAction[0], -1, 1);
                steering = Utility.SignedPow(Mathf.Clamp(vectorAction[1], -1, 1), Settings.TurnSignalExponent);
                breakRatio = Mathf.Clamp(vectorAction[2], 0, 1);

                carScript.Steering = steering * Settings.SteerAngle;
                carScript.Handbrake = breakRatio >= 0.8f;

                if (ForwardSpeed > 0) carScript.Throttle = (throttle - breakRatio) * 1;
                else if (ForwardSpeed < 0) carScript.Throttle = (throttle + breakRatio) * 1;
                else if (throttle > breakRatio) carScript.Throttle = (throttle - breakRatio) * 1;
                else
                {
                    carScript.Throttle = 0;
                    _rb.velocity = Vector3.zero;
                }

                if (CurrentBehaviour.GetType() == DrivingBehaviour.GetType())
                {
                    AddReward(Mathf.Abs(steering - lastSteering) * Settings.DBBaseSteeringChangeRewardMult / PathChecker.TotalLength, "steering change");
                }
                else if (CurrentBehaviour.GetType() == ParkingBehaviour.GetType())
                {
                    AddReward(Mathf.Abs(steering - lastSteering) * Settings.PBSteeringChangeRewardMult, "steering change");
                }
            }
        }

        /// <summary>
        /// resets agent
        /// </summary>
        public void AgentReset()
        {
            if (!ExperimentEnded)
            {
                EndEpisode(false);
            }
            ExperimentEnded = false;

            if (!Settings.PathType.SwitchesCarObjects() && carObject == null)
            {
                carObject = Instantiate(Settings.CarPrefab, transform);
                UpdateCarScriptsReferences();
            }

            if (GeneratedPath != null && !GeneratedPath.Type.SwitchesCarObjects())
            {
                foreach (WheelCollider wheel in wheels)
                {
                    wheel.steerAngle = 0;
                    wheel.brakeTorque = 0;
                    wheel.motorTorque = 0;
                    var trans = wheel.GetComponent<Suspension>()._wheelModel.transform;
                    trans.localPosition = Vector3.zero;
                    trans.localRotation = Quaternion.identity;
                }

                carObject.transform.position = new Vector3(0, 0.32f, 0);
                carObject.transform.rotation = Quaternion.identity;
            }

            GeneratePath();

            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            carScript.Steering = 0;
            carScript.Throttle = 0;

            TotalReward = 0;
            RewardMult = 1;
            AgentDone = false;
            AgentSuccessful = false;

            throttle = 0;
            steering = 0;
            breaking = false;

            ParkingBehaviour.ResetBehaviour();
            DrivingBehaviour.ResetBehaviour();

            PlaceSkyCollider();

            Observations.Clear();
            PathObservations.Clear();
            RoadObservations.Clear();
            ObstacleObservations.Clear();
            OtherObservations.Clear();


            carObject.gameObject.SetActive(true);
        }

        public float ForwardSpeed
        {
            get { return carObject.transform.InverseTransformVector(_rb.velocity).z; }
        }

        /// <summary>
        /// Generates new path using assigned PathGenerator and updates agent's position.
        /// </summary>
        public void GeneratePath()
        {
            Path path;

            if (UsePathSeed) path = PathGenerator.GeneratePathFromSeed(Settings.MinPathLength, PathSeed);
            else path = PathGenerator.GenerateRandomPath(Settings.MinPathLength);

            if (path.Type.SwitchesCarObjects())
            {
                GetCarFromManager(path.Car);
            }
            PathChecker.SetPath(path);
            PathChecker.Restart();

            if (path.Type.EndsOnParkingSpace())
            {
                path.ParkingSpace.AssignedAgents[path.ParkingSpaceId]++;
            }
        }

        /// <summary>
        /// Finishes current episode in safe manner and returns borrowed carObject if needed.
        /// </summary>
        /// <param name="success"></param>
        public void EndEpisode(bool success)
        {
            ExperimentEnded = true;

            if (GeneratedPath != null)
            {
                if (GeneratedPath.Type.EndsOnParkingSpace())
                {
                    GeneratedPath.ParkingSpace.AssignedAgents[GeneratedPath.ParkingSpaceId]--;
                }
            }

            if (Settings.PathType.SwitchesCarObjects())
            {
                carObject.transform.position = GeneratedPath.ParkingPosition + Vector3.up * 0.38f;
                carObject.transform.forward = GeneratedPath.ParkingDirection;

                ReturnCarToManager();
            }
        }

        public bool IsDone()
        {
            return AgentDone;
        }

        /// <summary>
        /// Checks if agent is in traffic jam.
        /// </summary>
        /// <param name="distance"></param>
        /// <returns></returns>
        public bool IsBehindCar(out float distance)
        {
            Vector3 f = carObject.transform.forward;
            f.y = 0;

            float viewDistance = Settings.ViewDistance / 3.5f;
            distance = viewDistance;

            for (int ang = -5; ang < 10; ang +=5)
            {
                Vector3 dir = Quaternion.Euler(0, ang, 0) * f;

                bool hit = Physics.Raycast(carObject.transform.position, dir, out RaycastHit info, viewDistance, RayCastLayerMask);

                if (!hit)
                {
                    if (Settings.DebugRaysIsBehindCarCheck && (IsMainAgent || !Settings.ShowOnlyMainCarRays))
                    {
                        Debug.DrawLine(carObject.transform.position, carObject.transform.position + dir.normalized * viewDistance);
                    }
                    continue;
                }

                if (info.distance < distance)
                {
                    distance = info.distance;
                }

                Vector3 f2 = info.transform.forward;
                f2.y = 0;

                float angle = Vector3.Angle(f, f2);

                if (angle < 20)
                {
                    if (Settings.DebugRaysIsBehindCarCheck && (IsMainAgent || !Settings.ShowOnlyMainCarRays))
                    {
                        Debug.DrawLine(carObject.transform.position, carObject.transform.position + dir.normalized * viewDistance, Color.red);
                    }
                    return true;
                }
                else
                {
                    if (Settings.DebugRaysIsBehindCarCheck && (IsMainAgent || !Settings.ShowOnlyMainCarRays))
                    {
                        Debug.DrawLine(carObject.transform.position, carObject.transform.position + dir.normalized * viewDistance);
                    }
                }
            }

            return false;
        }

        public float GetTotalReward()
        {
            return TotalReward * RewardMult;
        }

        public void SetPathSeed(int seed)
        {
            PathSeed = seed;
            UsePathSeed = true;
        }

        public void DisablePathSeed()
        {
            UsePathSeed = false;
        }

        /// <summary>
        /// removes all unity scripts of given type from given game objects
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        protected void RemoveAllScripts<T>(GameObject obj) where T : UnityEngine.Object
        {
            T[] scripts = obj.GetComponentsInChildren<T>();
            foreach (var item in scripts)
            {
                DestroyImmediate(item);
            }
        }

        /// <summary>
        /// Adds reward to agent and logs reward type.
        /// </summary>
        /// <param name="reward"></param>
        /// <param name="type"></param>
        public void AddReward(float reward, string type)
        {
            if (reward == 0) return;

            if (Rewards.ContainsKey(type)) Rewards[type] += reward;
            else Rewards.Add(type, reward);

            TotalReward += reward;

            AddRewardAction?.Invoke(reward);
        }

        public void AddRewardMult(float mult)
        {
            RewardMult *= mult;
        }

        /// <summary>
        /// finishes episode and logs end reason.
        /// </summary>
        /// <param name="printRewards"></param>
        /// <param name="type"></param>
        /// <param name="additionalInfo"></param>
        /// <param name="success"></param>
        public void Done(bool printRewards, EndingType type, string additionalInfo = "", bool success = false)
        {
            if (printRewards)
            {
                string msg = name + ": " + CurrentBehaviour.GetBehaviourName() + " finished. Reason: " + type.ToString() + "\n" + TotalReward.ToString("0.000") + "  ";

                foreach (var pair in Rewards)
                {
                    msg += "  " + pair.Key.Substring(0, Math.Min(pair.Key.Length, 4)) + ": " + pair.Value.ToString("0.000");
                }

                msg += "\ntime:" + (CurrentBehaviour.GetTime() / 50f).ToString("0.00") + " seconds";
                msg += "\ndamage:" + CurrentBehaviour.NumberOfCrashes.ToString("0.00") + " (c: " + CurrentBehaviour.CrossroadDamage.ToString("0.00") + ", r: " + CurrentBehaviour.RoadDamage.ToString("0.00") + ", ps: " + CurrentBehaviour.ParkingSpaceDamage.ToString("0.00") + ")";
                if (CurrentBehaviour.GetType() == DrivingBehaviour.GetType())
                {
                    msg += "\ndistance traveled aligned:" + DrivingBehaviour.GetDistancePercentageTraveledAligned();
                }
                msg += "\nend position: " + GetPosition() + ", end speed:" + ForwardSpeed;
                if (additionalInfo != "")
                {
                    msg += "\n" + additionalInfo;
                }

                Debug.Log(msg);
            }

            Rewards.Clear();

            //behaviour switch if necessary
            if ((PathChecker.GeneratedPath.Type.SwitchToParkingBehaviour())
                && CurrentBehaviour.GetType() == DrivingBehaviour.GetType()
                && success)
            {
                ChangeBehaviour(this, BehaviourState.Parking);

                ParkingBehaviour.NumberOfCrashes = DrivingBehaviour.NumberOfCrashes;
            }
            else
            {
                if (Statistics != null)
                {
                    float parkingScore = 0;
                    if (CurrentBehaviour.GetType() == ParkingBehaviour.GetType())
                    {
                        parkingScore = ParkingBehaviour.LastReward;
                    }

                    float time = CurrentBehaviour.GetTime();
                    if (GeneratedPath.Type.SwitchToParkingBehaviour())
                    {
                        time += DrivingBehaviour.GetTime();
                    }

                    Statistics.AddEnding(type,
                        GetPosition(),
                        CurrentBehaviour.NumberOfCrashes,
                        DrivingBehaviour.GetDistancePercentageTraveledAligned(),
                        time / 50,
                        CurrentBehaviour.CrossroadDamage,
                        CurrentBehaviour.RoadDamage,
                        CurrentBehaviour.ParkingSpaceDamage, 
                        parkingScore
                    );
                }

                DoneAction?.Invoke();
                EndEpisode(success);

                AgentDone = true;
                AgentSuccessful = success;
            }
        }

        /// <summary>
        /// Adds observation of given type.
        /// </summary>
        /// <param name="f"></param>
        /// <param name="type"></param>
        public void AddVectorObs(float f, ObservationType type)
        {
            Observations.Add(f);

            switch (type)
            {
                case ObservationType.Road:
                    RoadObservations.Add(f);
                    break;
                case ObservationType.Obstacle:
                    ObstacleObservations.Add(f);
                    break;
                case ObservationType.Path:
                    PathObservations.Add(f);
                    break;
                case ObservationType.Other:
                    OtherObservations.Add(f);
                    break;
                default:
                    break;
            }
        }

        public void AddVectorObs(Vector3 vec, ObservationType type)
        {
            AddVectorObs(vec.x, type);
            AddVectorObs(vec.y, type);
            AddVectorObs(vec.z, type);

            //MainAgentScript.AddVectorObs(vec);
        }

        public Vector3 GetPosition()
        {
            return carObject.transform.position;
        }

        public Transform GetTransform()
        {
            if (carObject == null) return null;

            return carObject.transform;
        }

        public bool OutputCarGizmos
        {
            get { return !Settings.ShowOnlyMainCarRays || IsMainAgent; }
        }

        public bool IsMainAgent
        {
            get { return name == "Agent"; }
        }

        /// <summary>
        /// Switches to given behaviour and corresponding brain.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="state"></param>
        private void ChangeBehaviour(object sender, BehaviourState state)
        {
            CurrentBehaviourType = state;
            switch (state)
            {
                case BehaviourState.Driving:
                    CurrentBehaviour = DrivingBehaviour;
                    if (Settings.DebugLogBrainSwitch) Debug.Log("changed to DRIVING behaviour " + CurrentBehaviour.GetObservationsCount());
                    break;
                case BehaviourState.Parking:
                    CurrentBehaviour = ParkingBehaviour;
                    if (Settings.DebugLogBrainSwitch) Debug.Log("changed to PARKING behaviour " + CurrentBehaviour.GetObservationsCount());
                    break;
                case BehaviourState.Finished:
                    break;
                default:
                    break;
            }

            var model = CurrentBehaviour.GetBehaiourModel();
            if (model != null && RLAgentScript != null)
            {
                RLAgentScript.GiveModel(CurrentBehaviour.GetBehaviourName(), model);
                RLAgentScript.SetObsVectorSize(CurrentBehaviour.GetObservationsCount());
                if (Settings.DebugLogBrainSwitch) Debug.Log("Given " + CurrentBehaviour.GetBehaviourName());
            }

        }

        public bool IsOnCrossroad()
        {
            return PathChecker.Status == NavMesh.CarStatus.OnCrossroad;
        }

        public bool IsOnRoad()
        {
            return PathChecker.Status == NavMesh.CarStatus.OnRoad;
        }

        /// <summary>
        /// Returns true if all road edge observations return 0 
        /// </summary>
        /// <returns></returns>
        public bool IsOutsidePath()
        {
            if (RoadObservations.Count == 0) return false;

            for (int i = 0; i < RoadObservations.Count; i++)
            {
                if (RoadObservations[i] != 1) return false;
            }

            return true;
        }
    }

    public enum ObservationType
    {
        Road = 0, 
        Obstacle,
        Path,
        Other
    }
}
