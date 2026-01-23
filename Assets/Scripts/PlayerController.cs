using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float attackRate = 1f;
    [SerializeField] private float attackAngle = 120f;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private float spawnDistance = 1f;
    
    [SerializeField] private Transform body;

    private float nextFireTime = 0f;

    void Update()
    {
        if (Time.time >= nextFireTime)
        {
            Fire();
            nextFireTime = Time.time + attackRate;
        }
    }

    void Fire()
    {
        float randomAngle = Random.Range(-attackAngle / 2f, attackAngle / 2f);
        float spawnAngle = (transform.eulerAngles.z + body.eulerAngles.z + randomAngle) * Mathf.Deg2Rad;
        
        Vector2 spawnPos = (Vector2)transform.position + spawnDistance * new Vector2(Mathf.Cos(spawnAngle), Mathf.Sin(spawnAngle));
        Vector2 direction = new Vector2(Mathf.Cos(spawnAngle), Mathf.Sin(spawnAngle));

        GameObject projectile = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        ProjectileMovement pm = projectile.AddComponent<ProjectileMovement>();
        pm.speed = projectileSpeed;
        pm.direction = direction;
        projectile.tag = "Projectile";
    }
}