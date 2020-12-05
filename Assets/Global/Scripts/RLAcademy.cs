using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using MLAgents;
using Assets.Global.Scripts.Map;

namespace Assets.Global.Scripts
{
    class RLAcademy : Academy
    {
        public GeneralSettings Settings;

        /// <summary>
        /// Creates callback funcitons for curriculum learning.
        /// </summary>
        public override void InitializeAcademy()
        {
            FloatProperties.RegisterCallback("distance_reward", x => { Settings.DistToRewardMult = x; });
            FloatProperties.RegisterCallback("min_length", x => { Settings.MinPathLength = x; });
            FloatProperties.RegisterCallback("end_reward", x => { Settings.DBFinishedReward = x; });
            FloatProperties.RegisterCallback("max_crashes", x => { Settings.MaxNumberOfCrashes = x; });
            FloatProperties.RegisterCallback("dist_from_center_mult", x => { Settings.DistFromCenterRewardMult = x; });
            FloatProperties.RegisterCallback("dist_from_obstacle_mult", x => { Settings.TooCloseObstacleRewardMult = x; });

            FloatProperties.RegisterCallback("path_type", x => { Settings.PathType = (PathType)x; });
            FloatProperties.RegisterCallback("secondary_path_type_probability", x => { Settings.SecondaryTypeProbability = x; });
            FloatProperties.RegisterCallback("turn_signal_power", x => { Settings.TurnSignalExponent = x; });

            FloatProperties.RegisterCallback("pb_dist_treshold", x => { Settings.PBDistanceTreshold = x; });
            FloatProperties.RegisterCallback("pb_angle_treshold", x => { Settings.PBAngleTreshold = x; });
            FloatProperties.RegisterCallback("pb_dist_reward_power", x => { Settings.PBDistanceRewardPower = x; });
            FloatProperties.RegisterCallback("pb_reward_type", x => { Settings.PBBaseRewardOnBestPositionOnly = x == 1; });
            FloatProperties.RegisterCallback("pb_max_init_speed", x => { Settings.PBMaxInitSpeed = x; });
            FloatProperties.RegisterCallback("pb_use_cone_reward", x => { Settings.PBUseConeReward = x == 1; });
            FloatProperties.RegisterCallback("pb_time_per_frame_reward", x => { Settings.PBTimeRewardPerFrame = x; });
            FloatProperties.RegisterCallback("pb_steering_change_reward_mult", x => { Settings.PBSteeringChangeRewardMult = x; });

            FloatProperties.RegisterCallback("db_total_distance_reward", x => { Settings.DBTotalDistanceReward = x; });
            FloatProperties.RegisterCallback("db_steering_reward_ratio", x => { Settings.DBSteeringRewardRatio = x; });
            FloatProperties.RegisterCallback("db_base_steering_change_reward_mult", x => { Settings.DBBaseSteeringChangeRewardMult = x; });
            FloatProperties.RegisterCallback("db_base_time_reward_per_frame", x => { Settings.DBBaseTimeRewardPerFrame = x; });
            FloatProperties.RegisterCallback("db_max_alignment_reward_angle", x => { Settings.DBMaxAlignRewardAngle = x; });
        }
    }
}
