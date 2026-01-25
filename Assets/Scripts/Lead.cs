using UnityEngine;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class Lead : InstrumentBase
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float attackAngle = 120f;
    [SerializeField] private float spawnDistance = 1f;

    [SerializeField] private bool autoDetectNoteRange = true;
    [SerializeField] private int minMidiNote = 21;
    [SerializeField] private int maxMidiNote = 108;
    
    [SerializeField] private float playOffset = 0.1f;

    private Playback playback;
    private double startDspTime;

    private float initialAngleOffset;
    
    private List<PendingShot> pendingShots = new List<PendingShot>();
    private object _lock = new object();
    private bool isQuitting = false;

    private struct PendingShot
    {
        public float angle;
        public float spawnTime;
    }

    void Start()
    {
        initialAngleOffset = transform.localEulerAngles.z;
    }

    void Update()
    {
        if (isQuitting || midiFile == null) return;

        List<PendingShot> toSpawn = new List<PendingShot>();
        lock (_lock)
        {
            for (int i = pendingShots.Count - 1; i >= 0; i--)
            {
                if (AudioSettings.dspTime >= pendingShots[i].spawnTime)
                {
                    toSpawn.Add(pendingShots[i]);
                    pendingShots.RemoveAt(i);
                }
            }
        }

        foreach (var shot in toSpawn)
        {
            SpawnProjectile(shot.angle);
        }
    }

    private void SetupMidiPlayback()
    {
        if (isQuitting) return;

        using (var memoryStream = new MemoryStream(midiFile.bytes))
        {
            var mf = MidiFile.Read(memoryStream);

            if (autoDetectNoteRange)
            {
                DetectNoteRange(mf);
            }

            var tempoMap = mf.GetTempoMap();
            var timedEvents = mf.GetTimedEvents();

            playback = new Playback(timedEvents, tempoMap);
            playback.EventPlayed += OnEventPlayed;
            
            startDspTime = AudioSettings.dspTime;
            playback.Start();
        }
    }

    private void DetectNoteRange(MidiFile mf)
    {
        var notes = mf.GetNotes();
        
        if (notes.Count == 0)
        {
            Debug.LogWarning("В MIDI-файле не найдено нот. Используются стандартные значения.");
            return;
        }

        int minNote = notes.Min(n => n.NoteNumber);
        int maxNote = notes.Max(n => n.NoteNumber);
        
        minMidiNote = Mathf.Max(0, minNote - 1);
        maxMidiNote = Mathf.Min(127, maxNote + 1);
    }

    private void OnEventPlayed(object sender, MidiEventPlayedEventArgs e)
    {
        if (isQuitting || playback == null) return;

        try
        {
            if (e.Event is NoteOnEvent noteOn && noteOn.Velocity > 0)
            {
                int noteNumber = noteOn.NoteNumber;
        
                float fraction = Mathf.Clamp01(
                    (noteNumber - minMidiNote) / (float)(maxMidiNote - minMidiNote)
                );
        
                float relativeAngle = (fraction - 0.5f) * attackAngle;

                var currentTime = playback.GetCurrentTime<MetricTimeSpan>();
                double spawnDspTime = startDspTime + (double)currentTime.TotalSeconds + (double)playOffset;

                lock (_lock)
                {
                    pendingShots.Add(new PendingShot { 
                        angle = relativeAngle, 
                        spawnTime = (float)spawnDspTime
                    });
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

    private void SpawnProjectile(float relativeAngle)
    {
        if (isQuitting) return;

        float baseAngle = transform.localEulerAngles.z - initialAngleOffset;
        float spawnAngle = (baseAngle + relativeAngle) * Mathf.Deg2Rad;
        
        Vector2 spawnPos = (Vector2)transform.position + 
                          spawnDistance * new Vector2(Mathf.Cos(spawnAngle), Mathf.Sin(spawnAngle));
        Vector2 direction = new Vector2(Mathf.Cos(spawnAngle), Mathf.Sin(spawnAngle));

        GameObject projectile = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        ProjectileMovement pm = projectile.GetComponent<ProjectileMovement>();
        pm.direction = direction;
        projectile.tag = "Projectile";
    }
    
    public void RestartMidiProcessing()
    {
        if (isQuitting) return;

        StopPlayback();

        lock (_lock)
        {
            pendingShots.Clear();
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