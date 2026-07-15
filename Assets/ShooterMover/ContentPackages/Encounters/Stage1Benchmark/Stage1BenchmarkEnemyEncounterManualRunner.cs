using UnityEngine;

namespace ShooterMover.ContentPackages.Encounters.Stage1Benchmark
{
    /// <summary>
    /// Play-mode-only convenience component for the EN-010 manual selector proof.
    /// Add it to any loaded object after EH-004 has loaded the benchmark arena;
    /// it attaches the package-owned loader at runtime and never saves a scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stage1BenchmarkEnemyEncounterManualRunner : MonoBehaviour
    {
        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            Stage1BenchmarkEnemyEncounterArenaLoader.AttachToLoadedArena();
            enabled = false;
        }
    }
}
