// Game4Automation (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 in2Sight GmbH - Usage of this source code only allowed based on License conditions see https://game4automation.com/lizenz  

using game4automation;

#if GAME4AUTOMATION_PLAYMAKER

namespace HutongGames.PlayMaker.Actions
{
    [ActionTarget(typeof(Drive), "gameObject")]
    [ActionCategory("Game4Automation")]
    [Tooltip("Start Game4Automatopm Drive")]
    public class DriveStart : FsmStateAction
    {
        public FsmOwnerDefault Drive;
        public FsmBool Forward=true;
        public FsmBool SetSpeed;
        public FsmFloat Speed;
        public FsmBool SetAcceleration;
        public FsmFloat Acceleration;
        public FsmGameObject StopAtSensor;
        
    

        private Drive _drive;
        private Sensor _sensor;
        
        public override string ErrorCheck()
        {
            string error = "";
      
            if (Fsm.GetOwnerDefaultTarget(Drive)==null)
            {
                error = "Game4Automation no Drive component selected";
            }
            else
            {
                if (Fsm.GetOwnerDefaultTarget(Drive).GetComponent<Drive>()==null)
                {
                    error = "Game4Automation  Drive component missing at this GameObject";
                }
            }

            return error;

        }
        
        public override void Reset()
        {
            base.Reset();
            if (this.State != null)
              this.State.ColorIndex = 3;
           

        }

        public override void OnEnter()
        {
            _drive = Fsm.GetOwnerDefaultTarget(Drive).GetComponent<Drive>();
            
            if (StopAtSensor.Value != null)
                _sensor = StopAtSensor.Value.GetComponent<Sensor>();
            
            if (_drive != null)
            {
                if (SetAcceleration.Value)
                    _drive.Acceleration = Acceleration.Value;
                if (SetSpeed.Value)
                    _drive.TargetSpeed = Speed.Value;

                if (Forward.Value)
                {
                    _drive.JogForward = true;
                    _drive.JogBackward = false;
                }
                else
                {
                    _drive.JogBackward = true;
                    _drive.JogForward = false;
                }
                if (_sensor==null)
                    Finish();
            }


        
        }

        public override void OnUpdate()
        {
            if (_sensor != null)
            {
                if (_sensor.Occupied)
                {
                    _drive.JogForward = false;
                    _drive.JogBackward = false;
                    Finish();
                }
            }
        }
        
    }
}
#endif