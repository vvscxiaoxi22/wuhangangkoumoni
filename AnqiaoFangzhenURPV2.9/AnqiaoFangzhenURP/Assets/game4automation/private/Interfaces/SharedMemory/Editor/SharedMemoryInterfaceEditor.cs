// Game4Automation (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 in2Sight GmbH - Usage of this source code only allowed based on License conditions see https://game4automation.com/lizenz  
#if UNITY_STANDALONE_WIN
using UnityEditor;
using UnityEngine;

namespace game4automation
{
    [CustomEditor(typeof(SharedMemoryInterface))]
    public class SharedMemoryInterfaceEditor : UnityEditor.Editor {

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        
            SharedMemoryInterface myScript = (SharedMemoryInterface)target;
            if(GUILayout.Button("Import Signals"))
            {
                myScript.ImportSignals(false);;
            }
        }
    }
}
#endif