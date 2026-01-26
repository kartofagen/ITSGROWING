using UnityEngine;
[RequireComponent(typeof(Collider))]
public class EnemyHealth : MonoBehaviour
{
    [Tooltip("Тег, который использует ваш projectile/pool для пуль")]
    public string projectileTag = "Projectile";
    [SerializeField] private AudioClip[] dieSounds;
    [SerializeField] private SkinnedMeshRenderer renderer;
    [SerializeField] private Material deadMaterial;
    
    private bool isDead = false;
    private AudioSource audioSource;
    
    public bool IsDead => isDead; // публичный геттер для внешних систем
    public bool isEaten = false; // Added to prevent multiple eating
    
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }
    void OnTriggerEnter(Collider other)
    {
        if (isDead) return;
        if (other.CompareTag(projectileTag))
        {
            Destroy(other.gameObject);
            Die();
        }
    }
    public void Die()
    {
        if (isDead) return;
        
        isDead = true;
        audioSource.clip = dieSounds[Random.Range(0, dieSounds.Length)];
        audioSource.Play();
        transform.GetChild(0).localRotation = Quaternion.Euler(180, 0, 90);
        renderer.material = deadMaterial;
        
        // Отключаем перемещение, но оставляем объект в сцене как "труп" (чтобы EatSounds его обнаружил)
        var em = GetComponent<EnemyMovement>();
        if (em != null) em.enabled = false;
        
        // (Опционально) оставляем Collider включённым — EatSounds будет срабатывать по нему.
        // Added: Notify waves of kill
        EnemyWaves.Instance?.IncrementKilledEnemies();
    }
}