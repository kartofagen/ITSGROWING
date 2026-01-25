using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyHealth : MonoBehaviour
{
    [Tooltip("Тег, который использует ваш projectile/pool для пуль")]
    public string projectileTag = "Projectile";

    private bool isDead = false;

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

        // эффект/звук/анимация — по желанию
        Destroy(gameObject);
    }
}