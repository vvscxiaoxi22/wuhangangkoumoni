using PortTraining.Core.Station;

namespace PortTraining.Core.Equipment
{
    public interface IEquipmentModule
    {
        MachineType MachineType { get; }
        bool IsActive { get; }
        void Activate(StationProfile profile);
        void Deactivate();
    }
}

