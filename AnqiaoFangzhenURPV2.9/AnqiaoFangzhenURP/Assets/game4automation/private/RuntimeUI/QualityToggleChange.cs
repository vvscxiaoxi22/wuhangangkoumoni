using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace game4automation
{
    public class QualityToggleChange : MonoBehaviour
    {

        public SettingsController settingscontroller;
        private Toggle toggle;
        public int qualitylevel;
        // Start is called before the first frame update

        void Awake()
        {
            toggle = GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(OnQualityToggleChanged);
        }

        public void SetQualityStatus(int quality)
        {
            if (quality == qualitylevel)
                toggle.isOn = true;
        }

        public void OnQualityToggleChanged(bool ison)
        {
            if (ison)
                settingscontroller.OnQualityToggleChanged(qualitylevel);
        }
        

    }
}
    
