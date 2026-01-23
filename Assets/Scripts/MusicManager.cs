using System;
using UnityEngine;
using UnityEngine.Events;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;
    
    [Header("Музыкальные настройки")]
    public float bpm = 120;
    public AudioSource musicSource;
    private int beatsPerBar = 4;
    
    [Header("Tact events")]
    public UnityEvent onBeat;
    public UnityEvent onBar;
    
    [Header("Tick events")]
    public UnityEvent onPercussionTick;
    public UnityEvent onBassTick;
    public UnityEvent onLeadTick;
    
    [Header("Instrument ticks")]
    [Range(1, 8)] public int percussionEveryNBeat = 1;
    [Range(1, 8)] public int bassEveryNBeat = 2;
    [Range(1, 16)] public int leadEveryNBeat = 4;
    
    private float beatInterval;
    private float nextBeatTime;
    private int currentBeat = 0;
    
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    
    private void Start()
    {
        CalculateBeatInterval();
        if (musicSource.isPlaying)
            nextBeatTime = musicSource.time + beatInterval;
    }
    
    private void Update()
    {
        if (!musicSource.isPlaying) return;
        
        if (musicSource.time >= nextBeatTime)
        {
            Beat();
            nextBeatTime += beatInterval;
        }
    }
    
    private void Beat()
    {
        currentBeat++;
        
        // Основное событие бита
        onBeat?.Invoke();
        
        // Проверяем перкуссию
        if (currentBeat % percussionEveryNBeat == 0)
            onPercussionTick?.Invoke();
        
        // Проверяем бас
        if (currentBeat % bassEveryNBeat == 0)
            onBassTick?.Invoke();
        
        // Проверяем лиды
        if (currentBeat % leadEveryNBeat == 0)
            onLeadTick?.Invoke();
        
        // Сброс счета на каждый бар
        if (currentBeat >= beatsPerBar)
            currentBeat = 0;
    }
    
    private void CalculateBeatInterval()
    {
        beatInterval = 60f / bpm;
    }
    
    public void StartMusic()
    {
        musicSource.Play();
        nextBeatTime = musicSource.time + beatInterval;
    }
}
