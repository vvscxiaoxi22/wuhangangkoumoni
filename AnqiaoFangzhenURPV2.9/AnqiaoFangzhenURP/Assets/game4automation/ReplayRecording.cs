using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace game4automation
{
    [RequireComponent(typeof(Recorder))]
    public class ReplayRecording : Game4AutomationBehavior,ISignalInterface
    {
        public string Sequence;
        public PLCOutputBool StartOnSignal;

        private Recorder recorder;
        // Start is called before the first frame update
        void Start()
        {
            recorder = GetComponent<Recorder>();
            if (StartOnSignal != null)
                StartOnSignal.EventSignalChanged.AddListener(OnSignal);
        }

        private void OnSignal(Signal signal)
        {
            if (((PLCOutputBool)signal).Value == true)
                recorder.StartReplay(Sequence);
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }

}
