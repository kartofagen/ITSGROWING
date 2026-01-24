using UnityEngine;
using System.Collections.Generic;

public class MusicManager : MonoBehaviour
{
    [SerializeField] private AudioClip baseClip;
    [SerializeField] private AudioSource[] sources;

    [System.Serializable]
    public class MidiControlledObject
    {
        public MonoBehaviour script;
    }

    public List<MidiControlledObject> midiControlledObjects = new List<MidiControlledObject>();
    public bool autoStart = true;

    private float lastStartTime = 0f;
    private float interval = 0f;
    private bool running = false;
    private bool isQuitting = false;

    void Start()
    {
        if (autoStart)
            StartLoop();
    }

    void OnEnable()
    {
        lastStartTime = 0f;
    }

    public void StartLoop()
    {
        if (isQuitting) return;

        running = true;
        interval = baseClip.length;

        // Даем объектам время на инициализацию перед первым рестартом
        StartCoroutine(DelayedFirstNotify());

        lastStartTime = Time.time;
        foreach (AudioSource source in sources)
        {
            source.Play();
        }
    }

    private System.Collections.IEnumerator DelayedFirstNotify()
    {
        // Ждем один кадр, чтобы все Start() методы завершились
        yield return null;
        
        if (!isQuitting)
        {
            NotifyAll();
        }
    }

    public void StopLoop()
    {
        running = false;
        foreach (AudioSource source in sources)
        {
            source.Stop();
        }
    }

    void Update()
    {
        if (!running || isQuitting)
            return;

        TimerBasedUpdate();
    }

    private void TimerBasedUpdate()
    {
        float now = Time.time;
        if (now >= lastStartTime + interval - 1e-3f)
        {
            NotifyAll();

            foreach (AudioSource source in sources)
            {
                source.Play();
            }
            lastStartTime = now;
        }
    }

    private void NotifyAll()
    {
        if (isQuitting) return;

        foreach (var obj in midiControlledObjects)
        {
            if (obj.script == null) continue;

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
        StopAllCoroutines();
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
        StopAllCoroutines();
    }
}