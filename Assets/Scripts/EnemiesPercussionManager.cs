using UnityEngine;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class EnemiesPercussionManager : InstrumentBase
{
    private static EnemiesPercussionManager _instance;
    private static bool applicationQuitting = false;
    
    [Header("Intro")]
    [SerializeField] private bool intro = true;
    [SerializeField] private float introDuration = 1f;

    [Header("Spawn")]
    public static EnemiesPercussionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindObjectOfType<EnemiesPercussionManager>();
            }

            if (_instance == null && Application.isPlaying && !applicationQuitting)
            {
                var go = new GameObject("EnemiesPercussionManager");
                _instance = go.AddComponent<EnemiesPercussionManager>();
            }

            return _instance;
        }
    }

    public GameObject enemyPrefab;
    public float spawnRate = 2f;
    public float spawnDistance = 10f;
    
    [SerializeField] private float playOffset = 0.1f;
    [SerializeField] private float moveDistance = 0.5f;
    [SerializeField] private float spawnRateCoeff = 0.1f;

    private float initialSpawnRate;

    private Transform player;
    private float nextSpawnTime = 0f;
    private Playback playback;
    private double startDspTime;
    private double pausedAtDspTime;
    private float pausedAtTime;
    private bool isPausedInternal = false;
    
    private List<double> pendingMoveTimes = new List<double>();
    private object _lock = new object();
    private bool isQuitting = false;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        
        if (midiFile == null)
        {
            Debug.LogWarning("EnemiesManager: No MIDI file assigned. Enemies will spawn but not move to rhythm.");
        }

        initialSpawnRate = spawnRate;

        StartCoroutine(Intro());
    }
    
    private IEnumerator Intro()
    {
        yield return new WaitForSeconds(introDuration);
        intro = false;
    }

    void Update()
    {
        if (isQuitting || isPausedInternal) return;

        if (Time.time >= nextSpawnTime && !intro)
        {
            SpawnEnemy();
            nextSpawnTime = Time.time + spawnRate;
        }
        
        List<double> toProcess = new List<double>();
        lock (_lock)
        {
            for (int i = pendingMoveTimes.Count - 1; i >= 0; i--)
            {
                if (AudioSettings.dspTime >= pendingMoveTimes[i])
                {
                    toProcess.Add(pendingMoveTimes[i]);
                    pendingMoveTimes.RemoveAt(i);
                }
            }
        }

        foreach (var moveTime in toProcess)
        {
            MoveAllEnemiesOneStep();
        }
    }

    public void OnPause()
    {
        if (isPausedInternal) return;
        
        isPausedInternal = true;
        pausedAtDspTime = AudioSettings.dspTime;
        pausedAtTime = Time.time;
        
        // Pause MIDI playback
        if (playback != null)
        {
            try
            {
                playback.Stop();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Error pausing EnemiesPercussionManager playback: {ex.Message}");
            }
        }
        
        Debug.Log("EnemiesPercussionManager paused");
    }

    public void OnResume()
    {
        if (!isPausedInternal) return;
        
        double pauseDuration = AudioSettings.dspTime - pausedAtDspTime;
        float timePauseDuration = Time.time - pausedAtTime;
        
        // Adjust all pending move times
        lock (_lock)
        {
            for (int i = 0; i < pendingMoveTimes.Count; i++)
            {
                pendingMoveTimes[i] += pauseDuration;
            }
        }
        
        // Adjust next spawn time
        nextSpawnTime += timePauseDuration;
        
        // Resume MIDI playback
        if (playback != null)
        {
            try
            {
                playback.Start();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Error resuming EnemiesPercussionManager playback: {ex.Message}");
            }
        }
        
        isPausedInternal = false;
        Debug.Log($"EnemiesPercussionManager resumed (adjusted times by {pauseDuration}s)");
    }
    
    void SpawnEnemy()
    {
        if (isQuitting || player == null || isPausedInternal) return;

        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 spawnPosition = (Vector2)player.position + spawnDistance * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        enemy.tag = "Enemy";

        EnemyMovement em = enemy.GetComponent<EnemyMovement>();
        em.target = player;
        em.moveDistance = moveDistance;
    }
    
    private void SetupMidiPlayback()
    {
        if (isQuitting) return;

        using (var memoryStream = new MemoryStream(midiFile.bytes))
        {
            var mf = MidiFile.Read(memoryStream);
            var tempoMap = mf.GetTempoMap();
            var timedEvents = mf.GetTimedEvents();

            playback = new Playback(timedEvents, tempoMap);
            playback.EventPlayed += OnEventPlayed;
            
            startDspTime = AudioSettings.dspTime;
            playback.Start();
        }
    }
    
    private void OnEventPlayed(object sender, MidiEventPlayedEventArgs e)
    {
        if (isQuitting || playback == null) return;

        try
        {
            if (e.Event is NoteOnEvent noteOn && noteOn.Velocity > 0)
            {
                var currentTime = playback.GetCurrentTime<MetricTimeSpan>();
                double moveTime = startDspTime + (double)currentTime.TotalSeconds + (double)playOffset;
        
                lock (_lock)
                {
                    pendingMoveTimes.Add(moveTime);
                }
            }
        }
        catch (System.Exception ex)
        {
            if (!isQuitting)
            {
                Debug.LogWarning($"Error in OnEventPlayed: {ex.Message}");
            }
        }
    }
    
    private void MoveAllEnemiesOneStep()
    {
        if (isQuitting || isPausedInternal) return;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                EnemyMovement perc = enemy.GetComponent<EnemyMovement>();
                if (perc != null)
                {
                    perc.MoveOneStep();
                }
            }
        }
    }
    
    public void RestartMidiProcessing()
    {
        if (isQuitting) return;

        StopPlayback();

        lock (_lock)
        {
            pendingMoveTimes.Clear();
        }

        isPausedInternal = false;

        if (midiFile != null)
        {
            SetupMidiPlayback();
        }
    }

    private void StopPlayback()
    {
        if (playback != null)
        {
            try
            {
                playback.EventPlayed -= OnEventPlayed;
                playback.Stop();
                
                System.Threading.Thread.Sleep(50);
                
                playback.Dispose();
            }
            catch (System.Exception ex)
            {
                if (!isQuitting)
                {
                    Debug.LogWarning($"Error stopping playback: {ex.Message}");
                }
            }
            finally
            {
                playback = null;
            }
        }
    }

    public void UpdateSpawnRate()
    {
        int killed = EnemyWaves.Instance?.KilledEnemies ?? 0;
        spawnRate = initialSpawnRate / (1f + killed * spawnRateCoeff);
        Debug.Log($"EnemiesPercussionManager: Updated spawnRate to {spawnRate} based on {killed} killed enemies");
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
        applicationQuitting = true;
        StopPlayback();
    }
    
    void OnDestroy()
    {
        isQuitting = true;
        applicationQuitting = true;
        StopPlayback();
        if (_instance == this)
            _instance = null;
    }

    void OnDisable()
    {
        if (!isQuitting)
        {
            StopPlayback();
        }
    }
}