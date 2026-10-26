// Game4Automation (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 in2Sight GmbH - Usage of this source code only allowed based on License conditions see https://game4automation.com/lizenz  

using System.Collections.Generic;
using UnityEngine;


namespace game4automation
{
    public interface ISignalInterface
    {
        public List<BehaviorInterfaceConnection> GetConnections();
        public List<Signal> GetSignals();
        GameObject gameObject { get ; } 
    }
}

