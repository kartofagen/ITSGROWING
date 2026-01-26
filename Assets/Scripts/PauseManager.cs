using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    private static PauseManager _instance;
    
    public static PauseManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindObjectOfType<PauseManager>();
            }
            return _instance;
        }
    }

    [Header("UI References")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button exitButton;
    
    [Header("Settings")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    
    private bool isPaused = false;
    private MusicManager musicManager;
    
    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }
    
    void Start()
    {
        musicManager = FindObjectOfType<MusicManager>();
        
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        
        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(ResumeGame);
        }
        
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(ExitToMenu);
        }
    }
    
    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }
    
    public void PauseGame()
    {
        if (isPaused) return;
        
        isPaused = true;
        Time.timeScale = 0f;
        
        // Pause audio sources instead of stopping them
        if (musicManager != null)
        {
            PauseAllAudioSources();
        }
        
        // Pause MIDI playback for instruments
        PauseMidiInstruments();
        
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
        
        DisablePlayerControls();
        
        Debug.Log("Game Paused");
    }
    
    public void ResumeGame()
    {
        if (!isPaused) return;
        
        isPaused = false;
        Time.timeScale = 1f;
        
        // Unpause audio sources
        if (musicManager != null)
        {
            UnpauseAllAudioSources();
        }
        
        // Resume MIDI playback for instruments
        ResumeMidiInstruments();
        
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        
        EnablePlayerControls();
        
        Debug.Log("Game Resumed");
    }
    
    public void ExitToMenu()
    {
        //Time.timeScale = 1f;
        //isPaused = false;
        Application.Quit();
        //SceneManager.LoadScene(menuSceneName);
    }
    
    private void PauseAllAudioSources()
    {
        if (musicManager == null) return;
        
        foreach (var obj in musicManager.midiControlledObjects)
        {
            if (obj.sourceA != null && obj.sourceA.isPlaying)
            {
                obj.sourceA.Pause();
            }
            if (obj.sourceB != null && obj.sourceB.isPlaying)
            {
                obj.sourceB.Pause();
            }
        }
    }
    
    private void UnpauseAllAudioSources()
    {
        if (musicManager == null) return;
        
        foreach (var obj in musicManager.midiControlledObjects)
        {
            if (obj.sourceA != null && obj.sourceA.time > 0)
            {
                obj.sourceA.UnPause();
            }
            if (obj.sourceB != null && obj.sourceB.time > 0)
            {
                obj.sourceB.UnPause();
            }
        }
    }
    
    private void PauseMidiInstruments()
    {
        // Notify Lead instruments
        Lead[] leads = FindObjectsOfType<Lead>();
        foreach (var lead in leads)
        {
            lead.OnPause();
        }
        
        // Notify EnemiesPercussionManager
        if (EnemiesPercussionManager.Instance != null)
        {
            EnemiesPercussionManager.Instance.OnPause();
        }
    }
    
    private void ResumeMidiInstruments()
    {
        // Notify Lead instruments
        Lead[] leads = FindObjectsOfType<Lead>();
        foreach (var lead in leads)
        {
            lead.OnResume();
        }
        
        // Notify EnemiesPercussionManager
        if (EnemiesPercussionManager.Instance != null)
        {
            EnemiesPercussionManager.Instance.OnResume();
        }
    }
    
    private void DisablePlayerControls()
    {
        BodyRotation bodyRotation = FindObjectOfType<BodyRotation>();
        if (bodyRotation != null)
        {
            bodyRotation.enabled = false;
        }
    }
    
    private void EnablePlayerControls()
    {
        BodyRotation bodyRotation = FindObjectOfType<BodyRotation>();
        if (bodyRotation != null)
        {
            bodyRotation.enabled = true;
        }
    }
    
    public bool IsPaused()
    {
        return isPaused;
    }
    
    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
        
        Time.timeScale = 1f;
    }
}