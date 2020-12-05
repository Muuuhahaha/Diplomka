/*
 * This code is part of Arcade Car Physics for Unity by Saarg (2018)
 * 
 * This is distributed under the MIT Licence (see LICENSE.md for details)
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.Global.Scripts.Agents;

namespace VehicleBehaviour.Utils {
	public class CameraFollow : MonoBehaviour {
		[SerializeField] bool follow = true;
		public bool Follow { get { return follow; } set { follow = value; } }
		[SerializeField] GenericAgent agent;
		[SerializeField] Vector3 offset = new Vector3(0,3,-8);
		[Range(0, 10)]
		[SerializeField] float lerpPositionMultiplier = 1f;
		[Range(0, 10)]		
		[SerializeField] float lerpRotationMultiplier = 1f;

		Rigidbody rb;

		void Start () {
			rb = GetComponent<Rigidbody>();
		}

		void FixedUpdate() {
			if (!follow || agent == null) return;

			this.rb.velocity.Normalize();

			Quaternion curRot = transform.rotation;

            Transform trans = agent.GetTransform();
            if (trans == null) return;

            transform.LookAt(trans);

			Vector3 tPos = agent.GetTransform().position + agent.GetTransform().TransformDirection(offset);
			if (tPos.y < agent.GetTransform().position.y) {
				tPos.y = agent.GetTransform().position.y;
			}

			transform.position = Vector3.Lerp(transform.position, tPos, Time.fixedDeltaTime * lerpPositionMultiplier);
			transform.rotation = Quaternion.Lerp(curRot, transform.rotation, Time.fixedDeltaTime * lerpRotationMultiplier);

			if (transform.position.y < 0.5f) {
				transform.position = new Vector3(transform.position.x , 0.5f, transform.position.z);
			}
		}
	}
}
