using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyWaves : MonoBehaviour
{
    private static EnemyWaves _instance;
    private static bool applicationQuitting = false;

    public static EnemyWaves Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindObjectOfType<EnemyWaves>();
            }

            if (_instance == null && Application.isPlaying && !applicationQuitting)
            {
                var go = new GameObject("EnemyWaves");
                _instance = go.AddComponent<EnemyWaves>();
            }

            return _instance;
        }
    }

    [Header("Aggression Tree (levels ascending)")]
    public List<UpgradeLevel> aggressionLevels = new List<UpgradeLevel>();

    public int KilledEnemies { get; private set; } = 0;

    private MusicManager musicManager;
    private EnemiesPercussionManager percussionManager;

    private int aggression = 0;
    private int appliedLevels = 0; // сколько уровней уже применено (0..N)

    [System.Serializable]
    public class UpgradeLevel
    {
        public int threshold = 20; // порог aggression для этого уровня
        
        [Header("Main Melody (loops)")]
        public AudioClip melody;
        public TextAsset midiAsset;

        [Header("Spawn rate")]
        [Tooltip("Коэффициент уменьшения spawnRate: spawnRate = initialSpawnRate / (1 + killed * spawnRateCoeff)")]
        public float spawnRateCoeff = 0.1f;
    }

    void Start()
    {
        musicManager = FindObjectOfType<MusicManager>();
        percussionManager = EnemiesPercussionManager.Instance;

        CheckAggressionStates();
    }

    void OnDestroy()
    {
        applicationQuitting = true;
        if (_instance == this)
            _instance = null;
    }

    public void SetAggression(int newAggression)
    {
        aggression = newAggression;
        CheckAggressionStates();
    }

    public void IncrementKilledEnemies()
    {
        KilledEnemies++;
        EnemiesPercussionManager.Instance?.UpdateSpawnRate();
    }

    private void CheckAggressionStates()
    {
        int shouldBeApplied = 0;
        for (int i = 0; i < aggressionLevels.Count; i++)
        {
            if (aggression >= aggressionLevels[i].threshold)
                shouldBeApplied = i + 1;
            else
                break;
        }

        appliedLevels = shouldBeApplied;

        // Schedule the sync after state changes
        ScheduleSync();
    }
    
    private float GetCurrentSpawnRateCoeffForAppliedState()
    {
        for (int i = appliedLevels - 1; i >= 0; i--)
        {
            return aggressionLevels[i].spawnRateCoeff;
        }

        return percussionManager != null ? percussionManager.GetSpawnRateCoeff() : 0.1f;
    }

    private AudioClip GetCurrentAudioClipForAppliedState()
    {
        // Otherwise return normal clip for highest applied level
        for (int i = appliedLevels - 1; i >= 0; i--)
        {
            var clip = aggressionLevels[i].melody;
            if (clip != null) return clip;
        }

        // best-effort: try to get clip from musicManager
        if (musicManager != null && percussionManager != null)
        {
            foreach (var obj in musicManager.midiControlledObjects)
            {
                if (obj.script == percussionManager)
                {
                    return obj.audioClip;
                }
            }
        }

        return null;
    }

    private TextAsset GetCurrentMidiForAppliedState()
    {
        // Otherwise return normal MIDI for highest applied level
        for (int i = appliedLevels - 1; i >= 0; i--)
        {
            var midi = aggressionLevels[i].midiAsset;
            if (midi != null) return midi;
        }

        // best-effort: try to get midi from percussionManager
        return percussionManager?.midiFile;
    }

    private void ScheduleSync()
    {
        if (musicManager == null || percussionManager == null) return;

        AudioClip newClip = GetCurrentAudioClipForAppliedState();
        TextAsset newMidi = GetCurrentMidiForAppliedState();

        musicManager.ScheduleUpdateForInstrument(percussionManager, newClip, newMidi);

        float coeff = GetCurrentSpawnRateCoeffForAppliedState();
        percussionManager.SetSpawnRateCoeff(coeff);
    }
}