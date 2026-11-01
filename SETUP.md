# Play a grey-box level

The player is a capsule with a `CharacterController` and a child camera. Walking, looking, jumping, and sprinting are handled by `FirstPersonController` (`Assets/Scripts/Player/FirstPersonController.cs`).

This project uses the **Input System** package only (Project Settings → Player → Active Input Handling is "Input System Package"). The actions already live in `Assets/InputSystem_Actions`: WASD and arrows, mouse, and a gamepad. Do not switch the project back to the old Input Manager. `Input.GetAxis` will throw.

## Controls

| Action | Keyboard and mouse | Gamepad |
| --- | --- | --- |
| Move | W A S D, or the arrow keys | Left stick |
| Look | Mouse (only while the cursor is locked) | Right stick |
| Jump | Space | South button (A on Xbox, Cross on PlayStation) |
| Sprint | Hold Left Shift | Click the left stick |
| Release the cursor | Escape | Start |
| Lock the cursor again | Click in the Game view | |

The cursor locks and hides when you press Play. Press Escape if you need the mouse back for the editor.

You will not see the capsule from the game camera, because the camera sits inside it. The capsule is visible in the Scene view, which makes the player easy to find.

## 1. Open the scene

1. In the Project window, open `Assets/Scenes/SampleScene`.
2. You should see a Main Camera, a Directional Light, and a Global Volume. Leave the light and the volume. The player's camera replaces the scene camera in the next steps.

## 2. Build a floor, a wall, and a ramp

Use the menu **GameObject → 3D Object**.

**Floor**

1. Create a **Plane**.
2. In the Inspector, set Position to `0, 0, 0` and Scale to `5, 1, 5`. A plane is 10 by 10 units, so this floor is 50 by 50 meters. The top of the plane is at y = 0.

**Wall**

1. Create a **Cube**.
2. Name it `Wall`.
3. Set Position to `0, 1, 6` and Scale to `6, 2, 1`. The cube is 2 meters tall and stands on the floor, a short walk in front of the player.

**Ramp**

1. Create another **Cube**.
2. Name it `Ramp`.
3. Set Position to `4, 0.5, 2`, Rotation to `25, 0, 0`, and Scale to `3, 0.3, 4`.
4. Keep the ramp's X rotation under 45 degrees. The player's CharacterController will not walk up anything steeper than its Slope Limit (45).

Cubes and planes already have colliders. Do not add a Rigidbody to them.

## 3. Add the player

1. Delete the scene's **Main Camera**. The prefab brings its own camera and audio listener. Two listeners print a warning, and two cameras fight over the view.
2. Drag `Assets/Prefabs/Player` into the Hierarchy.
3. Set the Player position to `0, 0, 0`. The capsule's pivot is at its feet, so y = 0 stands it on the floor. If it ever starts stuck in the ground, nudge it up to `0, 0.1, 0`.
4. Select Player and check the Inspector:
   - **Character Controller**: Height `2`, Radius `0.5`, Center Y `1`. The green capsule outline should sit on the floor, not halfway through it.
   - **First Person Controller**: Input Actions points at `InputSystem_Actions`. View Camera points at the child **Main Camera**. If Input Actions says None, drag `Assets/InputSystem_Actions` from the Project window into that slot.
5. Expand Player. **Body** is the visible capsule. **Main Camera** is at local position `0, 1.6, 0` (eye height) and is tagged MainCamera.

## 4. Press Play

1. Click **Play**.
2. Click the **Game** view once so it has keyboard focus.
3. Walk with WASD. The mouse looks around: left and right turn the body, up and down tilt the camera and stop near straight up and straight down.
4. Space jumps. Hold Left Shift to sprint. Walk into the wall (you should stop) and up the ramp (you should climb it).
5. Press **Escape** to free the cursor. Click the Game view to lock it again.
6. Press **Play** again to leave play mode.

If a gamepad is connected, the left stick moves, the right stick looks, the south button jumps, and clicking the left stick sprints. That comes from the same Input Actions asset. Nothing extra is required.

## Tunables

Select the Player while not in play mode. On **First Person Controller**:

| Field | Default | What it does |
| --- | --- | --- |
| Move Speed | 5 | Walk speed, meters per second |
| Sprint Speed | 8 | Speed while Sprint is held |
| Jump Height | 1.2 | How high a jump reaches, in meters |
| Gravity | 20 | Downward pull. 9.81 is Earth. Higher feels snappier |
| Mouse Sensitivity | 0.1 | Degrees turned per pixel of mouse movement |
| Stick Look Speed | 200 | Degrees per second at full stick tilt. The mouse ignores this |
| Min Pitch / Max Pitch | -85 / 85 | How far down and up the camera can look |
| Lock Cursor On Play | on | Uncheck this if you want the mouse free until you click the Game view |

Changes made during Play are discarded when you stop, unless you change them on the prefab itself (open `Assets/Prefabs/Player`).

## Build the player without the prefab

Use this if the prefab did not import cleanly.

1. Create an empty GameObject named `Player` at `0, 0, 0`. Set its tag to **Player**.
2. Add a **Character Controller**. Set Height `2`, Radius `0.5`, Center `0, 1, 0`, Step Offset `0.3`, Slope Limit `45`.
3. Add **First Person Controller** (`Add Component` and search for that name). Adding it also sets those Character Controller values.
4. Drag `Assets/InputSystem_Actions` onto **Input Actions**.
5. Right-click Player → **3D Object → Capsule**. Name it `Body`. Set its local position to `0, 1, 0` and its scale to `1, 1, 1`. Remove the Capsule Collider on Body (the Character Controller is the collider). The URP Lit material is fine if the capsule shows as magenta: assign any lit material.
6. Right-click Player → **Camera**. Name it `Main Camera`, tag it **MainCamera**, and set local position to `0, 1.6, 0`. Delete the scene's original Main Camera so only one Audio Listener remains.
7. Drag that camera onto the script's **View Camera** field. If you leave the field empty, the script uses a camera parented under the player.
8. Press Play and use the checks in section 4.

## If something is wrong

- **The player falls forever.** The floor needs a collider (a new Plane has one), and the player needs a Character Controller. A Rigidbody on the player fights the controller. Remove it.
- **The player will not walk up the ramp.** The ramp is steeper than 45 degrees, or its collider was removed.
- **WASD does nothing, and the Console mentions Input Actions.** Assign `Assets/InputSystem_Actions` on the script. Click the Game view so it has focus.
- **The mouse does not look.** The cursor is unlocked. Click the Game view. Escape unlocks it on purpose.
- **The Console says there are 2 audio listeners.** Delete the extra Main Camera.
- **The view spins the instant you press Play.** It should not. If it does, press Escape and click the Game view once. Mouse Look is ignored for the frame the cursor locks, which is when Unity sometimes reports a huge mouse jump.
