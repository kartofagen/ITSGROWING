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
    [SerializeField] private float attackRate = 1f;
    [SerializeField] private float attackAngle = 120f;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private float spawnDistance = 1f;

    [SerializeField] private bool autoDetectNoteRange = true;
    [SerializeField] private int minMidiNote = 21;
    [SerializeField] private int maxMidiNote = 108;
    
    [SerializeField] private float playOffset = 0.1f;

    private Playback playback;
    private bool useTimer = false;
    private float nextFireTime = 0f;
    private float startTime;

    private List<PendingShot> pendingShots = new List<PendingShot>();
    private object _lock = new object();

    private struct PendingShot
    {
        public float angle;
        public float spawnTime;
    }

    void Start()
    {
        if (midiFile != null)
        {
            SetupMidiPlayback();
        }
        else
        {
            useTimer = true;
        }
    }

    void Update()
    {
        if (useTimer && Time.time >= nextFireTime)
        {
            float randomAngle = Random.Range(-attackAngle / 2f, attackAngle / 2f);
            SpawnProjectile(randomAngle);
            nextFireTime = Time.time + attackRate;
        }

        List<PendingShot> toSpawn = new List<PendingShot>();
        lock (_lock)
        {
            for (int i = pendingShots.Count - 1; i >= 0; i--)
            {
                if (Time.time >= pendingShots[i].spawnTime)
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
            playback.Start();
            startTime = Time.time;
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
        
        Debug.Log($"Автоматически определен диапазон нот: {minMidiNote}-{maxMidiNote}");
    }

    private void OnEventPlayed(object sender, MidiEventPlayedEventArgs e)
    {
        if (e.Event is NoteOnEvent noteOn && noteOn.Velocity > 0)
        {
            int noteNumber = noteOn.NoteNumber;
            
            float fraction = Mathf.Clamp01(
                (noteNumber - minMidiNote) / (float)(maxMidiNote - minMidiNote)
            );
            
            float relativeAngle = (fraction - 0.5f) * attackAngle;

            var currentTime = playback.GetCurrentTime<MetricTimeSpan>();
            float spawnTime = startTime + (float)currentTime.TotalSeconds + playOffset;

            lock (_lock)
            {
                pendingShots.Add(new PendingShot { 
                    angle = relativeAngle, 
                    spawnTime = spawnTime 
                });
            }
        }
    }

    private void SpawnProjectile(float relativeAngle)
    {
        float baseAngle = transform.eulerAngles.z;
        float spawnAngle = (baseAngle + relativeAngle) * Mathf.Deg2Rad;
        
        Vector2 spawnPos = (Vector2)transform.position + 
                          spawnDistance * new Vector2(Mathf.Cos(spawnAngle), Mathf.Sin(spawnAngle));
        Vector2 direction = new Vector2(Mathf.Cos(spawnAngle), Mathf.Sin(spawnAngle));

        GameObject projectile = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        ProjectileMovement pm = projectile.GetComponent<ProjectileMovement>();
        pm.speed = projectileSpeed;
        pm.direction = direction;
        projectile.tag = "Projectile";
    }
    
    public void RestartMidiProcessing()
    {
        lock (_lock)
        {
            pendingShots.Clear();
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
        else
        {
            useTimer = true;
        }

        Debug.Log("Lead: RestartMidiProcessing called — playback restarted and pending shots cleared.");
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