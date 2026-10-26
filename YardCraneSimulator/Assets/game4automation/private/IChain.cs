using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace game4automation
{
    public interface IChain
    {
        public Vector3 GetPosition(float normalizedposition , bool normalized = true);
        public Vector3 GetDirection(float normalizedposition , bool normalized = true);

        public float CalculateLength();

        public bool UseSimulationPath();
    }

}
