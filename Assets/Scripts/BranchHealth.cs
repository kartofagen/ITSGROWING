using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

[RequireComponent(typeof(Collider))]
public class BranchHealth : MonoBehaviour
{
    [Header("Identity")]
    public string branchName = "Lead";
    [Tooltip("Перетащите сюда компонент Lead/Bass/EatSounds с этого GameObject (тот, что используется в MusicManager).")]
    public MonoBehaviour instrumentScript;

    [Header("Health")]
    public int initialHealth = 10;
    public int currentHealth;
    public int maxHealth = 9999;
    public int damageFromEnemy = 1;

    [Header("Upgrade Tree (levels ascending)")]
    public List<UpgradeLevel> upgradeLevels = new List<UpgradeLevel>();

    [Header("Animation / Audio")]
    public Animator animator;
    public string animatorLevelIntParam = "UpgradeLevel";
    public string animatorUpgradeTrigger = "OnUpgrade";
    public string animatorDowngradeTrigger = "OnDowngrade";

    [Header("Instrument MIDI Behaviour")]
    [Tooltip("Если true — при смене апгрейда BranchHealth попытается установить связанный midiAsset в поле 'midiFile' у instrumentScript (если такое поле/свойство найдено), и затем вызвать RestartMidiProcessing() если такой метод есть.")]
    public bool updateInstrumentMidiFile = true;

    private HashSet<GameObject> enemiesInside = new HashSet<GameObject>();
    private MusicManager musicManager;
    private int appliedLevels = 0; // сколько уровней уже применено (0..N)
    private List<int> selectedOptionPerLevel; // индекс выбранной опции для каждого уровня, -1 если не выбрано

    // Initial melody tracking
    private bool isPlayingInitialMelody = false;
    private int initialMelodyLevel = -1;
    private Coroutine initialMelodyCoroutine;

    [System.Serializable]
    public class UpgradeLevel
    {
        public int threshold = 20; // порог здоровья для этого уровня
        public List<UpgradeOption> options = new List<UpgradeOption>();
    }

    [System.Serializable]
    public class UpgradeOption
    {
        public string optionName = "Default";
        
        [Header("Initial Melody (plays once)")]
        public AudioClip initialMelody;
        public TextAsset initialMidiAsset;
        
        [Header("Main Melody (loops)")]
        public AudioClip melody;
        public TextAsset midiAsset;
        
        public int stateId = 0;
    }

    void Awake()
    {
        currentHealth = initialHealth;
        selectedOptionPerLevel = new List<int>(new int[upgradeLevels.Count]);
        for (int i = 0; i < selectedOptionPerLevel.Count; i++) selectedOptionPerLevel[i] = -1;
    }

    void Start()
    {
        if (instrumentScript == null)
        {
            Debug.LogWarning($"BranchHealth ({branchName}): instrumentScript is not assigned. Please assign the Lead/Bass/EatSounds component.");
        }

        // register in manager (instance creation now safe)
        BranchHealthManager.Instance?.RegisterBranch(this);

        musicManager = FindObjectOfType<MusicManager>();

        if (animator == null)
            animator = GetComponent<Animator>();

        // initial sync - for start, we can do immediate, but to be consistent, schedule
        ScheduleSync();
        
        CheckUpgradeStates();
    }

    void OnDestroy()
    {
        BranchHealthManager.Instance?.UnregisterBranch(this);
        
        // Stop any running coroutines
        if (initialMelodyCoroutine != null)
        {
            StopCoroutine(initialMelodyCoroutine);
            initialMelodyCoroutine = null;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            ApplyDamage(damageFromEnemy);
        }
    }

    public void ApplyDamage(int amount)
    {
        if (amount <= 0) return;
        
        if (instrumentScript is Bass bass)
        {
            amount = bass.TakeDamage(amount);
        }
        
        currentHealth = Mathf.Max(0, currentHealth - amount);
        CheckUpgradeStates();
        BranchHealthManager.Instance?.UpdateTotalHealth();
    }


    public void AddHealth(int amount)
    {
        if (amount <= 0) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        CheckUpgradeStates();
        BranchHealthManager.Instance?.UpdateTotalHealth();
    }

