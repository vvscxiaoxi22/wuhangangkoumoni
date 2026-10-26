using UnityEngine;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    /// <summary>
    /// Scene-authored marker for one yard container position (world pose plus which
    /// YardCraneContainer instance currently rests there, if any). The slot itself carries no
    /// collider or Rigidbody - occupancy is bookkeeping only, decided by the rig's pick/place
    /// logic, not sensed by physics. Runtime occupancy always starts from InitialOccupant on
    /// Awake and is never persisted across scene loads, matching this project's existing
    /// per-run (not saved) training/session state convention.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class YardCraneYardSlot : MonoBehaviour
    {
        [SerializeField] private string slotId = string.Empty;
        [SerializeField] private YardCraneContainerType acceptedContainerType = YardCraneContainerType.Ft40;
        [SerializeField] private YardCraneContainer initialOccupant;

        private YardCraneContainer currentOccupant;

        public string SlotId => slotId;
        public YardCraneContainerType AcceptedContainerType => acceptedContainerType;
        public YardCraneContainer InitialOccupant => initialOccupant;
        public bool IsOccupied => currentOccupant != null;
        public YardCraneContainer CurrentOccupant => currentOccupant;

        private void Awake()
        {
            currentOccupant = initialOccupant;
            if (initialOccupant != null && initialOccupant.ContainerType != acceptedContainerType)
            {
                Debug.LogError(
                    $"[YC_YARD_SLOT_INITIAL_OCCUPANT_TYPE_MISMATCH] slot={slotId}, " +
                    $"accepted={acceptedContainerType}, occupant={initialOccupant.ContainerType}, " +
                    $"container={initialOccupant.ContainerId}",
                    this);
            }
        }

        public bool CanAccept(YardCraneContainer container)
        {
            return container != null &&
                   container.ContainerType == acceptedContainerType &&
                   (currentOccupant == null || currentOccupant == container);
        }

        public void SetOccupant(YardCraneContainer container)
        {
            if (!CanAccept(container))
            {
                Debug.LogWarning(
                    $"[YC_YARD_SLOT_OCCUPANCY_REJECTED] slot={slotId}, " +
                    $"accepted={acceptedContainerType}, occupied={IsOccupied}, " +
                    $"container={(container != null ? container.ContainerId : "<null>")}",
                    this);
                return;
            }

            currentOccupant = container;
        }

        public void Clear()
        {
            currentOccupant = null;
        }
    }
}
