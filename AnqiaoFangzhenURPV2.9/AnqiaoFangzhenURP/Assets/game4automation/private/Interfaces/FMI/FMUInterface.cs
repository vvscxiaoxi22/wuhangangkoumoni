using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMI2;
using NaughtyAttributes;
using UnityEditor;

namespace game4automation
{
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/interfaces/fmi")]
    public class FMUInterface : InterfaceBaseClass
    {
        [System.Serializable]
        public class SignalMapping
        {
            public int FMUVariabelIndex;
            public Signal Signal;
        }
        
        public string FMUPath;
        private string InstanceName;

        [ReadOnly] public ModelDescription ModelDescription;
        private FMU fmu;

        [HideInInspector][SerializeField] private List<Signal> Inputs = new List<Signal>();
        [HideInInspector][SerializeField] private List<Signal> Outputs = new List<Signal>();
        [HideInInspector][SerializeField] private List<Signal> Calculated = new List<Signal>();
        [HideInInspector][SerializeField] private List<Signal> Parameters = new List<Signal>();
        // Start is called before the first frame update

        [Button("Select FMU path")]
        void ImportFMU()
        {
            #if UNITY_EDITOR
            string[] filters = {"FMUs", "fmu", "All files", "*"};
            FMUPath = EditorUtility.OpenFilePanelWithFilters("Import FMU", "", filters);
            #endif
        }

        [Button("Import FMU & create signals")]
        void ImportSignals()
        {
            #if UNITY_EDITOR
            ModelDescription = FMUImporter.ImportFMU(FMUPath);
            if (ModelDescription == null)
                Error($"Error Importing FMU {FMUPath}");
            EditorUtility.SetDirty(this);
            if (ModelDescription != null)
                CreateSignals();
#endif
        }

        void CreateSignals()
        {
            List<Signal> newsignals = new List<Signal>();
            var mapping = new SignalMapping();
            int index = 0;
            foreach (var modelvariable in ModelDescription.modelVariables)
            {
                SIGNALTYPE signaltype;
                SIGNALDIRECTION signaldirection;
                signaldirection = SIGNALDIRECTION.INPUT;
                signaltype = SIGNALTYPE.BOOL;
                string parent = "";
                switch (modelvariable.type)
                {
                    case VariableType.Boolean :
                        signaltype = SIGNALTYPE.BOOL;
                        break;
                    case VariableType.Enumeration :
                        Error("Not Implemented");
                        break;
                    case VariableType.Integer :
                        signaltype = SIGNALTYPE.INT;
                        break;
                    case VariableType.Real :
                        signaltype = SIGNALTYPE.REAL;
                        break;
                    case VariableType.String :
                        Error("Not Implemented");
                        break;
                }

                if (modelvariable.causality == "input")
                {
                    signaldirection = SIGNALDIRECTION.INPUT;
                    parent = "Inputs";
                }
                if (modelvariable.causality == "output")
                {
                    signaldirection = SIGNALDIRECTION.OUTPUT;
                    parent = "Outputs";
                }
                if (modelvariable.causality == "parameter")
                {
                    signaldirection = SIGNALDIRECTION.INPUT;
                    parent = "Parameter";
                }
                
                if (modelvariable.causality == "calculatedParameter")
                {
                    signaldirection = SIGNALDIRECTION.OUTPUT;
                    parent = "CalculatedParameter";
                }

                var partrans = gameObject.transform.Find(parent);
                GameObject pargameobj;
                if (partrans == null)
                {
                    pargameobj = new GameObject();
                    pargameobj.transform.parent = this.transform;
                    pargameobj.name = parent;
                }
                else
                {
                    pargameobj = partrans.gameObject;
                }

                mapping.Signal = CreateSignalObject(modelvariable.name, signaltype,signaldirection );
                newsignals.Add(mapping.Signal);
                mapping.Signal.Comment = modelvariable.description;
                mapping.Signal.OriginDataType = modelvariable.type.ToString();
                mapping.Signal.transform.parent = pargameobj.transform;
                mapping.FMUVariabelIndex = index;
                
                if (modelvariable.causality == "input")
                {
                   Inputs.Add( mapping.Signal);
                   mapping.Signal.SetValue(modelvariable.start);
                }
                if (modelvariable.causality == "output")
                {
                    Outputs.Add( mapping.Signal);
                }
                if (modelvariable.causality == "parameter")
                {
                    Parameters.Add( mapping.Signal);
                    mapping.Signal.SetValue(modelvariable.start);
                }
                
                if (modelvariable.causality == "calculatedParameter")
                {
                    Calculated.Add( mapping.Signal);
                }
                index++;
            }
            // Delete old signals
            var allsignals = GetComponentsInChildren<Signal>();
            for (int i = 0; i < allsignals.Length; i++)
            {
                if (!newsignals.Contains(allsignals[i]))
                {
                    DestroyImmediate(allsignals[i].gameObject);
                }
            }
        }
        new void Awake()
        {
            if (InstanceName == "")
                InstanceName = this.name;
            base.Awake();
        }


        void SetFMUSignals(List<Signal> signals)
        {
            foreach (var signal in signals)
            {
                if (signal.GetType() == typeof(PLCInputBool))
                {
                 
                    bool val = (bool)signal.GetValue();
                    fmu.SetBoolean(signal.name,val);
                }
                if (signal.GetType() == typeof(PLCInputInt))
                {
                    int val = (int)signal.GetValue();
                    fmu.SetInteger(signal.name,val);
                }
                if (signal.GetType() == typeof(PLCInputFloat))
                {
                    float val = (float)signal.GetValue();
                    fmu.SetReal(signal.name,val);
                }
            }
        }
        
        void GetFMUSignals(List<Signal> signals)
        {
            foreach (var signal in signals)
            {
                if (signal.GetType() == typeof(PLCOutputBool))
                {
                    bool val = fmu.GetBoolean(signal.name);
                    signal.SetValue(val);
                }
                if (signal.GetType() == typeof(PLCOutputInt))
                {
                    int val = fmu.GetInteger(signal.name);
                    signal.SetValue(val);
                }
                if (signal.GetType() == typeof(PLCOutputFloat))
                {
                    float val = (float)fmu.GetReal(signal.name);
                    signal.SetValue(val);
                }
            }
        }
        
        public override void OpenInterface()
        {
            if (ModelDescription == null)
                return;
            Parameters.RemoveAll(item => item == null);
            Inputs.RemoveAll(item => item == null);
            Outputs.RemoveAll(item => item == null);
            Calculated.RemoveAll(item => item == null);
            fmu = new FMU(ModelDescription, InstanceName, false);
            fmu.SetupExperiment(Time.time);
            SetFMUSignals(Parameters);
            fmu.EnterInitializationMode();
            fmu.ExitInitializationMode();
            GetFMUSignals(Calculated);
            if (fmu != null)
                OnConnected();
        }

        public override void CloseInterface()
        {
         
        }


        // Update is called once per frame
        void FixedUpdate()
        {
            SetFMUSignals(Inputs);
            fmu.DoStep(Time.time, Time.fixedDeltaTime);
            GetFMUSignals(Outputs);
        }

        private void OnDestroy()
        {
            if (fmu != null)
                fmu.Dispose();
        }
    }
}