using UnityEngine;
using System.Collections.Generic;

public class MusicManager : MonoBehaviour
{
    [SerializeField] private AudioClip baseClip;
    [SerializeField] private AudioSource[] sources;

    [System.Serializable]
    public class MidiControlledObject
    {
        public MonoBehaviour script; // Lead или EnemiesManager
    }

    public List<MidiControlledObject> midiControlledObjects = new List<MidiControlledObject>();
    public bool autoStart = true;

    private float lastStartTime = 0f;
    private float interval = 0f;

    private bool running = false;

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
        running = true;
        interval = baseClip.length;

        NotifyAll();

        lastStartTime = Time.time;
        foreach (AudioSource source in sources)
        {
            source.Play();
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
        if (!running)
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
        foreach (var obj in midiControlledObjects)
        {
            if (obj.script is Lead lead)
            {
                lead.RestartMidiProcessing();
            }
            else if (obj.script is EnemiesPercussionManager enemiesManager)
            {
                enemiesManager.RestartMidiProcessing();
            }
            else
            {
                Debug.LogWarning($"MusicManager: Unknown script type on {obj.script.name}");
            }
        }
    }

    void OnDisable()
    {
        StopLoop();
    }
}