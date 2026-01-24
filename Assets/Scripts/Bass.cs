using UnityEngine;

public class Bass : MonoBehaviour
{
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private float shieldDuration = 1f;
    [SerializeField] private float playOffset = 0.1f;
    
    [SerializeField] private float defendAngle = 120f; 
    [SerializeField] private float shieldRadius = 2f; 

    private GameObject currentShield;
    private Transform player;
    private float cycleStartTime;
    private float nextShieldTime;
    private bool isQuitting = false;
    private bool isActive = false;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Update()
    {
        if (isQuitting || !isActive || player == null) return;

        float currentTime = Time.time - cycleStartTime;

        if (currentShield == null && currentTime >= playOffset && currentTime < playOffset + shieldDuration)
        {
            ActivateShield();
        }

        if (currentShield != null && currentTime >= playOffset + shieldDuration)
        {
            DeactivateShield();
        }
    }

    private void ActivateShield()
    {
        if (currentShield != null || player == null) return;

        currentShield = Instantiate(shieldPrefab, transform.position, transform.rotation);
        currentShield.transform.SetParent(transform);
        currentShield.transform.localPosition = Vector3.zero;

        Shield shieldScript = currentShield.GetComponent<Shield>();
        shieldScript.Initialize(shieldDuration);
    }

    private void DeactivateShield()
    {
        if (currentShield != null)
        {
            Destroy(currentShield);
            currentShield = null;
        }
    }

    public void RestartCycle()
    {
        if (isQuitting) return;

        cycleStartTime = Time.time;
        isActive = true;

        DeactivateShield();
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
        DeactivateShield();
    }

    void OnDestroy()
    {
        isQuitting = true;
        DeactivateShield();
    }

    void OnDisable()
    {
        if (!isQuitting)
        {
            DeactivateShield();
            isActive = false;
        }
    }
}