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
    /// This class servers to generate statistics of end reasons over certain time period.
    /// </summary>
    [Serializable]
    public class Statistics
    {
        private const string FloatFormat = "{0,3:0.00}";

        private int Step;

        public StatsFrame CurrentPeriod;
        [ReadOnly] public string CurrentPeriodBreakdown;

        [ReadOnly] public StatsFrame[] PreviousPeriods;
        [ReadOnly] public string[] HistoryBreakdowns;



        public Statistics(int step, int lastN = 100)
        {
            this.Step = step;
            CurrentPeriod = new StatsFrame();
            PreviousPeriods = new StatsFrame[lastN];

            HistoryBreakdowns = new string[lastN];
        }

        public void AddEnding(EndingType type, float carDamage, float alignedPercentage, float episodeLength, float crossroadDamage, float roadDamage, float parkingSpaceDamage, float parkingScore)
        {
            CurrentPeriod.AddEnding(type, carDamage, alignedPercentage, episodeLength, crossroadDamage, roadDamage, parkingSpaceDamage, parkingScore);
            CurrentPeriodBreakdown = CurrentPeriod.UpdateRates();
        }

        public void Tick(int time)
        {
            if (time % Step == 0)
            {
                for (int i = HistoryBreakdowns.Length - 1; i > 0; i--)
                {
                    HistoryBreakdowns[i] = HistoryBreakdowns[i-1];
                    PreviousPeriods[i] = PreviousPeriods[i - 1];
                }

                HistoryBreakdowns[0] = CurrentPeriodBreakdown;
                PreviousPeriods[0] = CurrentPeriod;

                CurrentPeriod = new StatsFrame();
                CurrentPeriodBreakdown = CurrentPeriod.UpdateRates();
            }
        }

        /// <summary>
        /// This class colllects statistics over single time period.
        /// </summary>
        [Serializable]
        public class StatsFrame
        {
            [Header("Ending statistics")]
            [ReadOnly] public int TotalEndings;
            [ReadOnly] public int CountCarCrash;
            [ReadOnly] public int CountSuccess;
            [ReadOnly] public int CountTimeout;
            [ReadOnly] public int CountOutOfPath;
            [HideInInspector] public int CountFastFinish;

            [ReadOnly] public float CrashRate;
            [ReadOnly] public float FinishRate;
            [HideInInspector] public float FastFinishRate;
            [ReadOnly] private float TimeoutRate;
            [ReadOnly] private float OutOfPathRate;

            [Header("Episode statistics")]
            [ReadOnly] public float AverageCarDamage;
            [ReadOnly] public float AverageAlignedPercentage;
            [ReadOnly] public float AverageEpisodeLength;
            [ReadOnly] public float AverageSuccesfullEpisodeLength;
            [ReadOnly] public float AverageCrossroadDamage;
            [ReadOnly] public float AverageRoadDamage;
            [ReadOnly] public float AverageParkingSpaceDamage;
            [ReadOnly] public float AverageParkingScore;

            private float TotalCarDamage;
            private float TotalAlignedPercentage;
            private float TotalEpisodeLength;
            private float TotalSuccesfullEpisodeLength;
            private float TotalCrossroadDamage;
            private float TotalRoadDamage;
            private float TotalParkingSpaceDamage;

            private float TotalParkingScore;
            private int TotalParkingEndings;


            [ReadOnly] public string Stats;

            public void AddEnding(EndingType type, float carDamage, float alignedPercentage, float episodeLength, float crossroadDamage, float roadDamage, float parkingSpaceDamage, float parkingScore)
            {
                TotalCarDamage += carDamage;
                TotalAlignedPercentage += alignedPercentage;
                TotalEpisodeLength += episodeLength;
                if (type == EndingType.Finished) TotalSuccesfullEpisodeLength += episodeLength;
                TotalCrossroadDamage += crossroadDamage;
                TotalRoadDamage += roadDamage;
                TotalParkingSpaceDamage += parkingSpaceDamage;

                if (parkingScore > 0 && type == EndingType.Finished)
                {
                    TotalParkingEndings++;
                    TotalParkingScore += parkingScore;
                }

                AddEnding(type);
            }



            private void AddEnding(EndingType type)
            {
                TotalEndings++;

                if (type == EndingType.Crash) CountCarCrash++;
                else if (type == EndingType.Stuck || type == EndingType.OutOfPath || type == EndingType.TooFarFromLine) CountOutOfPath++;
                else if (type == EndingType.Finished) CountSuccess++;
                else if (type == EndingType.GlobalTimeout || type == EndingType.ProgressTimeout) CountTimeout++;
                else Debug.Log("unknown");
            }

            public string UpdateRates()
            {
                CrashRate = (float)CountCarCrash / TotalEndings * 100;
                FinishRate = (float)CountSuccess / TotalEndings * 100;
                TimeoutRate = (float)CountTimeout / TotalEndings * 100;
                OutOfPathRate = (float)CountOutOfPath / TotalEndings * 100;
                FastFinishRate = (float)CountFastFinish / TotalEndings * 100;

                AverageAlignedPercentage = TotalAlignedPercentage / TotalEndings;
                AverageCarDamage = TotalCarDamage / TotalEndings;
                AverageCrossroadDamage = TotalCrossroadDamage / TotalEndings;
                AverageRoadDamage = TotalRoadDamage / TotalEndings;
                AverageParkingSpaceDamage = TotalParkingSpaceDamage / TotalEndings;
                AverageEpisodeLength = TotalEpisodeLength / TotalEndings;
                AverageSuccesfullEpisodeLength = TotalSuccesfullEpisodeLength / CountSuccess;
                AverageParkingScore = TotalParkingScore / TotalParkingEndings;

                Stats = "C: " + String.Format(FloatFormat, CrashRate) +
                        "  F: " + String.Format(FloatFormat, FinishRate) +
                        //"  FF: " + String.Format(FloatFormat, FastFinishRate) +
                        "  T: " + String.Format(FloatFormat, TimeoutRate) +
                        "  S: " + String.Format(FloatFormat, OutOfPathRate);

                return Stats;
            }
        }
    }
}
