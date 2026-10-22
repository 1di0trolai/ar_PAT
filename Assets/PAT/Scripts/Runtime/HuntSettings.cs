using UnityEngine;

namespace Pat.ArLab
{
    [CreateAssetMenu(menuName = "PAT/AR Hunt Settings")]
    public sealed class HuntSettings : ScriptableObject
    {
        [Header("Scanning (seconds)")]
        [Min(1)] public float scanDuration = 10f;
        [Min(0.1f)] public float recognitionHold = 0.8f;
        [Min(1)] public float trackingLossReset = 12f;

        [Header("Distances (meters)")]
        [Min(0.1f)] public float minimumCameraDistance = 0.25f;
        [Min(0.5f)] public float maximumCameraDistance = 3f;
        [Min(0.1f)] public float spawnRadiusMinimum = 0.20f;
        [Min(0.1f)] public float spawnRadiusMaximum = 0.45f;
        [Min(0)] public float spawnHeight = 0.20f;
        public Vector3 plantCenterOffset = Vector3.zero;

        [Header("Lab")]
        public bool practiceByDefault = true;
        public bool showDebugByDefault;

        private void OnValidate()
        {
            maximumCameraDistance = Mathf.Max(minimumCameraDistance + 0.1f, maximumCameraDistance);
            spawnRadiusMaximum = Mathf.Max(spawnRadiusMinimum, spawnRadiusMaximum);
        }
    }
}
