using UnityEngine;

/// <summary>
/// Freezes look and walking while a mini-game or, later, a game-over screen
/// needs the mouse. Call Suspend, then Resume when that screen closes.
/// </summary>
public static class PlayerControl
{
    public static bool Suspended { get; private set; }

    /// <summary>
    /// freeCursor is false for the jumpscare, which still owns the camera.
    /// The game-over menu unlocks the cursor itself.
    /// </summary>
    public static void Suspend(bool freeCursor = true)
    {
        Suspended = true;
        if (!freeCursor)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Static flags survive a scene reload. Call this before loading the basement again.
    /// </summary>
    public static void ResetForSceneLoad()
    {
        Suspended = false;
    }

    public static void Resume()
    {
        Suspended = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
