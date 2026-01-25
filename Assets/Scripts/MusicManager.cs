using UnityEngine;
using System.Collections.Generic;

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
        [HideInInspector] public AudioSource sourceA;
        [HideInInspector] public AudioSource sourceB;
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

    void Start()
    {
        foreach (var obj in midiControlledObjects)
        {
            if (obj.script == null || obj.audioClip == null) continue;
            
            obj.sourceA = obj.script.gameObject.AddComponent<AudioSource>();
            obj.sourceA.clip = obj.audioClip;
            obj.sourceA.playOnAwake = false;
            obj.sourceA.loop = false;
            
            obj.sourceB = obj.script.gameObject.AddComponent<AudioSource>();
            obj.sourceB.clip = obj.audioClip;
            obj.sourceB.playOnAwake = false;
            obj.sourceB.loop = false;
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

    private void PlayCurrentSources()
    {
        foreach (var obj in midiControlledObjects)
        {
            if (!obj.enabled || obj.script == null) continue;
            
            // Ð§ÐµÑ€ÐµÐ´ÑƒÐµÐ¼ Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ¸
            AudioSource currentSource = obj.useSourceA ? obj.sourceA : obj.sourceB;
            
            if (currentSource != null)
            {
                currentSource.Play();
            }
            
            obj.useSourceA = !obj.useSourceA;
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