// Game4Automation (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 in2Sight GmbH - Usage of this source code only allowed based on License conditions see https://game4automation.com/lizenz  

using UnityEditor;

namespace game4automation
{
    
    [InitializeOnLoad]
    //! The class is automatically saving the scene when run is started in the Unity editor. It can be turned off by the toggle in the game4automation menu
    public class QuickEditMenuItem
    {
        
        public const string MenuName = "game4automation/Quick Edit Overlay";
        private static bool isToggled;

        static QuickEditMenuItem()
        {
            EditorApplication.delayCall += () =>
            {
                isToggled = EditorPrefs.GetBool(MenuName, true);
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
                Global.QuickEditDisplay = true;
            }
            else
            {
                Global.QuickEditDisplay = false;
            }
        }

    }
}