// Game4Automation (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 in2Sight GmbH - Usage of this source code only allowed based on License conditions see https://game4automation.com/lizenz  
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System;

namespace game4automation
{
	//! Class for saving the connection data - the signal and the name of the property where the signal is attached to
	public class BehaviorInterfaceConnection
	{
		public Signal Signal;
		public string Name;
	}

	//! Base class for all behavior models with connection to PLC signals. 
	public class BehaviorInterface : Game4AutomationBehavior, ISignalInterface
	{

	
		public List<BehaviorInterfaceConnection> ConnectionInfo = new List<BehaviorInterfaceConnection>();

	
		public new List<BehaviorInterfaceConnection> GetConnections()
		{
			ConnectionInfo = UpdateConnectionInfo();
			return ConnectionInfo;
		}

		public new List<Signal> GetSignals()
		{
			return GetConnectedSignals();
		}
	}
}
