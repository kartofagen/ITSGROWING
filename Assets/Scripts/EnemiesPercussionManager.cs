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
    [SerializeField] private float moveDistance = 0.5f; // Фиксированное расстояние за шаг

    private Transform player;
    private float nextSpawnTime = 0f;
    private Playback playback;
    private double startDspTime;
    
    private List<double> pendingMoveTimes = new List<double>();
    private object _lock = new object();

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        
        if (midiFile != null)
        {
            SetupMidiPlayback();
        }
        else
        {
            Debug.LogWarning("EnemiesManager: No MIDI file assigned. Enemies will spawn but not move to rhythm.");
        }
    }

    void Update()
    {
        // Спавн врагов по таймеру
        if (Time.time >= nextSpawnTime)
        {
            SpawnEnemy();
            nextSpawnTime = Time.time + spawnRate;
        }
        
        // Обработка запланированных движений
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

        
        // Применяем движения ко всем врагам
        foreach (var moveTime in toProcess)
        {
            MoveAllEnemiesOneStep();
        }
    }
    
    void SpawnEnemy()
    {
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
        using (var memoryStream = new MemoryStream(midiFile.bytes))
        {
            var mf = MidiFile.Read(memoryStream);
            var tempoMap = mf.GetTempoMap();
            var timedEvents = mf.GetTimedEvents();

            playback = new Playback(timedEvents, tempoMap);
            playback.EventPlayed += OnEventPlayed;
            playback.Start();
            startDspTime = Time.time;
        }
    }
    
    private void OnEventPlayed(object sender, MidiEventPlayedEventArgs e)
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

    
    private void MoveAllEnemiesOneStep()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemy in enemies)
        {
            EnemyMovement perc = enemy.GetComponent<EnemyMovement>();
            if (perc != null)
            {
                perc.MoveOneStep();
            }
        }
    }
    
    public void RestartMidiProcessing()
    {
        lock (_lock)
        {
            pendingMoveTimes.Clear();
        }

        if (playback != null)
        {
            playback.EventPlayed -= OnEventPlayed;
            playback.Stop();
            playback.Dispose();
            playback = null;
        }

        if (midiFile != null)
        {
            SetupMidiPlayback();
        }
    }
    
    void OnDestroy()
    {
        if (playback != null)
        {
            playback.EventPlayed -= OnEventPlayed;
            playback.Dispose();
        }
    }
}