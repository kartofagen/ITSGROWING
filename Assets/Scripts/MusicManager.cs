using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public enum Mode
    {
        DetectLoop,
        TimerBased
    }

    public Lead lead;
    public Mode mode = Mode.TimerBased;
    public bool autoStart = true;

    // Для DetectLoop
    private float prevTime = 0f;
    private bool prevIsPlaying = false;

    // Для TimerBased
    private float lastStartTime = 0f;
    private float interval = 0f;

    private AudioSource src;
    private bool running = false;

    void Awake()
    {
        src = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (autoStart)
            StartLoop();
    }

    void OnEnable()
    {
        prevTime = 0f;
        prevIsPlaying = false;
        lastStartTime = 0f;
    }

    public void StartLoop()
    {
        running = true;

        if (mode == Mode.DetectLoop)
        {
            src.loop = true;
            prevTime = src.time;
            prevIsPlaying = src.isPlaying;

            if (!src.isPlaying)
            {
                src.Play();
                NotifyLead();
            }
        }
        else // TimerBased
        {
            src.loop = false;
            interval = src.clip.length / Mathf.Max(0.0001f, src.pitch);
            lastStartTime = Time.time;
            src.Play();
            NotifyLead();
        }
    }

    public void StopLoop()
    {
        running = false;
        if (src != null)
            src.Stop();
    }

    void Update()
    {
        if (!running || src == null || src.clip == null)
            return;

        switch (mode)
        {
            case Mode.DetectLoop:
                DetectLoopUpdate();
                break;

            case Mode.TimerBased:
                TimerBasedUpdate();
                break;
        }
    }

    private void DetectLoopUpdate()
    {
        if (!src.isPlaying)
            return;

        float currentTime = src.time;
        if (prevIsPlaying && currentTime < prevTime - 0.001f)
        {
            // обнаружен новый цикл
            NotifyLead();
        }

        prevTime = currentTime;
        prevIsPlaying = src.isPlaying;
    }

    private void TimerBasedUpdate()
    {
        float now = Time.time;
        if (now >= lastStartTime + interval - 1e-3f)
        {
            src.Play();
            lastStartTime = now;
            NotifyLead();
        }
    }

    private void NotifyLead()
    {
        if (lead != null)
        {
            lead.RestartMidiProcessing();
        }
    }

    void OnDisable()
    {
        StopLoop();
    }
}
