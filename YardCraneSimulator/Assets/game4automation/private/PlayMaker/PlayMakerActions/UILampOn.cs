// Game4Automation (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 in2Sight GmbH - Usage of this source code only allowed based on License conditions see https://game4automation.com/lizenz  

using game4automation;

#if GAME4AUTOMATION_PLAYMAKER

namespace HutongGames.PlayMaker.Actions
{
    [ActionTarget(typeof(Drive), "gameObject")]
    [ActionCategory("Game4Automation")]
    [Tooltip("Stop Game4Automatopm Drive")]
    public class UILampOn : FsmStateAction
    {
        public FsmOwnerDefault Lamp;
        public FsmBool TurnLampOn = true;
        private UILamp _lamp;
       

       
        public override string ErrorCheck()
        {
            string error = "";
      
            if (Fsm.GetOwnerDefaultTarget(Lamp)==null)
            {
                error = "Game4Automation no Lamp component selected";
            }
            else
            {
                if (Fsm.GetOwnerDefaultTarget(Lamp).GetComponent<UILamp>()==null)
                {
                    error = "Game4Automation UILamp component missing at this GameObject";
                }
            }

            return error;

        }
        
          
        public override void Reset()
        {
            base.Reset();
            if (this.State != null)
            this.State.ColorIndex = 1;

        }

        public override void OnEnter()
        {
            _lamp = Fsm.GetOwnerDefaultTarget(Lamp).GetComponent<UILamp>();
            if (_lamp != null)
            {
                _lamp.LampIsOn = TurnLampOn.Value;
            }

            Finish();
        }
    }
}
#endif