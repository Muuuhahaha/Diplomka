using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Global.Scripts
{
    interface IAgent
    {
        Vector3 GetPosition();
        Transform GetTransform();

        void AddVectorObs(float f);
        void AddVectorObs(Vector3 vec);
    }
}
