using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Assets.Global.Scripts.Agents;

namespace Assets.Global.Scripts
{
    /// <summary>
    /// This class is used to send crash info from object with RigidBody to GenericAgent script.
    /// </summary>
    class CollisionHandler : MonoBehaviour
    {
        GenericAgent AgentScript;


        private void OnCollisionEnter(Collision collision)
        {
            if (!UpdateReference()) return;
            if (AgentScript.IsDone()) return;

            AgentScript.CurrentBehaviour.AddCrash();
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!UpdateReference()) return;
            AgentScript.CurrentBehaviour.AddPersistingContact();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!UpdateReference()) return;
            if (AgentScript.IsDone()) return;

            AgentScript.CurrentBehaviour.AddCrash();
        }

        private void OnTriggerStay(Collider other)
        {
            if (!UpdateReference()) return;

            AgentScript.CurrentBehaviour.AddPersistingContact();
        }

        private bool UpdateReference()
        {
            AgentScript = GetComponentInParent<GenericAgent>();

            return AgentScript != null;
        }
    }
}
