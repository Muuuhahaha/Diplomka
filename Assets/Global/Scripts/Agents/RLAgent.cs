using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Global.Scripts;
using UnityEngine;
using Barracuda;
using MLAgents;
using Unity.Profiling;
using Assets.Global.Scripts.Map;

namespace Assets.Global.Scripts.Agents
{
    /// <summary>
    /// This class serves as wrapper of GenericAgent for usage in Reinforcement learning using ml-agents.
    /// </summary>
    [RequireComponent(typeof(GenericAgent))]
    public class RLAgent : Agent
    {
        private GenericAgent AgentScript;
        private GeneralSettings Settings { get { return AgentScript.Settings; } }

        public void Awake()
        {
            AgentScript = GetComponent<GenericAgent>();

            AgentScript.AddRewardAction += AddReward;
            AgentScript.DoneAction += Done;
        }

        public override void CollectObservations()
        {
            AgentScript.CollectObservations();

            foreach (var obs in AgentScript.Observations)
            {
                AddVectorObs(obs);
            }
        }

        public override void AgentAction(float[] vectorAction)
        {
            AgentScript.AgentAction(vectorAction);
        }

        public override void AgentReset()
        {
            AgentScript.AgentReset();
        }

        /// <summary>
        /// user controls for agent
        /// </summary>
        /// <returns></returns>
        public override float[] Heuristic()
        {
            if (!AgentScript.IsMainAgent && Settings.OnlyControlMainAgentWithHeuristics)
            {
                return new float[] { 0, 0, 0 };
            }

            float[] ret = new float[3];

            if (Input.GetKey(KeyCode.W)) ret[0] = 1;
            if (Input.GetKey(KeyCode.A)) ret[1] = -1;
            if (Input.GetKey(KeyCode.D)) ret[1] = 1;

            if (Input.GetKey(KeyCode.S)) ret[0] = -1;
            if (Input.GetKey(KeyCode.Space)) ret[2] = 1;

            return ret;
        }   

        public void SetObsVectorSize(int size)
        {
            collectObservationsSensor.m_Shape[0] = size;

            //not needed, just for visual clarity in Unity inspector
            GetComponent<BehaviorParameters>().m_BrainParameters.vectorObservationSize = size;
        }
    }
}
