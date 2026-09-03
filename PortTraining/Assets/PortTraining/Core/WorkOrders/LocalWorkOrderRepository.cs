using System;
using System.Collections.Generic;
using System.IO;
using PortTraining.Core.Station;
using UnityEngine;

namespace PortTraining.Core.WorkOrders
{
    public sealed class LocalWorkOrderRepository
    {
        private const string RelativePath = "WorkOrders/work-orders.json";

        public IReadOnlyList<WorkOrderSnapshot> LoadForStation(StationProfile station)
        {
            var result = new List<WorkOrderSnapshot>();
            if (station == null)
            {
                return result;
            }

            var path = Path.Combine(Application.streamingAssetsPath, RelativePath);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[PortTraining] Work order seed file was not found: {path}");
                return result;
            }

            try
            {
                var collection = JsonUtility.FromJson<WorkOrderCollection>(File.ReadAllText(path));
                foreach (var workOrder in collection?.workOrders ?? Array.Empty<WorkOrderSnapshot>())
                {
                    var machineMatches = string.Equals(workOrder.machineType, station.MachineType.ToString(), StringComparison.OrdinalIgnoreCase);
                    var stationMatches = string.IsNullOrWhiteSpace(workOrder.stationId) ||
                                         string.Equals(workOrder.stationId, station.StationId, StringComparison.OrdinalIgnoreCase);
                    if (machineMatches && stationMatches)
                    {
                        result.Add(workOrder);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PortTraining] Work order seed file is invalid: {exception.Message}");
            }

            return result;
        }
    }
}

