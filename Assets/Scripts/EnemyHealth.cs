using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyHealth : MonoBehaviour
{
    [Tooltip("Тег, который использует ваш projectile/pool для пуль")]
    public string projectileTag = "Projectile";

    public int hp = 1;

    private bool isDead = false;

    void OnTriggerEnter(Collider other)
    {
        if (isDead) return;

        if (other.CompareTag(projectileTag))
        {
            // optionally: Destroy projectile
            Destroy(other.gameObject);

            Die();
        }
    }

    // Альтернативно вы можете вызвать этот метод из вашей логики попадания/снаряда.
    public void Die()
    {
        if (isDead) return;
        isDead = true;

        // дать здоровье случайной ветке
        BranchHealthManager.Instance?.GiveHealthToRandomBranch(1);

        // эффект/звук/анимация — по желанию
        Destroy(gameObject);
    }
}