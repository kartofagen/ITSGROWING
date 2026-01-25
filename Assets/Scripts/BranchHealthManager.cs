using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BranchHealthManager : MonoBehaviour
{
    private static BranchHealthManager _instance;
    private static bool applicationQuitting = false;
    
    [Header("Critical State Music")]
    public int criticalThreshold = 10;
    
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public static BranchHealthManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // First, try to find an existing one in the scene
                _instance = Object.FindObjectOfType<BranchHealthManager>();
            }

            // If still null and application is playing, create a new one (without DontDestroyOnLoad)
            if (_instance == null && Application.isPlaying && !applicationQuitting)
            {
                var go = new GameObject("BranchHealthManager");
                _instance = go.AddComponent<BranchHealthManager>();
            }

            return _instance;
        }
    }

    private List<BranchHealth> branches = new List<BranchHealth>();

    public int _totalHealth;

    public int TotalHealth
    {
        get
        {
            return _totalHealth;
        }
        set
        {
            _totalHealth = value;
        }
    }
    
    public void RegisterBranch(BranchHealth b)
    {
        if (!branches.Contains(b))
            branches.Add(b);
        UpdateTotalHealth();
    }

    public void UnregisterBranch(BranchHealth b)
    {
        if (branches.Contains(b))
            branches.Remove(b);
        UpdateTotalHealth();
    }

    public IReadOnlyList<BranchHealth> GetBranches() => branches.AsReadOnly();

    public void GiveHealthToWeakestBranch(int amount)
    {
        if (branches.Count == 0) return;

        if (_totalHealth > criticalThreshold)
        {
            audioSource.Stop();
        }

        // Find the minimum health
        int minHealth = branches.Min(b => b.currentHealth);

        // Get all branches with min health
        var weakestBranches = branches.Where(b => b.currentHealth == minHealth).ToList();

        // Pick random if multiple
        BranchHealth target = weakestBranches[Random.Range(0, weakestBranches.Count)];
        target.AddHealth(amount);
        Debug.Log($"BranchHealthManager: gave {amount} HP to branch {target.branchName}");
    }
    
    public void UpdateTotalHealth()
    {
        if (_totalHealth <= criticalThreshold)
        {
            audioSource.Play();
        }
        TotalHealth = branches.Sum(b => b.currentHealth);
        EnemyWaves.Instance?.SetAggression(TotalHealth);
    }

    void OnDestroy()
    {
        applicationQuitting = true;
        // ensure static reference is cleared when object is destroyed
        if (_instance == this)
            _instance = null;
    }
}