using UnityEngine;

public class ProjectileMovement : MonoBehaviour
{
    [HideInInspector] public float speed = 10f;
    [HideInInspector] public Vector2 direction;
    public float lifetime = 5f;

    void Start()
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }
}