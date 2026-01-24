using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    public Transform target;
    public float moveDuration = 0.5f;
    public float moveDistance = 0.5f;
    
    private float moveStartTime = -1f;
    private Vector2 moveStartPosition;
    private Vector2 moveTargetPosition;
    private bool isMoving = false;
    
    void Update()
    {
        if (isMoving)
        {
            float t = (Time.time - moveStartTime) / moveDuration;
            
            if (t >= 1f)
            {
                transform.position = moveTargetPosition;
                isMoving = false;
            }
            else
            {
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                transform.position = Vector2.Lerp(moveStartPosition, moveTargetPosition, smoothT);
            }
        }
        
        if (target != null)
        {
            Vector2 direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }
    
    public void MoveOneStep()
    {
        if (target != null && !isMoving)
        {
            Vector2 direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
            moveStartPosition = transform.position;
            moveTargetPosition = (Vector2)transform.position + direction * moveDistance;
            moveStartTime = Time.time;
            isMoving = true;
        }
    }
}