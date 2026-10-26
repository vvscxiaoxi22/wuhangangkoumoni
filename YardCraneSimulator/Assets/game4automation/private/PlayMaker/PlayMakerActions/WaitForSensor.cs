// Game4Automation (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 in2Sight GmbH - Usage of this source code only allowed based on License conditions see https://game4automation.com/lizenz  

using game4automation;

#if GAME4AUTOMATION_PLAYMAKER

namespace HutongGames.PlayMaker.Actions
{

	[ActionTarget(typeof(Drive), "gameObject")]              
	[ActionCategory("Game4Automation")]
	[Tooltip("Wait for a sensor signal")]
public class WaitForSensor : FsmStateAction
{
		public FsmOwnerDefault Sensor;
		public FsmBool WaitForOccupied;

		private Sensor _sensor;
		
		public override void Reset()
		{
			base.Reset();
			if (this.State != null)
			this.State.ColorIndex = 4;
		}


		public override string ErrorCheck()
		{
			string error = "";
      
			if (Fsm.GetOwnerDefaultTarget(Sensor)==null)
			{
				error = "Game4Automation no Sensor component selected";
			}
			else
			{
				if (Fsm.GetOwnerDefaultTarget(Sensor).GetComponent<Sensor>()==null)
				{
					error = "Game4Automation Sensor component missing at this GameObject";
				}
			}

			return error;

		}
		
		public override void OnEnter()
		{
		
			_sensor = Fsm.GetOwnerDefaultTarget(Sensor).GetComponent<Sensor>();
		
		}

		public override void OnUpdate()
		{
			if (_sensor.Occupied && WaitForOccupied.Value)
				Finish();
			if (!_sensor.Occupied && !WaitForOccupied.Value)
				Finish();

		}
}

}
#endif
