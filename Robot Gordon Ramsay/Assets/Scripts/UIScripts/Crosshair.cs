using UnityEngine;

public class CrosshairCenter : MonoBehaviour
{
    private Camera mainCamera;
    
    private void Start()
    {
        // Get the main camera
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindObjectOfType<Camera>();
    }
    
    /// <summary>
    /// Get the world position of the screen center (crosshair position)
    /// </summary>
    /// <param name="depth">Distance from camera to place the world position</param>
    /// <returns>World position of screen center</returns>
    public Vector3 GetCrosshairWorldPosition(float depth = 2f)
    {
        if (mainCamera == null)
            return Vector3.zero;
            
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, depth);
        return mainCamera.ScreenToWorldPoint(screenCenter);
    }
    
    /// <summary>
    /// Get the screen position of the crosshair (center of screen)
    /// </summary>
    /// <returns>Screen position in pixels</returns>
    public Vector2 GetCrosshairScreenPosition()
    {
        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }
}