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

    // Initial melody tracking
    private bool isPlayingInitialMelody = false;
    private int initialMelodyLevel = -1;
    private Coroutine initialMelodyCoroutine;

    [System.Serializable]
    public class UpgradeLevel
    {
        public int threshold = 20; // порог aggression для этого уровня
        
        [Header("Initial Melody (plays once)")]
        public AudioClip initialMelody;
        public TextAsset initialMidiAsset;
        
        [Header("Main Melody (loops)")]
        public AudioClip melody;
        public TextAsset midiAsset;
    }

    void Start()
    {
        musicManager = FindObjectOfType<MusicManager>();
        percussionManager = EnemiesPercussionManager.Instance;
    }

    void OnDestroy()
    {
        applicationQuitting = true;
        if (_instance == this)
            _instance = null;

        // Stop any running coroutines
        if (initialMelodyCoroutine != null)
        {
            StopCoroutine(initialMelodyCoroutine);
            initialMelodyCoroutine = null;
        }
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

        // Check if this level has initial melody
        if (HasInitialMelody(levelIndex))
        {
            // Stop any previous initial melody coroutine
            if (initialMelodyCoroutine != null)
            {
                StopCoroutine(initialMelodyCoroutine);
                initialMelodyCoroutine = null;
            }

            isPlayingInitialMelody = true;
            initialMelodyLevel = levelIndex;
            initialMelodyCoroutine = StartCoroutine(SwitchToMainMelodyAfterCycle());
        }
    }

    private void RevertLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= aggressionLevels.Count) return;

        // No animator

        // If reverting the level that was playing initial melody, stop it
        if (isPlayingInitialMelody && initialMelodyLevel == levelIndex)
        {
            if (initialMelodyCoroutine != null)
            {
                StopCoroutine(initialMelodyCoroutine);
                initialMelodyCoroutine = null;
            }
            isPlayingInitialMelody = false;
            initialMelodyLevel = -1;
        }
    }

    private IEnumerator SwitchToMainMelodyAfterCycle()
    {
        if (musicManager == null)
        {
            isPlayingInitialMelody = false;
            initialMelodyLevel = -1;
            yield break;
        }

        // Wait for one music cycle to complete
        yield return new WaitForSeconds(musicManager.interval);

        // Switch to main melody
        isPlayingInitialMelody = false;
        initialMelodyLevel = -1;
        initialMelodyCoroutine = null;

        ScheduleSync();
    }

    private bool HasInitialMelody(int level)
    {
        if (level < 0 || level >= aggressionLevels.Count) return false;
        return aggressionLevels[level].initialMelody != null;
    }

    private AudioClip GetCurrentAudioClipForAppliedState()
    {
        // If playing initial melody for a level, return it
        if (isPlayingInitialMelody && initialMelodyLevel >= 0 && initialMelodyLevel < appliedLevels)
        {
            var initialClip = aggressionLevels[initialMelodyLevel].initialMelody;
            if (initialClip != null) return initialClip;
        }

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
        // If playing initial melody for a level, return its MIDI
        if (isPlayingInitialMelody && initialMelodyLevel >= 0 && initialMelodyLevel < appliedLevels)
        {
            var initialMidi = aggressionLevels[initialMelodyLevel].initialMidiAsset;
            if (initialMidi != null) return initialMidi;
        }

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
    }
}