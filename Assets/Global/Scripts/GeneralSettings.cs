using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Barracuda;

namespace Assets.Global.Scripts
{
    /// <summary>
    /// This class contains majority of settings of given world and experiment.
    /// </summary>
    public class GeneralSettings : MonoBehaviour
    {
        [Tooltip("File describing agent's inputs")]
        public string AgentSettingsFile;

        [Header("Map settings")]

        public float MinLineWidth = 1f;
        public float MaxLineWidth = 1.66f;
        //[Tooltip("OSM file describing map")]
        [HideInInspector] public string MapFilePath = "maps\\newstd.osm";
        [Tooltip("Constant defining minimal crossroad size so that it is possible to drive trough crossroad in all direction.")]
        public float CrossroadSize = 3.5f;
        [HideInInspector]
        public float MaxHeight = 0f;
        [HideInInspector]
        public bool DisableMovingCrossroadCorners = true;
        [Tooltip("If True, random line width and parking space generation will use fixed seed when in editor.")]
        public bool UseFixedMapSeedInEditor = true;
        [Tooltip("Makes failure checks more lenient to prevent unecessary restarts.")]
        public bool EasyFailureCheck = false;
        [Tooltip("Max time in frames after car outside road will get reseted (50 fps).")]
        public int MaxTimeOutsideMap = 250;

        [Header("Parking space settings")]
        public int ParkingAreasPerRoad = 0;
        public float PSLength = 6f;
        public float PSWidth = 2.5f;
        public int PSInitCars = 2;
        [Tooltip("Minimum number of parking spaces per parking area.")]
        public int PSMinSlots = 3;
        [Tooltip("Maximum number of parking spaces per parking area.")]
        public int PSMaxSlots = 4;




        [Header("Car settings")]
        public float SteerAngle = 50f;
        public float ViewDistance = 30;
        [Tooltip("Exponent of turn signal. ")]
        public float TurnSignalExponent = 1;
        [Tooltip("Initial heath of car. Maximum mnumber of crashes that agent can sustain.")]
        public float MaxNumberOfCrashes = 10;
        [Tooltip("This variable defines how much gets car damaged when it continues to touch an obstacle.")]
        public float PersistingCrashRatio = 0.01f;
        [HideInInspector]
        public bool ContinuousBrain = true;
        [HideInInspector]
        public bool UseRelativeCarSpeed = true;
        [HideInInspector]
        public bool UseSkyCarDetection = false;
        public GameObject CarPrefab;




        [Header("Debug settings")]
        [Tooltip("If True, debugs summary of each ended episode in console.")]
        public bool DebugEpisodeSummary = true;
        [Tooltip("If true, only debug rays of main car will be showed.")]
        public bool ShowOnlyMainCarRays = true;
        [Tooltip("Shows road edge detection rays.")]
        public bool DebugRaysRoadDistance = true;
        [Tooltip("Shows obstacle detection rays.")]
        public bool DebugRaysCarDistance = true;
        [Tooltip("Shows navigation rays.")]
        public bool DebugRaysNextPoint = true;
        [HideInInspector]
        public bool DebugRaysPath = false;
        [Tooltip("Shows rays that detect if agent is in trafic jam.")]
        public bool DebugRaysIsBehindCarCheck = false;
        public bool DebugRaysOther = false;
        [HideInInspector]
        public bool DebugLogBrainSwitch = false;
        [Tooltip("If enabled, will make only main agent to be controlled with heuristic controls.")]
        public bool OnlyControlMainAgentWithHeuristics = true;
        [Tooltip("If enabled will show rays representing end of episode positions and reasons.")]
        public bool ShowEndPositions = false;
        [HideInInspector]
        public bool ShowParkingSpaceAvailibility = false;




        [Header("Experiment settings")]
        [Tooltip("Main type of generated path")]
        public Assets.Global.Scripts.Map.PathType PathType;
        [Tooltip("Secondary path type. Agent is assigned this path type with probability given by next variable.")]
        public Assets.Global.Scripts.Map.PathType SecondaryType;
        [Range(0, 1)]
        public float SecondaryTypeProbability = 0;
        [Tooltip("Minimal length of generated path.")]
        public float MinPathLength = 1000;




