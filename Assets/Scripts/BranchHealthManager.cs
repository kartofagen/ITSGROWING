using System.Collections.Generic;
using UnityEngine;

public class BranchHealthManager : MonoBehaviour
{
    private static BranchHealthManager _instance;
    private static bool applicationQuitting = false;

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

    public void RegisterBranch(BranchHealth b)
    {
        if (!branches.Contains(b))
            branches.Add(b);
    }

    public void UnregisterBranch(BranchHealth b)
    {
        if (branches.Contains(b))
            branches.Remove(b);
    }

    public IReadOnlyList<BranchHealth> GetBranches() => branches.AsReadOnly();

    public void GiveHealthToRandomBranch(int amount)
    {
        if (branches.Count == 0) return;
        int idx = Random.Range(0, branches.Count);
        branches[idx].AddHealth(amount);
        Debug.Log($"BranchHealthManager: gave {amount} HP to branch {branches[idx].branchName}");
    }

    void OnDestroy()
    {
        applicationQuitting = true;
        // ensure static reference is cleared when object is destroyed
        if (_instance == this)
            _instance = null;
    }
}