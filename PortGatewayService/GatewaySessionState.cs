using System;
using PortTraining.PortGatewayService.Protocol;

namespace PortTraining.PortGatewayService
{
    // Lease state for exactly one station, shared between GatewayControlPipeServer
    // (issues/releases leases via gateway.session.prepare/lease.release) and
    // GatewayRealtimePipeServer (validates a scene's protocol.hello against whatever
    // lease is currently Reserved). V1.1 section 11: "在Gateway单一临界区内重新读取
    // 设备帧..." - SyncRoot is that single critical section; callers must hold it
    // (lock (state.SyncRoot) { ... }) before calling any *NoLock member.
    public sealed class GatewaySessionState
    {
        private readonly object syncRoot = new object();
        private ActiveLeaseRecord currentLease;
        private ulong leaseEpochCounter;

        public object SyncRoot => syncRoot;

        public ActiveLeaseRecord CurrentLeaseNoLock => currentLease;

        public void ExpireIfStaleNoLock(Action<string> log)
        {
            if (currentLease != null && Environment.TickCount64 >= currentLease.MonotonicExpiryTicks)
            {
                log?.Invoke(
                    $"[PGS_LEASE_EXPIRED] leaseId={currentLease.Lease.LeaseId}, " +
                    $"operationRunId={currentLease.Lease.OperationRunId}");
                currentLease = null;
            }
        }

        public GatewaySessionLease IssueNoLock(
            string operationRunId,
            string stationId,
            string sceneInstanceId,
            string controlMode,
            string sessionTokenHash,
            bool isSimulated,
            long requestedLeaseDurationMs)
        {
            leaseEpochCounter++;
            var now = DateTimeOffset.UtcNow;
            var lease = new GatewaySessionLease
            {
                LeaseId = Guid.NewGuid().ToString(),
                LeaseEpoch = leaseEpochCounter,
                OperationRunId = operationRunId,
                StationId = stationId,
                SceneInstanceId = sceneInstanceId,
                ControlMode = controlMode,
                PreflightInputSequence = 0,
                IssuedAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(now),
                ExpiresAtUtc = EnvelopeSerializer.FormatOccurredAtUtc(now.AddMilliseconds(requestedLeaseDurationMs)),
                Status = "Reserved",
                IsSimulated = isSimulated
            };

            currentLease = new ActiveLeaseRecord
            {
                Lease = lease,
                SessionTokenHash = sessionTokenHash,
                MonotonicExpiryTicks = Environment.TickCount64 + requestedLeaseDurationMs
            };

            return lease;
        }

        public bool TryReleaseNoLock(string operationRunId, out string releasedLeaseId)
        {
            if (currentLease != null &&
                string.Equals(currentLease.Lease.OperationRunId, operationRunId, StringComparison.Ordinal))
            {
                releasedLeaseId = currentLease.Lease.LeaseId;
                currentLease = null;
                return true;
            }

            releasedLeaseId = null;
            return false;
        }

        public sealed class ActiveLeaseRecord
        {
            public GatewaySessionLease Lease;
            public string SessionTokenHash;
            public long MonotonicExpiryTicks;

            // V1.1 section 3.2/10.1: "新生成的streamId；每次Named Pipe重连都必须改变".
            // Null until the first realtime protocol.hello for this lease succeeds.
            public string LastAcceptedStreamId;
        }
    }
}
