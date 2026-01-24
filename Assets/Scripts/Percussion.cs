using UnityEngine;

public class Percussion : InstrumentBase
{
    public Transform target;
    public float speed = 3f;
    void Update()
    {
        if (target != null)
        {
            Vector2 direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);
            transform.Translate(direction * speed * Time.deltaTime, Space.World);
        }
    }
}
