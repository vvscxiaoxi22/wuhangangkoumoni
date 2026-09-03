using PortTraining.Core.Station;
using UnityEngine;

namespace PortTraining.Core.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    public sealed class PortTrainingBootstrap : MonoBehaviour
    {
        public static StationProfile CurrentStation { get; private set; }
        public static bool IsReady => CurrentStation != null;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            try
            {
                CurrentStation = StationProfileLoader.Load();
                Debug.Log(
                    $"[PortTraining] Station ready: {CurrentStation.StationId}, " +
                    $"{CurrentStation.MachineType}, gateway={CurrentStation.GatewayEndpoint}");
            }
            catch (System.Exception exception)
            {
                CurrentStation = null;
                Debug.LogError($"[PortTraining] Startup blocked: {exception.Message}");
            }
        }
    }
}

