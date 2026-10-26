// Game4Automation (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 in2Sight GmbH - Usage of this source code only allowed based on License conditions see https://game4automation.com/lizenz  

using game4automation;


#if GAME4AUTOMATION_PLAYMAKER

namespace HutongGames.PlayMaker.Actions
{
    [ActionTarget(typeof(Drive), "gameObject")]
    [ActionCategory("Game4Automation")]
    [Tooltip("Stop Game4Automatopm Drive")]
    public class DriveStop : FsmStateAction
    {
        public FsmOwnerDefault Drive;
        private Drive _drive;

       
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
            this.State.ColorIndex = 4;

        }

        public override void OnEnter()
        {
            _drive = Fsm.GetOwnerDefaultTarget(Drive).GetComponent<Drive>();
            if (_drive != null)
            {
                _drive.JogForward = false;
                _drive.JogBackward = false;
            }

            Finish();
        }
    }
}
#endif