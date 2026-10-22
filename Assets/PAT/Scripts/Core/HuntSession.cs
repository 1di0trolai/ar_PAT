using System;

namespace Pat.ArLab
{
    public enum HuntPhase { Searching, Scanning, ReadyToCatch, Completed }

    /// <summary>Pure gameplay state: neither the camera adapter nor UI can bypass scan validation.</summary>
    public sealed class HuntSession
    {
        public HuntPhase Phase { get; private set; } = HuntPhase.Searching;
        public string PlantId { get; private set; }
        public string RequestId { get; private set; }
        public float ScanSeconds { get; private set; }
        public float Duration { get; }
        public float Progress => Math.Min(1f, ScanSeconds / Duration);
        public bool Paused { get; private set; }
        public string PauseReason { get; private set; } = "";

        public HuntSession(float duration)
        {
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration))
                throw new ArgumentOutOfRangeException(nameof(duration));
            Duration = duration;
        }

        public bool Begin(string plantId)
        {
            if (Phase != HuntPhase.Searching || string.IsNullOrWhiteSpace(plantId)) return false;
            PlantId = plantId;
            RequestId = Guid.NewGuid().ToString("N");
            ScanSeconds = 0f;
            Phase = HuntPhase.Scanning;
            return true;
        }

        public void Tick(float deltaSeconds, bool valid, string reason)
        {
            if (Phase != HuntPhase.Scanning && Phase != HuntPhase.ReadyToCatch) return;
            Paused = !valid;
            PauseReason = valid ? "" : reason;
            if (Phase != HuntPhase.Scanning || !valid || deltaSeconds <= 0f
                || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
            // A frame stall / app resume must never credit the whole absent interval.
            ScanSeconds = Math.Min(Duration, ScanSeconds + Math.Min(deltaSeconds, 0.1f));
            if (ScanSeconds >= Duration) Phase = HuntPhase.ReadyToCatch;
        }

        public bool CanCatch => Phase == HuntPhase.ReadyToCatch && !Paused;

        public bool Complete()
        {
            if (!CanCatch) return false;
            Phase = HuntPhase.Completed;
            return true;
        }

        public void Reset()
        {
            Phase = HuntPhase.Searching;
            PlantId = null;
            RequestId = null;
            ScanSeconds = 0;
            Paused = false;
            PauseReason = "";
        }
    }
}
