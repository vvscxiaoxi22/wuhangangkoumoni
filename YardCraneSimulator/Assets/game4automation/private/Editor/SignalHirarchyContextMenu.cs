using System.Reflection;
using game4automation;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SignalHierarchyContextMenu
{
  

 
    private  static void ChangeDirection(GameObject gameobjec)
    {
        var signal = gameobjec.GetComponent<Signal>();
        if (signal == null)
            return;
        Signal newsignal = signal;

        var type = signal.GetType();
        if (signal.IsInput())
        {
            if (type == typeof(game4automation.PLCInputBool))
            {
                 newsignal = gameobjec.AddComponent<PLCOutputBool>();
            }
            if (type == typeof(game4automation.PLCInputInt))
            {
                newsignal = gameobjec.AddComponent<PLCOutputInt>();
            }
            if (type == typeof(game4automation.PLCInputFloat))
            {
                newsignal = gameobjec.AddComponent<PLCOutputFloat>();
            }
        }
        else
        {
            if (type == typeof(game4automation.PLCOutputBool))
            {
                newsignal = gameobjec.AddComponent<PLCInputBool>();
            }
            if (type == typeof(game4automation.PLCOutputInt))
            {
                newsignal = gameobjec.AddComponent<PLCInputInt>();
            }
            if (type == typeof(game4automation.PLCOutputFloat))
            {
                newsignal = gameobjec.AddComponent<PLCInputFloat>();
            }

        }
        
        newsignal.Name = signal.Name;
        newsignal.Comment = signal.Comment;
        newsignal.OriginDataType = signal.OriginDataType;
        Object.DestroyImmediate(signal);
    }
    
  
    
    [MenuItem("GameObject/Game4Automation/Change Signal Direction",false,0)]
    public static void HierarchyChangeSignalDirection()
    {
        foreach (var obj in Selection.objects)
        {
            var gameobject = (GameObject) obj;
            ChangeDirection(gameobject);
        }
     

    }
   
    [MenuItem("CONTEXT/Component/Game4Automation/Change Signal Direction")]
    public static void ComtextChangeSignalDirection(MenuCommand command)
    {
        var gameobject =  command.context;
        var obj = (Component)gameobject;
        ChangeDirection(obj.gameObject);

    }
 
}