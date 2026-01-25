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

        // дать здоровье случайной ветке
        BranchHealthManager.Instance?.GiveHealthToWeakestBranch(1);

        // Increment killed count
        EnemyWaves.Instance?.IncrementKilledEnemies();

        audioSource.clip = dieSounds[Random.Range(0, dieSounds.Length)];
        audioSource.Play();
        transform.GetChild(0).localRotation = Quaternion.Euler(180, 0, 90);
        renderer.material = deadMaterial;
        GetComponent<EnemyMovement>().enabled = false;
    }
}