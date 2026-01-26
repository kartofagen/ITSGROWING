using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FollowCursorWithFixedZ : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float fixedZPosition = 10f;
    [SerializeField] private Animator cameraAnimator;

    private bool enterGame = false;
    
    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }
    
    void Update()
    {
        if (enterGame)
        {
            return;
        }
        
        Vector3 mousePosition = Input.mousePosition;
        
        mousePosition.z = fixedZPosition - mainCamera.transform.position.z;
        
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(mousePosition);
        
        transform.position = worldPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Mushroom"))
        {
            enterGame = true;
            cameraAnimator.SetTrigger("StartGame");
            StartCoroutine(StartGame());
        }
    }
    
    private IEnumerator StartGame()
    {
        yield return new WaitForSeconds(3f);

        SceneManager.LoadScene("Scenes/Main");
    }
}