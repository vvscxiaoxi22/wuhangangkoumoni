using PortTraining.Core.Bootstrap;
using PortTraining.Core.Equipment;
using PortTraining.Core.Station;
using UnityEngine;

namespace PortTraining.Equipment.QC
{
    [DefaultExecutionOrder(-900)]
    public sealed class QcEquipmentModule : MonoBehaviour, IEquipmentModule
    {
        public MachineType MachineType => MachineType.QC;
        public bool IsActive { get; private set; }

        private void Awake()
        {
            if (!PortTrainingBootstrap.IsReady || PortTrainingBootstrap.CurrentStation.MachineType != MachineType.QC)
            {
                Deactivate();
                return;
            }

            Activate(PortTrainingBootstrap.CurrentStation);
        }

        public void Activate(StationProfile profile)
        {
            IsActive = true;
            gameObject.SetActive(true);
            Debug.Log($"[PortTraining] QC module active for {profile.StationId}.");
        }

        public void Deactivate()
        {
            IsActive = false;
            gameObject.SetActive(false);
        }
    }
}

