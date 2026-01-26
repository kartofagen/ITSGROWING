using UnityEngine;

public class AudioRepeater : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private AudioClip _audioClip;
    [SerializeField] private float _delayBetweenPlays = 21.39429f;
    
    [Header("Источники звука")]
    [SerializeField] private AudioSource _audioSource1;
    [SerializeField] private AudioSource _audioSource2;
    
    private AudioSource _currentSource;
    private AudioSource _nextSource;
    private float _timer;
    
    private void Start()
    {
        InitializeAudioSources();
        
        _currentSource = _audioSource1;
        _nextSource = _audioSource2;
        
        PlayWithCurrentSource();
    }
    
    private void InitializeAudioSources()
    {
        _audioSource1.clip = _audioClip;
        _audioSource2.clip = _audioClip;
        _audioSource1.playOnAwake = false;
        _audioSource2.playOnAwake = false;
        _audioSource1.loop = false;
        _audioSource2.loop = false;
    }
    
    private void Update()
    {
        _timer += Time.deltaTime;
            
        if (_timer >= _delayBetweenPlays)
        {
            PlayWithNextSource();
        }
    }
    
    private void PlayWithCurrentSource()
    {
        _currentSource.Play();
        _timer = 0f;
    }
    
    private void PlayWithNextSource()
    {
        (_currentSource, _nextSource) = (_nextSource, _currentSource);

        PlayWithCurrentSource();
    }
}