        [Header("Driving behaviour settings")]
        [Tooltip("Model used by DB in simulation.")]
        public NNModel DBModel;
        [Tooltip("Total reward for progress on path.")]
        public float DBTotalDistanceReward = 0.5f;
        [Tooltip("Reward for finishing path.")]
        public float DBFinishedReward = 0.3f;
        [HideInInspector]
        public float DistToRewardMult = 0f;
        [Tooltip("Reward for failures.")]
        public float DBFailureReward = -1;
        [HideInInspector]
        public int MinFramesBetweenCrashes = 15;
        [HideInInspector]
        public float DistFromCenterRewardMult = 0f;
        [HideInInspector]
        public float DistFromCenterTreshold = 0;
        [HideInInspector]
        public float TooCloseObstacleTreshold = 0;
        [HideInInspector]
        public float TooCloseObstacleRewardMult = 0;
        [Tooltip("Time reward that agent recieves each frame (50 fps).")]
        public float DBBaseTimeRewardPerFrame = -0.01f;
        [HideInInspector]
        public float DBSteeringRewardRatio = 0;
        [Tooltip("Max direction angle of next few points on path before agent in order to recieve align reward.")]
        public float DBMaxAlignRewardAngle = 2;
        [Tooltip("Reward multiplier for change of steering signal.")]
        public float DBBaseSteeringChangeRewardMult = -0.001f;
        [Tooltip("Minimal amount of progress that agent has to achieve in order to reset progres timer.")]
        public float DBMinProgressIncrement = 5;
        [Tooltip("Progress time limit in frames (at 50fps)")]
        public float DBMaxTimeWithoutProgress = 1200;
        [Tooltip("Time limit in frames (at 50fps)")]
        public float DBMaxExperimentTime = 3600;
        [HideInInspector]
        public float DBMaxTimeOnCrossroad = 750000;
        [HideInInspector]
        public float DBMaxDistanceFromLineCenter = 2;
        [Tooltip("Distance treshold for \"stuck\" failure. Agents get resetted if they get closer to line edge than this value.")]
        public float DBMinDistanceFromLineEdge = 0.35f;
        [HideInInspector]
        public float DBDistFromGoal = 0.5f;




        [Header("Parking behaviour settings")]
        [Tooltip("Model used by PB in simulation.")]
        public NNModel PBModel;
        [Tooltip("Time limit in frames (at 50fps)")]
        public float PBMaxTime = 1800;
        [Tooltip("Distance treshold constant for reward function.")]
        public float PBDistanceTreshold = 5;
        [Tooltip("Angle treshold constant for reward function.")]
        public float PBAngleTreshold = 90;
        [HideInInspector]
        public float PBDistanceRewardPower = 1f;
        [Tooltip("If true, agent wont recieve negative rewards for lowering his position score.")]
        public bool PBBaseRewardOnBestPositionOnly = false;
        [Tooltip("If true, agent gets rewarded only if he is in a place from which he can directly drive to parking position without driving back and forth.")]
        public bool PBUseConeReward = false;
        [Tooltip("Time reward that agent recieves each frame (50 fps).")]
        public float PBTimeRewardPerFrame = -0.0003f;
        [HideInInspector]
        public int PBMaxAgentsPerParkingSpace = 1;
        [Tooltip("Reward multiplier for change of steering signal.")]
        public float PBSteeringChangeRewardMult = -0.01f;
        [Tooltip("Maximal init speed when training PB")]
        public float PBMaxInitSpeed = 10;
        [Tooltip("Time that agent has to spend without movement of correct place in order to finish episode.")]
        public float PBTimeWithoutMovementTreshold = 90;

        
        public GameObject PlacePrefab(GameObject prefab, Vector3 position, Vector3 forward)
        {
            var obj = Instantiate(prefab, transform);
            obj.transform.position = position;
            obj.transform.forward = forward;

            return obj;
        }
    }
}