    private void CheckUpgradeStates()
    {
        int shouldBeApplied = 0;
        for (int i = 0; i < upgradeLevels.Count; i++)
        {
            if (currentHealth >= upgradeLevels[i].threshold)
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
        if (levelIndex < 0 || levelIndex >= upgradeLevels.Count) return;

        int sel = selectedOptionPerLevel[levelIndex];
        if (sel < 0)
        {
            sel = 0; // default to first option if not selected
            selectedOptionPerLevel[levelIndex] = sel;
        }

        if (animator != null)
        {
            animator.SetTrigger(animatorUpgradeTrigger);
            animator.SetInteger(animatorLevelIntParam, levelIndex + 1);
        }

        // Notify instrument scripts about level change
        if (instrumentScript != null)
        {
            if (instrumentScript is Bass bass)
            {
                bass.OnUpgradeLevelChanged(levelIndex);
            }
            else if (instrumentScript is EatSounds eatSounds)
            {
                eatSounds.OnUpgradeLevelChanged(levelIndex);
            }
        }

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
        if (levelIndex < 0 || levelIndex >= upgradeLevels.Count) return;

        if (animator != null)
        {
            animator.SetTrigger(animatorDowngradeTrigger);
            animator.SetInteger(animatorLevelIntParam, levelIndex);
        }

        // ВНИМАНИЕ: levelIndex - это индекс уровня, который ОТКАТЫВАЕТСЯ
        // После отката этого уровня, текущий уровень будет levelIndex (не levelIndex+1)
        // Например, если откатываем уровень 2, то новый уровень будет 1
        // Но нам нужно передать НОВЫЙ уровень, поэтому levelIndex
    
        if (instrumentScript != null)
        {
            if (instrumentScript is Bass bass)
            {
                // Передаем новый уровень (levelIndex уже указывает на уровень после отката)
                bass.OnUpgradeLevelChanged(levelIndex);
            }
            else if (instrumentScript is EatSounds eatSounds)
            {
                eatSounds.OnUpgradeLevelChanged(levelIndex);
            }
        }

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
        int sel = selectedOptionPerLevel[level];
        return HasInitialMelody(level, sel);
    }

    private bool HasInitialMelody(int level, int option)
    {
        if (level < 0 || level >= upgradeLevels.Count) return false;
        var lvl = upgradeLevels[level];
        if (option < 0 || option >= lvl.options.Count) return false;
        return lvl.options[option].initialMelody != null;
    }

    private AudioClip GetAudioClipForLevelOption(int level, int option)
    {
        if (level < 0 || level >= upgradeLevels.Count) return null;
        var lvl = upgradeLevels[level];
        if (option < 0 || option >= lvl.options.Count) return null;
        return lvl.options[option].melody;
    }

    private TextAsset GetMidiForLevelOption(int level, int option)
    {
        if (level < 0 || level >= upgradeLevels.Count) return null;
        var lvl = upgradeLevels[level];
        if (option < 0 || option >= lvl.options.Count) return null;
        return lvl.options[option].midiAsset;
    }

    private AudioClip GetInitialAudioClipForLevelOption(int level, int option)
    {
        if (level < 0 || level >= upgradeLevels.Count) return null;
        var lvl = upgradeLevels[level];
        if (option < 0 || option >= lvl.options.Count) return null;
        return lvl.options[option].initialMelody;
    }

    private TextAsset GetInitialMidiForLevelOption(int level, int option)
    {
        if (level < 0 || level >= upgradeLevels.Count) return null;
        var lvl = upgradeLevels[level];
        if (option < 0 || option >= lvl.options.Count) return null;
        return lvl.options[option].initialMidiAsset;
    }

    private AudioClip GetCurrentAudioClipForAppliedState()
    {
        // If playing initial melody for a level, return it
        if (isPlayingInitialMelody && initialMelodyLevel >= 0 && initialMelodyLevel < appliedLevels)
        {
            int sel = selectedOptionPerLevel[initialMelodyLevel];
            var initialClip = GetInitialAudioClipForLevelOption(initialMelodyLevel, sel);
            if (initialClip != null) return initialClip;
        }

        // Otherwise return normal clip for highest applied level
        for (int i = appliedLevels - 1; i >= 0; i--)
        {
            int sel = selectedOptionPerLevel[i];
            var clip = GetAudioClipForLevelOption(i, sel);
            if (clip != null) return clip;
        }

        // best-effort: try to get clip from musicManager
        if (musicManager != null && instrumentScript != null)
        {
            foreach (var obj in musicManager.midiControlledObjects)
            {
                if (obj.script == instrumentScript)
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
            int sel = selectedOptionPerLevel[initialMelodyLevel];
            var initialMidi = GetInitialMidiForLevelOption(initialMelodyLevel, sel);
            if (initialMidi != null) return initialMidi;
        }

        // Otherwise return normal MIDI for highest applied level
        for (int i = appliedLevels - 1; i >= 0; i--)
        {
            int sel = selectedOptionPerLevel[i];
            var midi = GetMidiForLevelOption(i, sel);
            if (midi != null) return midi;
        }

        // best-effort: try to get midi from instrumentScript if it exposes midiFile field
        if (instrumentScript != null)
        {
            var t = instrumentScript.GetType();
            // try field
            var fld = t.GetField("midiFile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (fld != null && fld.FieldType == typeof(TextAsset))
            {
                return fld.GetValue(instrumentScript) as TextAsset;
            }
        }

        return null;
    }

    private void ScheduleSync()
    {
        if (musicManager == null || instrumentScript == null) return;

        AudioClip newClip = GetCurrentAudioClipForAppliedState();
        TextAsset newMidi = updateInstrumentMidiFile ? GetCurrentMidiForAppliedState() : null;

        musicManager.ScheduleUpdateForInstrument(instrumentScript, newClip, newMidi);
    }
}