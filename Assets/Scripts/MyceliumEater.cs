using UnityEngine;
using System;

[RequireComponent(typeof(MyceliumGrowController))]
public class MyceliumEater : MonoBehaviour
{
    [Header("Configuration")]
    public float growSpeed = 0.25f;
    public float targetScale = 1f;
    public int healthToGive = 1;
    
    [Header("State")]
    public bool isDefault = false;
    
    [HideInInspector]
    public Action<GameObject> onDone;
    
    private MyceliumGrowController growController;
    private MyceliumTree3D tree;
    private bool isGrowing = false;
    private bool isShrinking = false;
    private Action onShrinkComplete;
    private GameObject targetEnemy;
    
    void Awake()
    {
        growController = GetComponent<MyceliumGrowController>();
        tree = GetComponent<MyceliumTree3D>();
        
        if (growController != null)
        {
            growController.playOnStart = false;
            growController.grow = 0f;
            growController.growSpeed = growSpeed;
        }
        
        // Disable tree initially for non-default eaters
        if (tree != null && !isDefault)
        {
            tree.enabled = false;
        }
    }
    
    void Update()
    {
        if (isGrowing)
        {
            if (growController.grow >= 1f)
            {
                OnGrowthComplete();
            }
        }
        else if (isShrinking)
        {
            if (growController.grow <= 0f)
            {
                OnShrinkComplete();
            }
        }
    }
    
    public void Grow()
    {
        if (growController == null) return;
        
        // Enable tree for non-default eaters when starting to grow
        if (tree != null && !isDefault && !tree.enabled)
        {
            tree.enabled = true;
        }
        
        isGrowing = true;
        isShrinking = false;
        growController.playOnStart = true;
        growController.growSpeed = Mathf.Abs(growSpeed);
        
        // Store target enemy reference if available
        if (tree != null && tree.targetObject != null)
        {
            targetEnemy = tree.targetObject.gameObject;
        }
    }
    
    public void Shrink(Action onComplete = null)
    {
        if (growController == null) return;
        
        isGrowing = false;
        isShrinking = true;
        onShrinkComplete = onComplete;
        
        growController.playOnStart = true;
        growController.growSpeed = -Mathf.Abs(growSpeed);
    }
    
    public void ResetToStart()
    {
        if (growController == null) return;
        
        isGrowing = false;
        isShrinking = false;
        growController.grow = 0f;
        growController.playOnStart = false;
    }
    
    private void OnGrowthComplete()
    {
        isGrowing = false;
        growController.playOnStart = false;
        
        // Don't do anything for default eater
        if (isDefault)
        {
            return;
        }
        
        // Consume the enemy and give health
        if (targetEnemy != null)
        {
            Destroy(targetEnemy);
            BranchHealthManager.Instance?.GiveHealthToWeakestBranch(healthToGive);
            Debug.Log($"MyceliumEater consumed enemy, gave {healthToGive} health");
        }
        
        // Start shrinking back
        Shrink(() => {
            // Notify parent that we're done
            if (onDone != null)
            {
                onDone.Invoke(gameObject);
            }
        });
    }
    
    private void OnShrinkComplete()
    {
        isShrinking = false;
        growController.playOnStart = false;
        
        // Call completion callback if set
        if (onShrinkComplete != null)
        {
            Action callback = onShrinkComplete;
            onShrinkComplete = null;
            callback.Invoke();
        }
    }
}