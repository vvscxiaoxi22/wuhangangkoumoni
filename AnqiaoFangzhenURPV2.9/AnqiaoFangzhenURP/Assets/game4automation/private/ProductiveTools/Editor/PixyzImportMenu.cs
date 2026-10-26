

using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace game4automation
{
    
    [InitializeOnLoad]
    //! The class is automatically saving the scene when run is started in the Unity editor. It can be turned off by the toggle in the game4automation menu
    public class PixyzImportMenu
    {
        
        public const string MenuName = "game4automation/Enable PiXYZ Import (Pro, Pixyz 2020.1.0.25)";
        private static bool isToggled;

        static PixyzImportMenu()
        {
            EditorApplication.delayCall += () =>
            {
                isToggled = EditorPrefs.GetBool(MenuName, false);
                UnityEditor.Menu.SetChecked(MenuName, isToggled);
                SetMode();
            };
        }

        [MenuItem(MenuName, false, 500)]
        private static void ToggleMode()
        {
            isToggled = !isToggled;
            UnityEditor.Menu.SetChecked(MenuName, isToggled);
            EditorPrefs.SetBool(MenuName, isToggled);
            SetMode();
        }

        private static void SetMode()
        {
           
            if (isToggled)
            {
    
                var pixyzpath = Application.dataPath+"/Plugins/PiXYZ";
                if (!Directory.Exists(pixyzpath))
               {
                   EditorUtility.DisplayDialog("Error",
                     "PiXYZ 2020 is not available in standard folder Plugins/PiXYZ, PiXYZ importer is only available for Game4Automation Professional and requires an additional PiXYZ license", "OK");
               }
               else
               {
                   Global.SetDefine("GAME4AUTOMATION_PIXYZ");
                   Global.SetAssemblyDefReference("game4automation/game4automation.base.asmdef","Pixyz.PluginUnity",true);
               }
            }
            else
            {
                Global.DeleteDefine("GAME4AUTOMATION_PIXYZ");
                Global.SetAssemblyDefReference("game4automation/game4automation.base.asmdef","Pixyz.PluginUnity",false);
            }
        }

       
    }
}