using UnityEngine;

/// <summary>
/// Freezes look and walking while a mini-game or, later, a game-over screen
/// needs the mouse. Call Suspend, then Resume when that screen closes.
/// </summary>
public static class PlayerControl
{
    public static bool Suspended { get; private set; }

    public static void Suspend()
    {
        Suspended = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void Resume()
    {
        Suspended = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
