using System;
using System.Threading;
using PortTraining.Control.Gateway;
using PortTraining.Core.Bootstrap;
using PortTraining.Core.Station;
using UnityEngine;

namespace PortTraining.Control.EmbeddedGateway
{
    public sealed class EmbeddedGatewayClient : MonoBehaviour, IEquipmentGatewayClient
    {
        [SerializeField, Min(10)] private int pollIntervalMilliseconds = 20;

        private readonly object snapshotGate = new object();
        private CancellationTokenSource cancellation;
        private Thread worker;
        private IPlcTransport transport;
        private EquipmentSnapshot latest;
        private long sequence;
        private long deliveredSequence;
        private volatile bool isConnected;

        public bool IsConnected => isConnected;

        public EquipmentSnapshot Latest
        {
            get
            {
                lock (snapshotGate)
                {
                    return latest;
                }
            }
        }

        public event Action<EquipmentSnapshot> SnapshotReceived;

        private void Start()
        {
            Connect();
        }

        private void Update()
        {
            var snapshot = Latest;
            if (snapshot == null || snapshot.sequence == deliveredSequence)
            {
                return;
            }

            deliveredSequence = snapshot.sequence;
            SnapshotReceived?.Invoke(snapshot);
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        public void Connect()
        {
            if (worker != null && worker.IsAlive)
            {
                return;
            }

            cancellation = new CancellationTokenSource();
            transport = CreateTransport();
            worker = new Thread(() => PollLoop(cancellation.Token))
            {
                IsBackground = true,
                Name = "PortTraining.EmbeddedGateway"
            };
            worker.Start();
            Debug.Log("[PortTraining] EmbeddedGateway worker started.");
        }

        public void Disconnect()
        {
            cancellation?.Cancel();
            if (worker != null && worker.IsAlive)
            {
                worker.Join(500);
            }

            transport?.Disconnect();
            isConnected = false;
            worker = null;
            cancellation?.Dispose();
            cancellation = null;
        }

        private void PollLoop(CancellationToken token)
        {
            try
            {
                transport.Connect();
                isConnected = transport.IsConnected;

                while (!token.IsCancellationRequested)
                {
                    var frame = transport.Poll();
                    var snapshot = new EquipmentSnapshot
                    {
                        gatewayOnline = true,
                        plcOnline = frame.PlcOnline,
                        emergencyStopReleased = frame.EmergencyStopReleased,
                        controlPowerOn = frame.ControlPowerOn,
                        handlesCentered = frame.HandlesCentered,
                        controlMode = frame.ControlMode,
                        gantryCommand = frame.GantryCommand,
                        trolleyCommand = frame.TrolleyCommand,
                        hoistCommand = frame.HoistCommand,
                        hornPressed = frame.HornPressed,
                        spreaderLockPressed = frame.SpreaderLockPressed,
                        spreaderUnlockPressed = frame.SpreaderUnlockPressed,
                        sequence = Interlocked.Increment(ref sequence),
                        capturedAt = DateTime.UtcNow.ToString("O")
                    };

                    lock (snapshotGate)
                    {
                        latest = snapshot;
                    }

                    Thread.Sleep(pollIntervalMilliseconds);
                }
            }
            catch (Exception)
            {
                isConnected = false;
                lock (snapshotGate)
                {
                    latest = new EquipmentSnapshot
                    {
                        gatewayOnline = false,
                        plcOnline = false,
                        sequence = Interlocked.Increment(ref sequence),
                        capturedAt = DateTime.UtcNow.ToString("O")
                    };
                }
            }
            finally
            {
                transport?.Disconnect();
                isConnected = false;
            }
        }

        private static IPlcTransport CreateTransport()
        {
            var station = PortTrainingBootstrap.CurrentStation;
            return station?.GatewayTransport == GatewayTransportMode.S7
                ? new S7PlcTransport(station)
                : new SimulatedPlcTransport();
        }
    }
}
