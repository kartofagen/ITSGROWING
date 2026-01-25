using UnityEngine;
using System.Collections.Generic;

public class Bass : InstrumentBase
{
    [Header("Shield Configuration")]
    [Tooltip("Shield health for each applied upgrade level. Index 0 = no upgrades (base), index 1 = 1 upgrade applied, etc.")]
    public List<int> shieldHealthPerLevel = new List<int>() { 0, 10, 20, 30, 40 };
    
    [Header("Shield Status (Read-only)")]
    [SerializeField] private int currentShieldHealth = 0;
    [SerializeField] private int currentLevel = 0;
    [SerializeField] private int maxShieldHealth = 0;
    
    private BranchHealth branchHealth;
    
    void Start()
    {
        branchHealth = GetComponent<BranchHealth>();
        if (branchHealth == null)
        {
            Debug.LogWarning("Bass: No BranchHealth component found on this GameObject!");
        }
        
        // Initialize shield with base level
        OnUpgradeLevelChanged(0);
    }
    
    /// <summary>
    /// Called by BranchHealth when upgrade level changes.
    /// Restores shield to full capacity for the new level.
    /// </summary>
    public void OnUpgradeLevelChanged(int newLevel)
    {
        currentLevel = newLevel;
        maxShieldHealth = GetMaxShieldForLevel(newLevel);
        currentShieldHealth = maxShieldHealth;
        
        Debug.Log($"Bass shield updated: Level {newLevel}, Shield restored to {currentShieldHealth}/{maxShieldHealth}");
    }
    
    /// <summary>
    /// Absorbs damage and returns overflow damage that should pass through to BranchHealth.
    /// </summary>
    /// <param name="damage">Incoming damage amount</param>
    /// <returns>Damage that passes through the shield</returns>
    public int TakeDamage(int damage)
    {
        if (damage <= 0) return 0;
        
        if (currentShieldHealth > 0)
        {
            int absorbed = Mathf.Min(damage, currentShieldHealth);
            currentShieldHealth -= absorbed;
            int overflow = damage - absorbed;
            
            if (absorbed > 0)
            {
                Debug.Log($"Bass shield absorbed {absorbed} damage. Remaining: {currentShieldHealth}/{maxShieldHealth}. Overflow: {overflow}");
            }
            
            return overflow;
        }
        
        Debug.Log($"Bass shield is depleted! All {damage} damage passes through to branch.");
        return damage;
    }
    
    public void RepairShield(int amount)
    {
        if (amount <= 0) return;
        
        int oldHealth = currentShieldHealth;
        currentShieldHealth = Mathf.Min(maxShieldHealth, currentShieldHealth + amount);
        
        Debug.Log($"Bass shield repaired: {oldHealth} -> {currentShieldHealth}/{maxShieldHealth}");
    }
    
    private int GetMaxShieldForLevel(int level)
    {
        if (level >= 0 && level < shieldHealthPerLevel.Count)
            return shieldHealthPerLevel[level];
        
        // If level is beyond our list, return the last value
        if (level >= shieldHealthPerLevel.Count && shieldHealthPerLevel.Count > 0)
            return shieldHealthPerLevel[shieldHealthPerLevel.Count - 1];
            
        return 0;
    }
}