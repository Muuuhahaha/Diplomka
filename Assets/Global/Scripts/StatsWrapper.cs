using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Assets.Global.Scripts.Agents.AgentBehaviours;


namespace Assets.Global.Scripts
{
    /// <summary>
    /// This class collects episodes statistics. Mainly end reasons and position of agents.
    /// </summary>
    public class StatsWrapper : MonoBehaviour
    {
        public GeneralSettings Settings;
        [Tooltip("If enables, pauses editor after amount of time given by next variable.")]
        public bool EnableAutoPauseInEditor = false;
        [Tooltip("If autopause enabled, pauses editor after given number of hours.")]
        public float AutoPauseAfterNHours = 1;
        [Tooltip("Height of rays showing end positions.")]
        public float RayHeight = 10;

        [Header("Ending position debug settings")]
        public Color CrashColor;
        public Color FinishedColor;
        public Color StuckColor;
        public Color TimeoutColor;

        public bool ShowCrashPositions;
        public bool ShowFinishPositions;
        public bool ShowStuckPositions;
        public bool ShowTimeoutPositions;

        [Header("Statistics")]
        [Tooltip("This instance produces statistics over periods of time lasting 20 secs.")]
        private Statistics T1000 = new Statistics(1000);
        [Tooltip("This instance produces statistics over periods of time lasting 200 secs.")]
        private Statistics T10000 = new Statistics(10000);
        [Tooltip("This instance produces statistics over periods of time lasting 2000 secs.")]
        private Statistics T100000 = new Statistics(100000);
        [Tooltip("This instance produces statistics over periods of time lasting 200 000 secs.")]
        private Statistics T10000000 = new Statistics(1000000);

        public Statistics.StatsFrame Stats = new Statistics.StatsFrame();

        [Tooltip("Current time since start (in frames at 50 fps).")]
        [ReadOnly]
        public int time = 0;

        private List<Vector3>[] EndingPositions;

        public void Awake()
        {
            const int c = 8; // number of position types

            EndingPositions = new List<Vector3>[c];
            for (int i = 0; i < c; i++)
            {
                EndingPositions[i] = new List<Vector3>();
            }
        }

        public void FixedUpdate()
        {
            T1000.Tick(time);
            T10000.Tick(time);
            T100000.Tick(time);
            T10000000.Tick(time);

            time++;

            ShowEndingPositions();

            if (EnableAutoPauseInEditor 
                && Mathf.RoundToInt(AutoPauseAfterNHours * 3600 * 50) == time 
                && Application.isEditor)
            {
                Debug.Break();
            }
        }

        public void AddEnding(EndingType type, Vector3 position, float carDamage, float alignedPercentage, float episodeLength, float crossroadDamage, float roadDamage, float parkingSpaceDamage, float parkingScore)
        {
            T1000.AddEnding(type, carDamage, alignedPercentage, episodeLength, crossroadDamage, roadDamage, parkingSpaceDamage, parkingScore);
            T10000.AddEnding(type, carDamage, alignedPercentage, episodeLength, crossroadDamage, roadDamage, parkingSpaceDamage, parkingScore);
            T100000.AddEnding(type, carDamage, alignedPercentage, episodeLength, crossroadDamage, roadDamage, parkingSpaceDamage, parkingScore);
            T10000000.AddEnding(type, carDamage, alignedPercentage, episodeLength, crossroadDamage, roadDamage, parkingSpaceDamage, parkingScore);

            Stats.AddEnding(type, carDamage, alignedPercentage, episodeLength, crossroadDamage, roadDamage, parkingSpaceDamage, parkingScore);
            Stats.UpdateRates();

            EndingPositions[(int)type].Add(position);
        }

        private void ShowEndingPositions()
        {
            if (!Settings.ShowEndPositions) return;

            ShowEndingPositions(ShowCrashPositions, EndingPositions[(int)EndingType.Crash], CrashColor);
            ShowEndingPositions(ShowFinishPositions, EndingPositions[(int)EndingType.Finished], FinishedColor);
            ShowEndingPositions(ShowStuckPositions, EndingPositions[(int)EndingType.Stuck], StuckColor);
            ShowEndingPositions(ShowStuckPositions, EndingPositions[(int)EndingType.OutOfPath], StuckColor);
            ShowEndingPositions(ShowStuckPositions, EndingPositions[(int)EndingType.TooFarFromLine], StuckColor);
            ShowEndingPositions(ShowTimeoutPositions, EndingPositions[(int)EndingType.CrossroadTimeout], TimeoutColor);
            ShowEndingPositions(ShowTimeoutPositions, EndingPositions[(int)EndingType.GlobalTimeout], TimeoutColor);
            ShowEndingPositions(ShowTimeoutPositions, EndingPositions[(int)EndingType.ProgressTimeout], TimeoutColor);
        }

        private void ShowEndingPositions(bool show, List<Vector3> positions, Color color)
        {
            if (!show) return;

            foreach (var position in positions)
            {
                Debug.DrawLine(position, position + Vector3.up * RayHeight, color);
            }
        }

        class EndingPosition
        {
            public EndingPosition(EndingType type, Vector3 position)
            {
                this.EndingType = type;
                this.Position = position;
            }

            Vector3 Position;
            EndingType EndingType;
        }
    }
}
