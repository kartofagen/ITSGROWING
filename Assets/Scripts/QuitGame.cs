using UnityEngine;

public class QuitGame : MonoBehaviour
{
    // Public function to be called by the button
    public void DoQuit()
    {
        // Quits the application
        Application.Quit();

        // The following line is for testing in the Unity Editor,
        // as Application.Quit() is ignored when running in Play Mode.
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif

        // Optional: Add a log message to confirm the function is called
        Debug.Log("Game is exiting");
    }
}
