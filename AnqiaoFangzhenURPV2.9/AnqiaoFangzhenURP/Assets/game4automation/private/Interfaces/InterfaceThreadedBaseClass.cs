// Game4Automation (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 in2Sight GmbH - Usage of this source code only allowed based on License conditions see https://game4automation.com/lizenz  

using UnityEngine;
using System.Threading;
using System;
using NaughtyAttributes;
namespace game4automation
{
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/custom-interfaces")]
    public class InterfaceThreadedBaseClass : InterfaceBaseClass
    {
        [HideIf("NoThreading")] 
        [Foldout("Thread Status")]
        public int MinCommCycleMs = 0;
        [HideIf("NoThreading")] 
        [Foldout("Thread Status")]
        public int CommCycleMeasures= 1000;
        [HideIf("NoThreading")] 
        [Foldout("Thread Status")]
        [ReadOnly] public int CommCycleNr;
        [HideIf("NoThreading")]
        [Foldout("Thread Status")]
        [ReadOnly] public int CommCycleMs;
        [HideIf("NoThreading")] 
        [Foldout("Thread Status")]
        [ReadOnly] public int CommCycleMin;
        [HideIf("NoThreading")] 
        [Foldout("Thread Status")]
        [ReadOnly] public float CommCycleMed;
        [HideIf("NoThreading")] 
        [Foldout("Thread Status")]
        [ReadOnly] public int CommCycleMax;
        [HideIf("NoThreading")] 
        [Foldout("Thread Status")]
        [ReadOnly] public int CommCycleMeasureNum;
        [HideIf("NoThreading")] 
        [Foldout("Thread Status")]
        [ReadOnly] public string ThreadStatus; 
        private Thread CommThread;
        private DateTime ThreadTime;
        private bool run;
        private float commcyclesum = 0;
        private DateTime last;
        [HideInInspector] public bool NoThreading = false;
        
        protected virtual void CommunicationThreadUpdate()
        {
        }
        
        
        protected virtual void CommunicationThreadClose()
        {
        }
        
        public override void OpenInterface()
        {
            if (NoThreading)
            {
                ThreadStatus = "Threading turned off";
                return;
            }
            ThreadStatus = "running";
            CommThread = new Thread(CommunicationThread);
            CommCycleNr = 0;
            run = true;
            ResetMeasures();
            CommThread.Start();
        }
        
        public override void CloseInterface()
        {
            run = false;
            if (CommThread!=null)
                   CommThread.Abort();
        }

        private void ResetMeasures()
        {
            CommCycleMeasureNum = 0;
            CommCycleMin = 99999;
            CommCycleMax = 0;
            commcyclesum = 0;
        }
        
        void CommunicationThread()
        {
            DateTime end;
            bool first = true;
            do
            {
                CommCycleMeasureNum++;
                CommunicationThreadUpdate();
                ThreadTime = last;
                CommCycleNr++;
                end = DateTime.Now;
                TimeSpan span = end - last;
                last = DateTime.Now;
                if (!first)
                {
                    CommCycleMs = (int) span.TotalMilliseconds;

                    // Calculate Communication Statistics
                    commcyclesum = commcyclesum + CommCycleMs;
                    if (CommCycleMs > CommCycleMax)
                        CommCycleMax = CommCycleMs;
                    if (CommCycleMs < CommCycleMin)
                        CommCycleMin = CommCycleMs;
                    CommCycleMed = commcyclesum / CommCycleMeasureNum;
                    if (CommCycleMeasureNum > CommCycleMeasures)
                        ResetMeasures();
                }

                first = false;

                if (MinCommCycleMs-CommCycleMs>0)
                    Thread.Sleep(MinCommCycleMs-CommCycleMs);
            } while (run == true);
            CommunicationThreadClose();
            
        }

    }
}