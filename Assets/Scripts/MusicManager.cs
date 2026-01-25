using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

public class MusicManager : MonoBehaviour
{
    [SerializeField] private float interval;
    [SerializeField] private float audioDelay = 0f;
    
    [System.Serializable]
    public class MidiControlledObject
    {
        public MonoBehaviour script;
        public AudioClip audioClip;
        public bool enabled = true;
        public AudioSource sourceA;
        public AudioSource sourceB;
        [HideInInspector] public bool useSourceA = true;
    }
    
    public List<MidiControlledObject> midiControlledObjects = new List<MidiControlledObject>();
    public bool autoStart = true;
    
    private float lastStartTime = 0f;
    private bool running = false;
    private bool isQuitting = false;
    private bool firstCycle = true;
    private bool waitingForAudioStart = false;
    private float audioStartTime = 0f;

    private List<PendingUpdate> pendingUpdates = new List<PendingUpdate>();

    private class PendingUpdate
    {
        public MonoBehaviour script;
        public AudioClip clip;
        public TextAsset midi;
    }

    void Start()
    {
        foreach (var obj in midiControlledObjects)
        {
            if (obj.script == null || obj.audioClip == null) continue;
            
            obj.sourceA.clip = obj.audioClip;
            obj.sourceA.playOnAwake = false;
            obj.sourceA.loop = false;
            
            obj.sourceB.clip = obj.audioClip;
            obj.sourceB.playOnAwake = false;
            obj.sourceB.loop = false;

            // Preload initial clips
            if (obj.sourceA.clip != null)
            {
                obj.sourceA.Play();
                obj.sourceA.Stop();
            }
            if (obj.sourceB.clip != null)
            {
                obj.sourceB.Play();
                obj.sourceB.Stop();
            }
        }
        
        if (autoStart)
            StartLoop();
    }

    void OnEnable()
    {
        lastStartTime = 0f;
        firstCycle = true;
        waitingForAudioStart = false;
    }

    public void StartLoop()
    {
        if (isQuitting) return;
        
        running = true;
        lastStartTime = Time.time;
        firstCycle = true;
        
        if (audioDelay > 0f)
        {
            waitingForAudioStart = true;
            audioStartTime = Time.time + audioDelay;
        }
        else
        {
            PlayCurrentSources();
        }
    }

    public void StopLoop()
    {
        running = false;
        waitingForAudioStart = false;
        
        foreach (var obj in midiControlledObjects)
        {
            if (obj.sourceA != null) obj.sourceA.Stop();
            if (obj.sourceB != null) obj.sourceB.Stop();
        }
    }

    void Update()
    {
        if (!running || isQuitting)
            return;

        float now = Time.time;
        
        if (waitingForAudioStart && now >= audioStartTime)
        {
            PlayCurrentSources();
            waitingForAudioStart = false;
        }
        
        float elapsed = now - lastStartTime;
        
        if (firstCycle && elapsed > Time.deltaTime)
        {
            firstCycle = false;
            NotifyAll();
        }
        
        if (elapsed >= interval - 1e-3f)
        {
            ApplyPendingUpdates();
            NotifyAll();
            
            if (audioDelay > 0f)
            {
                waitingForAudioStart = true;
                audioStartTime = now + audioDelay;
            }
            else
            {
                PlayCurrentSources();
            }
            
            lastStartTime = now;
        }
    }

    private void ApplyPendingUpdates()
    {
        foreach (var pending in pendingUpdates)
        {
            foreach (var obj in midiControlledObjects)
            {
                if (obj.script == pending.script)
                {
                    // Update audio clip on the next source
                    if (pending.clip != null)
                    {
                        obj.audioClip = pending.clip;
                        AudioSource nextSource = obj.useSourceA ? obj.sourceB : obj.sourceA;
                        nextSource.clip = pending.clip;

                        // Preload the new clip
                        if (nextSource.clip != null)
                        {
                            nextSource.Play();
                            nextSource.Stop();
                        }
                    }

                    // Update MIDI if provided
                    if (pending.midi != null)
                    {
                        TrySetMidiField(obj.script, pending.midi);
                    }

                    break;
                }
            }
        }

        pendingUpdates.Clear();
    }

    private void PlayCurrentSources()
    {
        foreach (var obj in midiControlledObjects)
        {
            if (!obj.enabled || obj.script == null) continue;
            
            // Flip first
            obj.useSourceA = !obj.useSourceA;
            
            AudioSource currentSource = obj.useSourceA ? obj.sourceA : obj.sourceB;
            
            if (currentSource != null)
            {
                currentSource.Play();
            }
        }
    }

    private void NotifyAll()
    {
        if (isQuitting) return;
        
        foreach (var obj in midiControlledObjects)
        {
            if (!obj.enabled || obj.script == null) continue;
            
            try
            {
                if (obj.script is Lead lead)
                {
                    lead.RestartMidiProcessing();
                }
                else if (obj.script is EnemiesPercussionManager enemiesManager)
                {
                    enemiesManager.RestartMidiProcessing();
                }
                else if (obj.script is Bass bass)
                {
                    bass.RestartCycle();
                }
                else
                {
                    Debug.LogWarning($"MusicManager: Unknown script type on {obj.script.name}");
                }
            }
            catch (System.Exception ex)
            {
                if (!isQuitting)
                {
                    Debug.LogError($"Error restarting cycle for {obj.script.name}: {ex.Message}");
                }
            }
        }
    }

    public void ScheduleUpdateForInstrument(MonoBehaviour script, AudioClip clip, TextAsset midi)
    {
        // Add or overwrite pending update for this script
        pendingUpdates.RemoveAll(p => p.script == script);
        pendingUpdates.Add(new PendingUpdate { script = script, clip = clip, midi = midi });
    }

    private bool TrySetMidiField(MonoBehaviour target, TextAsset midiAsset)
    {
        var t = target.GetType();

        // Try field named midiFile
        var field = t.GetField("midiFile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
        if (field != null && field.FieldType == typeof(TextAsset))
        {
            field.SetValue(target, midiAsset);
            return true;
        }

        // Try property named midiFile
        var prop = t.GetProperty("midiFile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
        if (prop != null && prop.CanWrite && prop.PropertyType == typeof(TextAsset))
        {
            prop.SetValue(target, midiAsset, null);
            return true;
        }

        return false;
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
        StopLoop();
    }

    void OnDisable()
    {
        if (!isQuitting)
        {
            StopLoop();
        }
    }

    void OnDestroy()
    {
        isQuitting = true;
    }
}