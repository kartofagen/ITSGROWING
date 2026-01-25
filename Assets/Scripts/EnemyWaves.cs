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
        public AudioClip melody;
        public TextAsset midiAsset;
    }

    void Start()
    {
        musicManager = FindObjectOfType<MusicManager>();
        percussionManager = EnemiesPercussionManager.Instance;
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

        for (int level = appliedLevels; level < shouldBeApplied; level++)
            ApplyLevel(level);

        for (int level = appliedLevels - 1; level >= shouldBeApplied; level--)
            RevertLevel(level);

        appliedLevels = shouldBeApplied;

        // Schedule the sync after state changes
        ScheduleSync();
    }

    private void ApplyLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= aggressionLevels.Count) return;

        // No animator for EnemyWaves, but can add if needed
    }

    private void RevertLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= aggressionLevels.Count) return;

        // No animator
    }

    private AudioClip GetCurrentAudioClipForAppliedState()
    {
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
    }

    void OnDestroy()
    {
        applicationQuitting = true;
        if (_instance == this)
            _instance = null;
    }
}