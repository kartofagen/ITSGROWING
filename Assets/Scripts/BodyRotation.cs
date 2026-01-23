using UnityEngine;

public class BodyRotation : MonoBehaviour
{
    private bool isTracking = true;
    private float initialOffset;

    public void StartTracking()
    {
        Vector2 cursorDir = GetCursorDirection();
        Vector2 currentDir = transform.right;
        initialOffset = Vector2.SignedAngle(currentDir, cursorDir);
        isTracking = true;
    }

    public void StopTracking()
    {
        isTracking = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            if (isTracking)
            {
                StopTracking();
            }
            else
            {
                StartTracking();
            }

        if (isTracking)
        {
            Vector2 cursorDir = GetCursorDirection();
            float targetAngle = Mathf.Atan2(cursorDir.y, cursorDir.x) * Mathf.Rad2Deg - initialOffset;
            transform.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
        }
    }

    private Vector2 GetCursorDirection()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.forward, 0f);
        plane.Raycast(ray, out float distance);
        Vector3 worldPos = ray.GetPoint(distance);
        Vector2 cursorPos = new Vector2(worldPos.x, worldPos.y);
        Vector2 objPos = new Vector2(transform.position.x, transform.position.y);
        return (cursorPos - objPos).normalized;
    }
}