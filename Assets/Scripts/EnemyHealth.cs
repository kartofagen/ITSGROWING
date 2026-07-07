using UnityEngine;
[RequireComponent(typeof(Collider))]
public class EnemyHealth : MonoBehaviour
{
    [Tooltip("Тег, который использует ваш projectile/pool для пуль")]
    public string projectileTag = "Projectile";
    [SerializeField] private AudioClip[] dieSounds;
    [SerializeField] private SkinnedMeshRenderer renderer;
    [SerializeField] private Material deadMaterial;
    [SerializeField] private float destroyDelay = 15f;
    
    private bool isDead = false;
    private AudioSource audioSource;
    private Camera mainCamera;
    
    public bool IsDead => isDead;
    public bool isEaten = false;
    
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        mainCamera = Camera.main;
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (isDead) return;
        if (other.CompareTag(projectileTag))
        {
            if (IsVisibleByCamera())
            {
                Destroy(other.gameObject);
                Die();
            }
        }
    }
    
    private bool IsVisibleByCamera()
    {
        if (mainCamera == null) return false;
        
        Bounds bounds = renderer.bounds;
        
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
        bool inFrustum = GeometryUtility.TestPlanesAABB(planes, bounds);
        
        if (!inFrustum) return false;
        
        Vector3 viewportPoint = mainCamera.WorldToViewportPoint(transform.position);
        return viewportPoint.z > 0 && 
               viewportPoint.x >= 0 && viewportPoint.x <= 1 && 
               viewportPoint.y >= 0 && viewportPoint.y <= 1;
    }
    
    public void Die()
    {
        if (isDead) return;
        
        isDead = true;
        audioSource.clip = dieSounds[Random.Range(0, dieSounds.Length)];
        audioSource.Play();
        transform.GetChild(0).localRotation = Quaternion.Euler(180, 0, 90);
        renderer.material = deadMaterial;
        
        var em = GetComponent<EnemyMovement>();
        if (em != null) em.enabled = false;
        
        EnemyWaves.Instance?.IncrementKilledEnemies();
        
        Destroy(gameObject, destroyDelay);
    }
}