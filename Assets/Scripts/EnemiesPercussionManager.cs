using UnityEngine;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using System.IO;
using System.Collections.Generic;

public class EnemiesPercussionManager : InstrumentBase
{
    public GameObject enemyPrefab;
    public float spawnRate = 2f;
    public float spawnDistance = 10f;
    
    [SerializeField] private float playOffset = 0.1f;
    [SerializeField] private float moveDistance = 0.5f;

    private Transform player;
    private float nextSpawnTime = 0f;
    private Playback playback;
    private double startDspTime;
    
    private List<double> pendingMoveTimes = new List<double>();
    private object _lock = new object();
    private bool isQuitting = false;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        
        // Не запускаем playback автоматически
        // Ждем первого вызова RestartMidiProcessing от MusicManager
        if (midiFile == null)
        {
            Debug.LogWarning("EnemiesManager: No MIDI file assigned. Enemies will spawn but not move to rhythm.");
        }
    }

    void Update()
    {
        if (isQuitting) return;

        if (Time.time >= nextSpawnTime)
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
    
    void SpawnEnemy()
    {
        if (isQuitting || player == null) return;

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
        if (isQuitting) return;

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
                
                // Даем время на завершение нативных таймеров
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

    void OnApplicationQuit()
    {
        isQuitting = true;
        StopPlayback();
    }
    
    void OnDestroy()
    {
        isQuitting = true;
        StopPlayback();
    }

    void OnDisable()
    {
        if (!isQuitting)
        {
            StopPlayback();
        }
    }
}