using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the grey-box basement from scratch.
/// Menu: Tools > Gray Box Galore > Build Basement Scene.
/// Running it again overwrites Assets/Scenes/Basement.unity.
/// </summary>
public static class BasementSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Basement.unity";
    const string MaterialFolder = "Assets/Materials";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    const string VolumeProfilePath = "Assets/Settings/SampleSceneProfile.asset";
    const string AudioFolder = "Assets/Audio";

    // Interior size. The walkable floor is this wide and deep. Walls sit just outside it.
    const float HalfWidth = 6f;
    const float HalfDepth = 8f;
    const float WallHeight = 3.9f;
    const float Thick = 0.25f;

    // Stairs climb toward -Z (the south wall) and stop at a landing with a sealed door.
    const float StairRise = 1.55f;
    const float StairRun = 3.05f;
    const float StairLowZ = -3.15f;
    const float StairHalfWidth = 1.25f;

    [MenuItem("Tools/Gray Box Galore/Build Basement Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit Play mode, then run Tools > Gray Box Galore > Build Basement Scene again.");
            return;
        }

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (lit == null || playerPrefab == null || actions == null)
        {
            Debug.LogError(
                "Could not build the basement. Need the URP Lit shader, " + PlayerPrefabPath + ", and " + InputActionsPath + ".");
            return;
        }

        // Ask before throwing away unsaved work in the open scene. Cancel leaves everything as it was.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder(MaterialFolder);
        Palette palette = CreateMaterials(lit);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ApplyDarkMood();

        Transform structure = Group("Structure");
        Transform props = Group("Props");
        Transform jail = Group("Jail");
        Transform stairs = Group("Stairs");
        Transform lighting = Group("Lighting");

        BuildStructure(structure, palette);
        Transform furnace = BuildProps(props, palette);
        BuildJail(jail, palette);
        BuildFlashlightPickup(jail, palette);
        BuildStairs(stairs, palette);
        BuildLighting(lighting, palette);
        GameObject player = PlacePlayer(playerPrefab, actions);
        AttachFlashlight(player, palette);
        player.AddComponent<Inventory>();
        player.AddComponent<InventoryHud>();
        player.AddComponent<HookMinigame>();
        player.AddComponent<CarriedFire>();
        AxeSwing axe = player.AddComponent<AxeSwing>();
        SerializedObject axeObject = new SerializedObject(axe);
        axeObject.FindProperty("handleMaterial").objectReferenceValue = palette.wood;
        axeObject.FindProperty("headMaterial").objectReferenceValue = palette.metal;
        axeObject.ApplyModifiedPropertiesWithoutUndo();
        BuildInteractionUi(player, actions);
        WireThreat(player, lit);
        BuildAudio(furnace);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        if (!saved)
        {
            Debug.LogError("The basement was built in memory but could not be saved to " + ScenePath + ".");
            return;
        }

        EnsureSceneInBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Selection.activeObject = sceneAsset;
        EditorGUIUtility.PingObject(sceneAsset);

        Debug.Log("Built " + ScenePath + " and added it to Build Settings. Press Play to walk the basement.");
        EditorUtility.DisplayDialog(
            "Basement built",
            "Assets/Scenes/Basement is open.\n\nYou start inside the jail. Hold E on the loose floorboard five times, then follow SETUP.md to get out.\n\nThe stair door at the top of the stairs is still sealed.\n\nSounds are included. Master volume is on the Audio object.",
            "OK");
    }

    static void ApplyDarkMood()
    {
        // Flat ambient, no skybox, and no directional sun. The only real light is the
        // handful of bulbs created below. A little fog keeps the far wall from reading as a clean studio.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.025f, 0.022f, 0.02f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.subtractiveShadowColor = Color.black;
        RenderSettings.reflectionIntensity = 0.04f;
        RenderSettings.skybox = null;
        RenderSettings.sun = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.018f, 0.016f, 0.016f);
        RenderSettings.fogDensity = 0.04f;
    }

    static void BuildStructure(Transform parent, Palette palette)
    {
        float floorWidth = HalfWidth * 2f + Thick * 2f;
        float floorDepth = HalfDepth * 2f + Thick * 2f;

        // Top of the solid floor sits just under the visual boards (boards are about 3cm tall).
        Box(parent, "Floor", new Vector3(0f, -Thick * 0.5f + 0.03f, 0f), new Vector3(floorWidth, Thick, floorDepth), palette.floor);
        BuildFloorboards(parent, palette);
        Box(parent, "Ceiling", new Vector3(0f, WallHeight + Thick * 0.5f, 0f), new Vector3(floorWidth, Thick, floorDepth), palette.ceiling);

        float wallY = WallHeight * 0.5f;
        Box(parent, "WallNorth", new Vector3(0f, wallY, HalfDepth + Thick * 0.5f), new Vector3(floorWidth, WallHeight, Thick), palette.wall);
        Box(parent, "WallSouth", new Vector3(0f, wallY, -(HalfDepth + Thick * 0.5f)), new Vector3(floorWidth, WallHeight, Thick), palette.wall);
        Box(parent, "WallEast", new Vector3(HalfWidth + Thick * 0.5f, wallY, 0f), new Vector3(Thick, WallHeight, floorDepth), palette.wall);
        Box(parent, "WallWest", new Vector3(-(HalfWidth + Thick * 0.5f), wallY, 0f), new Vector3(Thick, WallHeight, floorDepth), palette.wall);

        // Pipes under the ceiling. Colliders are removed so a jump cannot snag on them.
        Cylinder(parent, "PipeA", new Vector3(-2.2f, WallHeight - 0.28f, 0.4f), new Vector3(0.08f, HalfDepth * 0.85f, 0.08f), palette.metal, Quaternion.Euler(90f, 0f, 0f), false);
        Cylinder(parent, "PipeB", new Vector3(1.4f, WallHeight - 0.42f, -1.2f), new Vector3(0.06f, HalfWidth * 0.7f, 0.06f), palette.metal, Quaternion.Euler(0f, 0f, 90f), false);
    }

    static Transform BuildProps(Transform parent, Palette palette)
    {
        Transform furnace = BuildFurnace(parent, palette, new Vector3(-4.65f, 0f, 1.9f));
        // Rotated 90 degrees so the long side runs away from the bars and the
        // doorway stays clear. The near end is still a short reach through the bars.
        Transform bench = BuildWorkbench(parent, palette, new Vector3(2.4f, 0f, 4.1f));
        bench.rotation = Quaternion.Euler(0f, 90f, 0f);
        BuildJailKey(parent, palette, bench.TransformPoint(new Vector3(-0.5f, 1.02f, 0.05f)));
        BuildCloset(parent, palette);
        BuildPaintBucket(parent, palette, new Vector3(-3.4f, 0f, 0.4f));
        // Flush to the west wall so it can be hidden under later. The old spot sat in the open floor.
        BuildBed(parent, palette, new Vector3(-5.42f, 0f, -4.2f));
        Transform shelf = BuildShelf(parent, palette, new Vector3(-5.78f, 0f, 3.45f));
        shelf.rotation = Quaternion.Euler(0f, 90f, 0f);
        MakeAxeBreakable(BuildCrateStack(parent, palette, "CratesWest", new Vector3(-3.7f, 0f, 2.4f)));
        MakeAxeBreakable(BuildCrateStack(parent, palette, "CratesEast", new Vector3(3.6f, 0f, 2.1f)));

        // Water heater in the west corner, clear of the stair opening.
        Transform heater = new GameObject("WaterHeater").transform;
        heater.SetParent(parent, false);
        heater.position = new Vector3(-4.7f, 0f, -2.15f);
        GameObject tank = Cylinder(heater, "Tank", new Vector3(0f, 0.7f, 0f), new Vector3(0.7f, 0.7f, 0.7f), palette.metal);
        SetInteractable(tank, "Heater", "The tank is cold. It hasn't been lit in a long time.");
        Cylinder(heater, "Vent", new Vector3(0f, 1.55f, 0f), new Vector3(0.12f, 0.2f, 0.12f), palette.rust);
        return furnace;
    }

    static Transform BuildFurnace(Transform parent, Palette palette, Vector3 origin)
    {
        Transform root = new GameObject("Furnace").transform;
        root.SetParent(parent, false);
        root.position = origin;

        GameObject furnaceBody = Box(root, "Body", new Vector3(0f, 0.7f, 0f), new Vector3(1.15f, 1.4f, 0.85f), palette.rust);
        SetInteractable(furnaceBody, "Furnace", "");
        FurnaceFire furnaceFire = furnaceBody.AddComponent<FurnaceFire>();
        UnityEventTools.AddPersistentListener(furnaceBody.GetComponent<Interactable>().OnInteract, furnaceFire.TryLight);
        GameObject furnaceDoor = Box(root, "Door", new Vector3(0.58f, 0.55f, 0f), new Vector3(0.06f, 0.7f, 0.5f), palette.metal);
        SetInteractable(furnaceDoor, "Furnace", "");
        UnityEventTools.AddPersistentListener(furnaceDoor.GetComponent<Interactable>().OnInteract, furnaceFire.TryLight);
        Box(root, "Mouth", new Vector3(0.62f, 0.48f, 0f), new Vector3(0.04f, 0.28f, 0.22f), palette.ember, false);
        Cylinder(root, "Flue", new Vector3(-0.15f, 1.7f, -0.05f), new Vector3(0.18f, 0.45f, 0.18f), palette.metal);
        // Continues up into the ceiling so the furnace is tied to the room, not floating.
        Cylinder(root, "CeilingDuct", new Vector3(-0.15f, 3.05f, -0.05f), new Vector3(0.14f, 0.95f, 0.14f), palette.metal);
        return root;
    }

    static Transform BuildWorkbench(Transform parent, Palette palette, Vector3 origin)
    {
        Transform root = new GameObject("Workbench").transform;
        root.SetParent(parent, false);
        root.position = origin;

        Box(root, "Top", new Vector3(0f, 0.92f, 0f), new Vector3(1.7f, 0.08f, 0.7f), palette.wood);
        Box(root, "LegA", new Vector3(-0.75f, 0.44f, -0.26f), new Vector3(0.08f, 0.88f, 0.08f), palette.wood);
        Box(root, "LegB", new Vector3(0.75f, 0.44f, -0.26f), new Vector3(0.08f, 0.88f, 0.08f), palette.wood);
        Box(root, "LegC", new Vector3(-0.75f, 0.44f, 0.26f), new Vector3(0.08f, 0.88f, 0.08f), palette.wood);
        Box(root, "LegD", new Vector3(0.75f, 0.44f, 0.26f), new Vector3(0.08f, 0.88f, 0.08f), palette.wood);

        // Hammer: a thin handle and a head, lying on the tabletop (top is at y = 0.96).
        Cylinder(root, "HammerHandle", new Vector3(-0.35f, 1.02f, 0.05f), new Vector3(0.04f, 0.16f, 0.04f), palette.wood, Quaternion.Euler(0f, 0f, 80f), false);
        Box(root, "HammerHead", new Vector3(-0.18f, 1.04f, 0.05f), new Vector3(0.16f, 0.07f, 0.07f), palette.metal, false);

        // Wrench: a shaft and an open jaw.
        Box(root, "WrenchShaft", new Vector3(0.28f, 1.01f, -0.08f), new Vector3(0.34f, 0.03f, 0.06f), palette.metal, false);
        Box(root, "WrenchJawA", new Vector3(0.46f, 1.02f, -0.02f), new Vector3(0.08f, 0.04f, 0.04f), palette.metal, false);
        Box(root, "WrenchJawB", new Vector3(0.46f, 1.02f, -0.14f), new Vector3(0.08f, 0.04f, 0.04f), palette.metal, false);
        return root;
    }

    static void BuildCloset(Transform parent, Palette palette)
    {
        // Back face sits on the east wall. Yaw -90 turns the doors toward the room.
        Transform root = new GameObject("Closet").transform;
        root.SetParent(parent, false);
        root.position = new Vector3(HalfWidth - 0.3f, 0f, 1.6f);
        root.rotation = Quaternion.Euler(0f, -90f, 0f);

        // Open box of panels. A solid carcass hid the axe inside the mesh even after the doors opened.
        Box(root, "Back", new Vector3(0f, 1.1f, -0.26f), new Vector3(1.2f, 2.2f, 0.06f), palette.wood, false);
        Box(root, "SideLeft", new Vector3(-0.57f, 1.1f, 0f), new Vector3(0.06f, 2.2f, 0.5f), palette.wood, false);
        Box(root, "SideRight", new Vector3(0.57f, 1.1f, 0f), new Vector3(0.06f, 2.2f, 0.5f), palette.wood, false);
        Box(root, "Top", new Vector3(0f, 2.16f, 0f), new Vector3(1.2f, 0.08f, 0.52f), palette.wood, false);
        Box(root, "Floor", new Vector3(0f, 0.06f, 0f), new Vector3(1.12f, 0.06f, 0.5f), palette.wood, false);

        Transform hingeLeft = new GameObject("HingeLeft").transform;
        hingeLeft.SetParent(root, false);
        hingeLeft.localPosition = new Vector3(-0.58f, 0f, 0.3f);
        Box(hingeLeft, "Door", new Vector3(0.28f, 1.05f, 0f), new Vector3(0.56f, 2.0f, 0.04f), palette.wall);
        Box(hingeLeft, "Knob", new Vector3(0.5f, 1.05f, 0.03f), new Vector3(0.04f, 0.04f, 0.04f), palette.brass, false);

        Transform hingeRight = new GameObject("HingeRight").transform;
        hingeRight.SetParent(root, false);
        hingeRight.localPosition = new Vector3(0.58f, 0f, 0.3f);
        Box(hingeRight, "Door", new Vector3(-0.28f, 1.05f, 0f), new Vector3(0.56f, 2.0f, 0.04f), palette.wall);
        Box(hingeRight, "Knob", new Vector3(-0.5f, 1.05f, 0.03f), new Vector3(0.04f, 0.04f, 0.04f), palette.brass, false);

        ClosetLock closet = root.gameObject.AddComponent<ClosetLock>();
        SerializedObject closetObject = new SerializedObject(closet);
        closetObject.FindProperty("hingeLeft").objectReferenceValue = hingeLeft;
        closetObject.FindProperty("hingeRight").objectReferenceValue = hingeRight;
        closetObject.ApplyModifiedPropertiesWithoutUndo();

        // The left door carries the "it's locked" interaction. Its collider blocks the axe until the door swings.
        SetInteractable(hingeLeft.Find("Door").gameObject, "Closet", "");
        UnityEventTools.AddPersistentListener(hingeLeft.Find("Door").GetComponent<Interactable>().OnInteract, closet.TryOpen);
        SetInteractable(hingeRight.Find("Door").gameObject, "Closet", "");
        UnityEventTools.AddPersistentListener(hingeRight.Find("Door").GetComponent<Interactable>().OnInteract, closet.TryOpen);

        // Upright in the open front of the closet, blade toward the room (+Z). A small light so it reads in the dark.
        Transform axe = new GameObject("Axe").transform;
        axe.SetParent(root, false);
        axe.localPosition = new Vector3(0.05f, 0.72f, 0.16f);
        axe.localRotation = Quaternion.Euler(8f, 0f, 12f);
        Cylinder(axe, "Handle", new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.32f, 0.05f), palette.wood, Quaternion.identity, false);
        Box(axe, "Head", new Vector3(0f, 0.28f, 0.02f), new Vector3(0.06f, 0.08f, 0.2f), palette.metal, false);
        Box(axe, "Blade", new Vector3(0f, 0.28f, 0.13f), new Vector3(0.02f, 0.14f, 0.08f), palette.metal, false);
        GameObject axeLightObject = new GameObject("AxeLight");
        axeLightObject.transform.SetParent(root, false);
        axeLightObject.transform.localPosition = new Vector3(0f, 1.35f, 0.2f);
        Light axeLight = axeLightObject.AddComponent<Light>();
        axeLight.type = LightType.Point;
        axeLight.color = new Color(1f, 0.82f, 0.55f);
        axeLight.useColorTemperature = false;
        axeLight.intensity = 0.7f;
        axeLight.range = 2.2f;
        axeLight.shadows = LightShadows.None;
        BoxCollider axeCollider = axe.gameObject.AddComponent<BoxCollider>();
        axeCollider.center = new Vector3(0.04f, 0.08f, 0f);
        axeCollider.size = new Vector3(0.34f, 0.5f, 0.14f);
        SetInteractable(axe.gameObject, "Axe", "An axe. Heavy enough to break something.");
        AddItemPickup(axe.gameObject, ItemId.Axe, "I already have the axe.");
    }

    static void BuildBed(Transform parent, Palette palette, Vector3 origin)
    {
        Transform root = new GameObject("Bed").transform;
        root.SetParent(parent, false);
        root.position = origin;

        // Legs and a rail, not a solid block, so there is a gap to crawl into.
        // The collider on the root still stops you walking through the bed.
        Box(root, "LegA", new Vector3(-0.48f, 0.17f, -0.88f), new Vector3(0.08f, 0.34f, 0.08f), palette.wood, false);
        Box(root, "LegB", new Vector3(0.48f, 0.17f, -0.88f), new Vector3(0.08f, 0.34f, 0.08f), palette.wood, false);
        Box(root, "LegC", new Vector3(-0.48f, 0.17f, 0.88f), new Vector3(0.08f, 0.34f, 0.08f), palette.wood, false);
        Box(root, "LegD", new Vector3(0.48f, 0.17f, 0.88f), new Vector3(0.08f, 0.34f, 0.08f), palette.wood, false);
        Box(root, "Rail", new Vector3(0f, 0.36f, 0f), new Vector3(1.08f, 0.06f, 1.95f), palette.wood, false);
        Box(root, "Mattress", new Vector3(0f, 0.5f, 0.02f), new Vector3(1.05f, 0.18f, 1.9f), palette.fabric);
        Box(root, "Pillow", new Vector3(0f, 0.62f, 0.78f), new Vector3(0.55f, 0.12f, 0.32f), palette.fabric);
        Box(root, "Headboard", new Vector3(0f, 0.7f, 1.0f), new Vector3(1.15f, 0.7f, 0.08f), palette.wood);
        SetInteractable(root.gameObject, "Bed", "The mattress smells like dust and old rain.");
        BoxCollider bedCollider = root.gameObject.AddComponent<BoxCollider>();
        bedCollider.center = new Vector3(0f, 0.4f, 0f);
        bedCollider.size = new Vector3(1.15f, 0.8f, 2.05f);
    }

    static Transform BuildShelf(Transform parent, Palette palette, Vector3 origin)
    {
        Transform root = new GameObject("Shelf").transform;
        root.SetParent(parent, false);
        root.position = origin;

        Box(root, "SideL", new Vector3(-0.55f, 0.9f, 0f), new Vector3(0.06f, 1.8f, 0.4f), palette.wood);
        Box(root, "SideR", new Vector3(0.55f, 0.9f, 0f), new Vector3(0.06f, 1.8f, 0.4f), palette.wood);
        Box(root, "Board1", new Vector3(0f, 0.45f, 0f), new Vector3(1.1f, 0.05f, 0.38f), palette.wood);
        Box(root, "Board2", new Vector3(0f, 1.0f, 0f), new Vector3(1.1f, 0.05f, 0.38f), palette.wood);
        Box(root, "Board3", new Vector3(0f, 1.55f, 0f), new Vector3(1.1f, 0.05f, 0.38f), palette.wood);
        Box(root, "Box", new Vector3(-0.2f, 1.14f, 0f), new Vector3(0.22f, 0.18f, 0.22f), palette.wall);
        Cylinder(root, "Can", new Vector3(0.22f, 0.56f, 0f), new Vector3(0.1f, 0.08f, 0.1f), palette.metal);
        return root;
    }

    static GameObject BuildCrateStack(Transform parent, Palette palette, string name, Vector3 origin)
    {
        Transform root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.position = origin;

        Box(root, "CrateA", new Vector3(0f, 0.28f, 0f), new Vector3(0.6f, 0.55f, 0.6f), palette.wood);
        Box(root, "CrateB", new Vector3(0.1f, 0.78f, 0.05f), new Vector3(0.45f, 0.45f, 0.45f), palette.wood);
        return root.gameObject;
    }

    static void MakeAxeBreakable(GameObject target)
    {
        Damageable damageable = target.AddComponent<Damageable>();
        SerializedObject serialized = new SerializedObject(damageable);
        serialized.FindProperty("hitsToBreak").intValue = 2;
        serialized.FindProperty("hitMessage").stringValue = "The crate splits.";
        serialized.FindProperty("breakMessage").stringValue = "The crate falls to pieces.";
        serialized.FindProperty("disableOnBreak").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildJail(Transform parent, Palette palette)
    {
        // The cell is the strip between the bars (z = 5.15) and the north wall (z = 8).
        // Blockers stop the body. They are on Ignore Raycast so a look-ray still
        // reaches the table and the key on the far side of the bars.
        float barZ = 5.15f;
        float doorHalf = 0.7f;
        float y = WallHeight * 0.5f;

        Blocker(parent, "BarBlockerLeft", new Vector3(-(HalfWidth + doorHalf) * 0.5f, y, barZ), new Vector3(HalfWidth - doorHalf, WallHeight, 0.14f), true);
        Blocker(parent, "BarBlockerRight", new Vector3((HalfWidth + doorHalf) * 0.5f, y, barZ), new Vector3(HalfWidth - doorHalf, WallHeight, 0.14f), true);

        Transform door = new GameObject("JailDoor").transform;
        door.SetParent(parent, false);
        Blocker(door, "DoorBlocker", new Vector3(0f, y, barZ), new Vector3(doorHalf * 2f, WallHeight, 0.14f), true);
        JailDoor jailDoor = door.gameObject.AddComponent<JailDoor>();
        // Sits on the door and slides aside with it. Disabled while shut so rays
        // can still reach the table. Enabled once the door is open, beside the gap.
        GameObject handle = Box(door, "DoorHandle", new Vector3(0.45f, 1.15f, barZ), new Vector3(0.16f, 0.35f, 0.18f), palette.metal);
        Collider handleCollider = handle.GetComponent<Collider>();
        handleCollider.enabled = false;
        SetInteractable(handle, "Bars", "");
        SerializedObject doorObject = new SerializedObject(jailDoor);
        doorObject.FindProperty("handleCollider").objectReferenceValue = handleCollider;
        doorObject.ApplyModifiedPropertiesWithoutUndo();

        float barHeightScale = WallHeight * 0.5f;
        for (float x = -HalfWidth + 0.35f; x <= HalfWidth - 0.3f; x += 0.34f)
        {
            bool isDoor = Mathf.Abs(x) < doorHalf - 0.05f;
            Transform barParent = isDoor ? door : parent;
            Cylinder(barParent, isDoor ? "DoorBar" : "Bar", new Vector3(x, y, barZ), new Vector3(0.07f, barHeightScale, 0.07f), palette.metal, Quaternion.identity, false);
        }

        Transform lockRoot = new GameObject("Lock").transform;
        lockRoot.SetParent(parent, false);
        lockRoot.position = new Vector3(0.15f, 1.2f, barZ - 0.4f);
        Box(lockRoot, "Body", Vector3.zero, new Vector3(0.16f, 0.22f, 0.1f), palette.brass);
        Box(lockRoot, "Shackle", new Vector3(0f, 0.14f, 0f), new Vector3(0.12f, 0.08f, 0.06f), palette.metal);
        BoxCollider lockCollider = lockRoot.gameObject.AddComponent<BoxCollider>();
        lockCollider.center = new Vector3(0f, 0.04f, 0f);
        lockCollider.size = new Vector3(0.36f, 0.5f, 0.3f);
        SetInteractable(lockRoot.gameObject, "Lock", "");
        JailLock jailLock = lockRoot.gameObject.AddComponent<JailLock>();
        SerializedObject lockObject = new SerializedObject(jailLock);
        lockObject.FindProperty("door").objectReferenceValue = jailDoor;
        lockObject.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(lockRoot.GetComponent<Interactable>().OnInteract, jailLock.TryOpen);
        SerializedObject doorLink = new SerializedObject(jailDoor);
        doorLink.FindProperty("lockPiece").objectReferenceValue = jailLock;
        doorLink.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(handle.GetComponent<Interactable>().OnInteract, jailDoor.RequestClose);

        Cylinder(parent, "Bucket", new Vector3(2.3f, 0.18f, 7.2f), new Vector3(0.32f, 0.16f, 0.32f), palette.metal);

        BuildSmashCrate(parent, palette);
    }

    static void BuildFloorboards(Transform parent, Palette palette)
    {
        // Short planks with a seam on every side. The one that can be pried is the
        // same size, height, and material as the rest, and it does not announce itself.
        Transform boards = new GameObject("Floorboards").transform;
        boards.SetParent(parent, false);

        const float width = 0.26f;
        const float length = 1.7f;
        const float boardY = 0.016f;
        int xCount = Mathf.Max(1, Mathf.FloorToInt((HalfWidth * 2f - 0.12f) / width));
        int zCount = Mathf.Max(1, Mathf.FloorToInt((HalfDepth * 2f - 0.12f) / length));
        float startX = -xCount * width * 0.5f + width * 0.5f;
        float startZ = -zCount * length * 0.5f + length * 0.5f;
        Vector3 size = new Vector3(width - 0.02f, 0.03f, length - 0.035f);
        Vector3 pryTarget = new Vector3(0.45f, boardY, 6.45f);
        GameObject pryBoard = null;
        float best = float.MaxValue;

        for (int ix = 0; ix < xCount; ix++)
        {
            for (int iz = 0; iz < zCount; iz++)
            {
                Vector3 position = new Vector3(startX + ix * width, boardY, startZ + iz * length);
                GameObject board = Box(boards, "Board_" + ix + "_" + iz, position, size, palette.wood, false);
                bool insideCell = position.z > 5.55f && position.z < 7.55f && Mathf.Abs(position.x) < 1.6f;
                float distance = (position - pryTarget).sqrMagnitude;
                if (insideCell && distance < best)
                {
                    best = distance;
                    pryBoard = board;
                }
            }
        }

        if (pryBoard != null)
            MakePryBoard(parent, palette, pryBoard);
    }

    static void MakePryBoard(Transform keyParent, Palette palette, GameObject visual)
    {
        Transform root = new GameObject("LooseFloorboard").transform;
        root.SetParent(visual.transform.parent, false);
        root.position = visual.transform.position;
        root.rotation = visual.transform.rotation;
        visual.transform.SetParent(root, true);
        // One end sits a few centimetres proud of the other boards. Same mesh, just disturbed.
        root.position += new Vector3(0.02f, 0.02f, 0f);
        root.rotation = Quaternion.Euler(4f, 7f, 1.5f);
        visual.AddComponent<BoxCollider>();
        root.gameObject.AddComponent<LookHighlight>();

        SetInteractable(root.gameObject, "", "");
        SerializedObject prompt = new SerializedObject(root.GetComponent<Interactable>());
        prompt.FindProperty("showPrompt").boolValue = false;
        prompt.ApplyModifiedPropertiesWithoutUndo();

        HoldInteractable hold = root.gameObject.AddComponent<HoldInteractable>();
        PryFloorboard pry = root.gameObject.AddComponent<PryFloorboard>();

        Vector3 keyPosition = root.position;
        keyPosition.y = 0.05f;
        Transform hiddenKey = BuildKeyObject(keyParent, "ClosetKey", keyPosition, palette.brass);
        BoxCollider hiddenCollider = hiddenKey.GetComponent<BoxCollider>();
        hiddenCollider.center = new Vector3(0.04f, 0.04f, 0f);
        hiddenCollider.size = new Vector3(0.34f, 0.14f, 0.2f);
        hiddenKey.gameObject.SetActive(false);
        SetInteractable(hiddenKey.gameObject, "Key", "A small key.");
        AddItemPickup(hiddenKey.gameObject, ItemId.ClosetKey, "I already have the key.");

        SerializedObject pryObject = new SerializedObject(pry);
        pryObject.FindProperty("hold").objectReferenceValue = hold;
        pryObject.FindProperty("boardVisual").objectReferenceValue = visual;
        pryObject.FindProperty("hiddenKey").objectReferenceValue = hiddenKey.gameObject;
        pryObject.FindProperty("boardCollider").objectReferenceValue = visual.GetComponent<Collider>();
        pryObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildJailKey(Transform parent, Palette palette, Vector3 onTable)
    {
        Transform key = BuildKeyObject(parent, "JailKey", onTable, palette.brass);
        BoxCollider keyCollider = key.GetComponent<BoxCollider>();
        keyCollider.center = new Vector3(0.04f, 0.06f, 0f);
        keyCollider.size = new Vector3(0.42f, 0.22f, 0.32f);

        SetInteractable(key.gameObject, "Jail key", "");
        JailKeyPuzzle puzzle = key.gameObject.AddComponent<JailKeyPuzzle>();
        SerializedObject puzzleObject = new SerializedObject(puzzle);
        puzzleObject.FindProperty("nearBars").vector3Value = new Vector3(2.4f, 0.045f, 5.02f);
        puzzleObject.FindProperty("pushedAway").vector3Value = new Vector3(2.2f, 0.045f, 2.9f);
        puzzleObject.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(key.GetComponent<Interactable>().OnInteract, puzzle.Use);
    }

    static void BuildPaintBucket(Transform parent, Palette palette, Vector3 origin)
    {
        Transform root = new GameObject("PaintBucket").transform;
        root.SetParent(parent, false);
        root.position = origin;
        GameObject bucket = Cylinder(root, "Bucket", new Vector3(0f, 0.16f, 0f), new Vector3(0.34f, 0.16f, 0.34f), palette.metal);
        Cylinder(root, "Paint", new Vector3(0f, 0.31f, 0f), new Vector3(0.28f, 0.015f, 0.28f), palette.paint, Quaternion.identity, false);
        SetInteractable(bucket, "Bucket", "It's a bucket of paint. I think it's a muave colour.");
    }

    static void BuildSmashCrate(Transform parent, Palette palette)
    {
        Vector3 origin = new Vector3(-1.35f, 0.28f, 5.9f);
        GameObject crate = Box(parent, "JailCrate", origin, new Vector3(0.55f, 0.55f, 0.55f), palette.wood);
        SetInteractable(crate, "Box", "");
        SmashableCrate smash = crate.AddComponent<SmashableCrate>();

        Transform splinters = new GameObject("Splinters").transform;
        splinters.SetParent(parent, false);
        splinters.gameObject.SetActive(false);
        Vector3[] offsets =
        {
            new Vector3(-0.28f, -0.2f, 0.22f),
            new Vector3(0.24f, -0.2f, -0.12f),
            new Vector3(0.02f, -0.2f, 0.3f),
            new Vector3(-0.08f, -0.2f, -0.26f)
        };
        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject piece = Box(splinters, "Splinter" + (i + 1), origin + offsets[i], new Vector3(0.2f, 0.04f, 0.07f), palette.wood);
            piece.transform.rotation = Quaternion.Euler(0f, 25f * i, 10f);
            SetInteractable(piece, "Splinter", "A sharp splinter.");
            AddItemPickup(piece, ItemId.Splinter, "I already have a splinter.");
        }

        SerializedObject smashObject = new SerializedObject(smash);
        smashObject.FindProperty("splinters").objectReferenceValue = splinters.gameObject;
        smashObject.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(crate.GetComponent<Interactable>().OnInteract, smash.Bash);
    }

    static Transform BuildKeyObject(Transform parent, string name, Vector3 position, Material material)
    {
        Transform root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.position = position;
        Cylinder(root, "Ring", Vector3.zero, new Vector3(0.09f, 0.012f, 0.09f), material, Quaternion.Euler(90f, 0f, 0f), false);
        Box(root, "Shaft", new Vector3(0.1f, 0f, 0f), new Vector3(0.14f, 0.02f, 0.035f), material, false);
        BoxCollider collider = root.gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0.05f, 0.02f, 0f);
        collider.size = new Vector3(0.3f, 0.1f, 0.14f);
        return root;
    }

    static void AddItemPickup(GameObject target, ItemId item, string alreadyMessage)
    {
        ItemPickup pickup = target.AddComponent<ItemPickup>();
        SerializedObject pickupObject = new SerializedObject(pickup);
        pickupObject.FindProperty("item").enumValueIndex = (int)item;
        pickupObject.FindProperty("alreadyCarryingMessage").stringValue = alreadyMessage;
        pickupObject.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(target.GetComponent<Interactable>().OnInteract, pickup.Collect);
    }

    static void BuildStairs(Transform parent, Palette palette)
    {
        float angle = Mathf.Atan2(StairRise, StairRun) * Mathf.Rad2Deg;
        if (angle >= 44f)
            Debug.LogError("Stair slope is " + angle.ToString("0") + " degrees. The player slope limit is 45, so this ramp will not be climbable.");

        float highZ = StairLowZ - StairRun;
        float slopeLength = Mathf.Sqrt(StairRise * StairRise + StairRun * StairRun);
        Quaternion rampRotation = Quaternion.Euler(angle, 0f, 0f);
        Vector3 low = new Vector3(0f, 0.02f, StairLowZ);
        Vector3 high = new Vector3(0f, StairRise, highZ);
        Vector3 mid = (low + high) * 0.5f;
        Vector3 normal = rampRotation * Vector3.up;
        const float rampThickness = 0.22f;
        Vector3 rampCenter = mid - normal * (rampThickness * 0.5f);

        // Invisible ramp is what the CharacterController walks on. The step boxes are only the look of stairs.
        // Positive X rotation raises the -Z end, which is the top of this flight.
        GameObject ramp = Box(
            parent,
            "StairRamp",
            rampCenter,
            new Vector3(StairHalfWidth * 2f - 0.2f, rampThickness, slopeLength),
            palette.wood,
            true,
            rampRotation);
        ramp.GetComponent<MeshRenderer>().enabled = false;

        int stepCount = 8;
        for (int i = 0; i < stepCount; i++)
        {
            float t0 = i / (float)stepCount;
            float t1 = (i + 1) / (float)stepCount;
            float top = Mathf.Lerp(0.02f, StairRise, t1);
            float z = Mathf.Lerp(StairLowZ, highZ, (t0 + t1) * 0.5f);
            float depth = StairRun / stepCount + 0.04f;
            Box(parent, "Step" + (i + 1), new Vector3(0f, top - 0.08f, z), new Vector3(StairHalfWidth * 2f - 0.28f, 0.16f, depth), palette.wood, false);
        }

        // Solid from the floor up to the landing, so there is no gap to get stuck in under the door.
        float landingFarZ = -(HalfDepth + 0.05f);
        // A few centimeters of overlap with the ramp, so the walk ends on the flat top
        // instead of a tall lip. The CharacterController step offset can clear a small edge.
        float landingNearZ = highZ + 0.08f;
        float landingDepth = landingNearZ - landingFarZ;
        float landingCenterZ = (landingNearZ + landingFarZ) * 0.5f;
        Box(
            parent,
            "Landing",
            new Vector3(0f, StairRise * 0.5f, landingCenterZ),
            new Vector3(StairHalfWidth * 2f, StairRise, landingDepth),
            palette.floor);

        // Walls on both sides of the flight. You enter from the room and walk up. You cannot slip under the ramp.
        float wallCenterZ = (StairLowZ + 0.2f + landingFarZ) * 0.5f;
        float wallDepth = Mathf.Abs(landingFarZ - (StairLowZ + 0.2f));
        float wallX = StairHalfWidth + Thick * 0.5f;
        Box(parent, "StairWallLeft", new Vector3(-wallX, WallHeight * 0.5f, wallCenterZ), new Vector3(Thick, WallHeight, wallDepth), palette.wall);
        Box(parent, "StairWallRight", new Vector3(wallX, WallHeight * 0.5f, wallCenterZ), new Vector3(Thick, WallHeight, wallDepth), palette.wall);

        Box(parent, "NewelBottomL", new Vector3(-StairHalfWidth + 0.08f, 0.45f, StairLowZ + 0.15f), new Vector3(0.1f, 0.9f, 0.1f), palette.wood);
        Box(parent, "NewelBottomR", new Vector3(StairHalfWidth - 0.08f, 0.45f, StairLowZ + 0.15f), new Vector3(0.1f, 0.9f, 0.1f), palette.wood);

        const float doorHeight = 2.15f;
        float doorCenterY = StairRise + doorHeight * 0.5f;
        float doorZ = -HalfDepth + 0.08f;

        // The panel swings for the visitor. It has no collider, so it cannot
        // shove the player. The blocker stays put and keeps the line "It's sealed shut".
        Transform hinge = new GameObject("DoorHinge").transform;
        hinge.SetParent(parent, false);
        hinge.position = new Vector3(-0.525f, StairRise, doorZ);
        Box(hinge, "SealedDoor", new Vector3(0.525f, doorHeight * 0.5f, 0f), new Vector3(1.05f, doorHeight, 0.08f), palette.wood, false);

        GameObject blocker = new GameObject("SealedBlocker");
        blocker.transform.SetParent(parent, false);
        blocker.transform.position = new Vector3(0f, doorCenterY, doorZ);
        BoxCollider blockerCollider = blocker.AddComponent<BoxCollider>();
        blockerCollider.size = new Vector3(1.05f, doorHeight, 0.12f);
        SetInteractable(blocker, "Interact", "It's sealed shut");

        Box(parent, "JambLeft", new Vector3(-0.58f, doorCenterY, doorZ + 0.02f), new Vector3(0.08f, doorHeight + 0.08f, 0.1f), palette.wood);
        Box(parent, "JambRight", new Vector3(0.58f, doorCenterY, doorZ + 0.02f), new Vector3(0.08f, doorHeight + 0.08f, 0.1f), palette.wood);
        Box(parent, "Header", new Vector3(0f, StairRise + doorHeight + 0.04f, doorZ + 0.02f), new Vector3(1.24f, 0.1f, 0.1f), palette.wood);

        SealedStairDoor sealedDoor = hinge.gameObject.AddComponent<SealedStairDoor>();
        SerializedObject sealedObject = new SerializedObject(sealedDoor);
        sealedObject.FindProperty("hinge").objectReferenceValue = hinge;
        sealedObject.FindProperty("solidBlocker").objectReferenceValue = blockerCollider;
        sealedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildLighting(Transform parent, Palette palette)
    {
        // Same intensity scale as a normal Unity point light (the template sun is intensity 2).
        // One brighter bulb in the middle, a small orange pool on the furnace, a weak pool on the bars.
        CreateLight(
            parent,
            "BareBulb",
            new Vector3(0f, 3.15f, -0.4f),
            LightType.Point,
            new Color(1f, 0.78f, 0.5f),
            2f,
            14f,
            LightShadows.Soft,
            palette.bulb);

        CreateLight(
            parent,
            "FurnaceGlow",
            new Vector3(-3.95f, 0.55f, 1.9f),
            LightType.Point,
            new Color(1f, 0.42f, 0.12f),
            0.75f,
            4.5f,
            LightShadows.None,
            palette.ember);

        Light jail = CreateLight(
            parent,
            "JailLight",
            new Vector3(0f, 3.35f, 3.35f),
            LightType.Spot,
            new Color(0.78f, 0.7f, 0.52f),
            1.15f,
            9f,
            LightShadows.None,
            null);
        jail.spotAngle = 70f;
        jail.innerSpotAngle = 40f;
        jail.transform.LookAt(new Vector3(0f, 1.1f, 5.3f));

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
            return;

        GameObject volumeObject = new GameObject("Global Volume");
        volumeObject.transform.SetParent(parent, false);
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0f;
        volume.sharedProfile = profile;
    }

    static Light CreateLight(
        Transform parent,
        string name,
        Vector3 position,
        LightType type,
        Color color,
        float intensity,
        float range,
        LightShadows shadows,
        Material bulbMaterial)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        Light light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = shadows;
        light.shadowStrength = 0.85f;
        go.AddComponent<UniversalAdditionalLightData>();

        if (bulbMaterial != null)
        {
            GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glass.name = name + "Glass";
            glass.transform.SetParent(go.transform, false);
            glass.transform.localScale = Vector3.one * (type == LightType.Point ? 0.16f : 0.1f);
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            glass.GetComponent<Renderer>().sharedMaterial = bulbMaterial;
            glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        return light;
    }

    static GameObject PlacePlayer(GameObject prefab, InputActionAsset actions)
    {
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        // Inside the cell, facing the bars. The north wall is at z = 8 and the bars at z = 5.15.
        player.transform.position = new Vector3(-0.15f, 0.08f, 7.45f);
        player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        FirstPersonController controller = player.GetComponent<FirstPersonController>();
        if (controller != null)
        {
            SerializedObject controllerObject = new SerializedObject(controller);
            SerializedProperty actionsProperty = controllerObject.FindProperty("inputActions");
            if (actionsProperty != null && actionsProperty.objectReferenceValue == null)
                actionsProperty.objectReferenceValue = actions;
            controllerObject.ApplyModifiedPropertiesWithoutUndo();
        }

        Camera camera = player.GetComponentInChildren<Camera>();
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.013f, 0.013f);
        }

        // The body capsule sits around the camera. If it casts shadows, the flashlight
        // beam draws a blob of the player on every wall.
        foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return player;
    }

    static void AttachFlashlight(GameObject player, Palette palette)
    {
        Flashlight flashlight = player.GetComponent<Flashlight>();
        if (flashlight == null)
            flashlight = player.AddComponent<Flashlight>();

        SerializedObject serialized = new SerializedObject(flashlight);
        serialized.FindProperty("bodyMaterial").objectReferenceValue = palette.metal;
        serialized.FindProperty("lensMaterial").objectReferenceValue = palette.glow;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildInteractionUi(GameObject player, InputActionAsset actions)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            Debug.LogError("LegacyRuntime.ttf was not found. On-screen prompts will have no letters.");

        GameObject canvasObject = new GameObject("InteractionUI");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject crosshair = CreateCrosshair(canvasObject.transform);
        Text prompt = CreateLabel(canvasObject.transform, "Prompt", font, 22, new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(560f, 40f));
        Text message = CreateLabel(canvasObject.transform, "Message", font, 28, new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(900f, 90f));
        prompt.gameObject.SetActive(false);
        message.gameObject.SetActive(false);

        PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
        Camera camera = player.GetComponentInChildren<Camera>();
        SerializedObject serialized = new SerializedObject(interactor);
        serialized.FindProperty("inputActions").objectReferenceValue = actions;
        serialized.FindProperty("viewCamera").objectReferenceValue = camera != null ? camera.transform : null;
        serialized.FindProperty("crosshair").objectReferenceValue = crosshair;
        serialized.FindProperty("promptLabel").objectReferenceValue = prompt;
        serialized.FindProperty("messageLabel").objectReferenceValue = message;
        serialized.FindProperty("interactDistance").floatValue = 3.75f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildFlashlightPickup(Transform jail, Palette palette)
    {
        // Outside the cell, left of the exit, so it can be picked up after the door opens.
        Transform root = new GameObject("Flashlight").transform;
        root.SetParent(jail, false);
        root.position = new Vector3(-2.6f, 0.05f, 3.25f);
        // LookAt points forward (+Z) at the player, so the glowing lens on that end faces them.
        root.LookAt(new Vector3(-2.6f, 0.05f, 0f));

        // Length of a cylinder is its Y axis. Tilting it 90 degrees on X lays that length along Z.
        Cylinder(root, "Body", Vector3.zero, new Vector3(0.07f, 0.16f, 0.07f), palette.metal, Quaternion.Euler(90f, 0f, 0f), false);
        Cylinder(root, "Lens", new Vector3(0f, 0f, 0.18f), new Vector3(0.09f, 0.02f, 0.09f), palette.glow, Quaternion.Euler(90f, 0f, 0f), false);

        BoxCollider pickupCollider = root.gameObject.AddComponent<BoxCollider>();
        pickupCollider.center = new Vector3(0f, 0.06f, 0f);
        pickupCollider.size = new Vector3(0.2f, 0.28f, 0.5f);

        GameObject glow = new GameObject("Glow");
        glow.transform.SetParent(root, false);
        glow.transform.localPosition = new Vector3(0f, 0.04f, 0.18f);
        Light glowLight = glow.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = new Color(1f, 0.72f, 0.35f);
        glowLight.useColorTemperature = false;
        glowLight.intensity = 0.12f;
        glowLight.range = 1.5f;
        glowLight.shadows = LightShadows.None;
        glow.AddComponent<UniversalAdditionalLightData>();

        SetInteractable(root.gameObject, "Pick up flashlight", "You picked up a flashlight. Press F to turn it on.");
        FlashlightPickup pickup = root.gameObject.AddComponent<FlashlightPickup>();
        Interactable interactable = root.gameObject.GetComponent<Interactable>();
        UnityEventTools.AddPersistentListener(interactable.OnInteract, pickup.Collect);
        EditorUtility.SetDirty(interactable);
    }

    static GameObject CreateCrosshair(Transform parent)
    {
        GameObject go = new GameObject("Crosshair");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(8f, 8f);
        rect.anchoredPosition = Vector2.zero;

        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.color = new Color(1f, 0.95f, 0.85f, 0.9f);
        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        if (knob != null)
            image.sprite = knob;

        return go;
    }

    static Text CreateLabel(Transform parent, string name, Font font, int size, Vector2 anchor, Vector2 offset, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = offset;

        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.95f, 0.9f, 0.78f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);
        return text;
    }

    static Palette CreateMaterials(Shader lit)
    {
        return new Palette
        {
            floor = SaveLit(lit, "BasementConcrete.mat", new Color(0.16f, 0.15f, 0.14f), 0.08f, 0f),
            wall = SaveLit(lit, "BasementWall.mat", new Color(0.2f, 0.18f, 0.16f), 0.05f, 0f),
            ceiling = SaveLit(lit, "BasementCeiling.mat", new Color(0.08f, 0.075f, 0.07f), 0.04f, 0f),
            wood = SaveLit(lit, "BasementWood.mat", new Color(0.3f, 0.19f, 0.11f), 0.16f, 0f),
            metal = SaveLit(lit, "BasementMetal.mat", new Color(0.13f, 0.13f, 0.14f), 0.55f, 0.75f),
            fabric = SaveLit(lit, "BasementFabric.mat", new Color(0.22f, 0.2f, 0.18f), 0.06f, 0f),
            brass = SaveLit(lit, "BasementBrass.mat", new Color(0.55f, 0.4f, 0.16f), 0.6f, 0.85f),
            rust = SaveLit(lit, "BasementRust.mat", new Color(0.28f, 0.13f, 0.08f), 0.22f, 0.35f),
            bulb = SaveEmissive(lit, "BasementBulb.mat", new Color(1f, 0.86f, 0.6f), 4f),
            ember = SaveEmissive(lit, "BasementEmber.mat", new Color(1f, 0.35f, 0.08f), 3f),
            glow = SaveEmissive(lit, "BasementFlashlightGlow.mat", new Color(1f, 0.7f, 0.32f), 0.45f),
            paint = SaveLit(lit, "BasementPaint.mat", new Color(0.52f, 0.36f, 0.46f), 0.25f, 0f),
        };
    }

    static Material SaveLit(Shader shader, string fileName, Color color, float smoothness, float metallic)
    {
        string path = MaterialFolder + "/" + fileName;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.color = color;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        material.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material SaveEmissive(Shader shader, string fileName, Color color, float intensity)
    {
        Material material = SaveLit(shader, fileName, color, 0.4f, 0f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * intensity);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void SetInteractable(GameObject target, string prompt, string message)
    {
        Interactable interactable = target.GetComponent<Interactable>();
        if (interactable == null)
            interactable = target.AddComponent<Interactable>();

        SerializedObject serialized = new SerializedObject(interactable);
        serialized.FindProperty("prompt").stringValue = prompt;
        serialized.FindProperty("message").stringValue = message;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform Group(string name)
    {
        GameObject go = new GameObject(name);
        return go.transform;
    }

    static GameObject Box(
        Transform parent,
        string name,
        Vector3 position,
        Vector3 scale,
        Material material,
        bool withCollider = true,
        Quaternion rotation = default)
    {
        return Primitive(PrimitiveType.Cube, parent, name, position, scale, material, withCollider, ValidRotation(rotation));
    }

    static GameObject Cylinder(
        Transform parent,
        string name,
        Vector3 position,
        Vector3 scale,
        Material material,
        Quaternion rotation = default,
        bool withCollider = true)
    {
        return Primitive(PrimitiveType.Cylinder, parent, name, position, scale, material, withCollider, ValidRotation(rotation));
    }

    // default(Quaternion) is (0,0,0,0). That is not a usable rotation, and Unity's
    // == operator does not treat it as equal to itself, so it cannot be detected that way.
    static Quaternion ValidRotation(Quaternion rotation)
    {
        if (rotation.x == 0f && rotation.y == 0f && rotation.z == 0f && rotation.w == 0f)
            return Quaternion.identity;
        return rotation;
    }

    static GameObject Primitive(
        PrimitiveType type,
        Transform parent,
        string name,
        Vector3 position,
        Vector3 scale,
        Material material,
        bool withCollider,
        Quaternion rotation)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = rotation;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!withCollider)
            Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void Blocker(Transform parent, string name, Vector3 position, Vector3 size, bool ignoreRaycast = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.size = size;
        if (!ignoreRaycast)
            return;

        // Physics still blocks the body. Rays skip this layer, so the player can
        // use the table and the key through the bars.
        int layer = LayerMask.NameToLayer("Ignore Raycast");
        if (layer >= 0)
            go.layer = layer;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    static void EnsureSceneInBuildSettings(string scenePath)
    {
        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(current.Length + 1);
        bool found = false;
        foreach (EditorBuildSettingsScene entry in current)
        {
            if (entry.path == scenePath)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                found = true;
            }
            else
            {
                scenes.Add(entry);
            }
        }

        if (!found)
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void WireThreat(GameObject player, Shader lit)
    {
        ClosetLock closet = Object.FindAnyObjectByType<ClosetLock>();
        JailDoor jailDoor = Object.FindAnyObjectByType<JailDoor>();
        JailLock jailLock = Object.FindAnyObjectByType<JailLock>();
        SealedStairDoor stairDoor = Object.FindAnyObjectByType<SealedStairDoor>();
        Transform bed = GameObject.Find("Bed") != null ? GameObject.Find("Bed").transform : null;
        Transform jail = GameObject.Find("Jail") != null ? GameObject.Find("Jail").transform : null;

        Transform closetHide = Marker(closet != null ? closet.transform : player.transform, "ClosetHidePoint", new Vector3(0f, 0.08f, -0.1f), 0f);
        Transform closetStand = Marker(closet != null ? closet.transform : player.transform, "ClosetStandPoint", new Vector3(0f, 0.08f, 1.65f), 0f);
        Transform bedHide = Marker(bed != null ? bed : player.transform, "BedHidePoint", new Vector3(0.05f, 0.08f, 0f), 90f);
        Transform bedStand = Marker(bed != null ? bed : player.transform, "BedStandPoint", new Vector3(1.45f, 0.08f, 0f), 90f);

        GameObject cell = new GameObject("CellVolume");
        if (jail != null)
            cell.transform.SetParent(jail, false);
        BoxCollider cellBox = cell.AddComponent<BoxCollider>();
        cellBox.isTrigger = true;
        // Just inside the bars (z = 5.15) through to the north wall. Feet in here,
        // with the lock back on, count as safe. The doorway itself does not.
        cellBox.center = new Vector3(0f, 1.3f, 6.56f);
        cellBox.size = new Vector3(11.4f, 2.6f, 2.56f);

        PlayerHiding hiding = player.AddComponent<PlayerHiding>();
        SerializedObject hidingObject = new SerializedObject(hiding);
        hidingObject.FindProperty("body").objectReferenceValue = player.GetComponent<FirstPersonController>();
        hidingObject.FindProperty("closet").objectReferenceValue = closet;
        hidingObject.FindProperty("closetHide").objectReferenceValue = closetHide;
        hidingObject.FindProperty("closetStand").objectReferenceValue = closetStand;
        hidingObject.FindProperty("bedHide").objectReferenceValue = bedHide;
        hidingObject.FindProperty("bedStand").objectReferenceValue = bedStand;
        hidingObject.FindProperty("jailLock").objectReferenceValue = jailLock;
        hidingObject.FindProperty("cellVolume").objectReferenceValue = cellBox;
        hidingObject.ApplyModifiedPropertiesWithoutUndo();

        if (bed != null)
        {
            Interactable bedUse = bed.GetComponent<Interactable>();
            if (bedUse == null)
                bedUse = bed.gameObject.AddComponent<Interactable>();
            UnityEventTools.AddPersistentListener(bedUse.OnInteract, hiding.RequestBed);
        }

        Material skin = SaveLit(lit, "BasementEnemy.mat", new Color(0.015f, 0.014f, 0.016f), 0.18f, 0f);
        Material eyes = SaveEmissive(lit, "BasementEnemyEyes.mat", new Color(0.75f, 0.1f, 0.07f), 1.5f);

        GameObject visitor = new GameObject("Visitor");
        visitor.transform.position = new Vector3(0f, 1.62f, -7.35f);
        BasementEnemy enemy = visitor.AddComponent<BasementEnemy>();
        SerializedObject enemyObject = new SerializedObject(enemy);
        SetVectors(enemyObject, "stairDown",
            new Vector3(0f, 1.62f, -7.35f),
            new Vector3(0f, 1.15f, -5.55f),
            new Vector3(0f, 0.45f, -3.9f),
            new Vector3(0f, 0.08f, -2.15f));
        SetVectors(enemyObject, "browsePoints",
            new Vector3(-3.5f, 0.08f, 1.05f),
            new Vector3(2.0f, 0.08f, 2.5f),
            new Vector3(-3.4f, 0.08f, -3.55f),
            new Vector3(3.7f, 0.08f, 1.45f),
            new Vector3(0.3f, 0.08f, -0.4f),
            new Vector3(-0.3f, 0.08f, 3.35f));
        enemyObject.FindProperty("barPlaneZ").floatValue = 5.15f;
        enemyObject.FindProperty("player").objectReferenceValue = player.transform;
        enemyObject.FindProperty("jailDoor").objectReferenceValue = jailDoor;
        enemyObject.FindProperty("stairDoor").objectReferenceValue = stairDoor;
        enemyObject.FindProperty("hiding").objectReferenceValue = hiding;
        enemyObject.FindProperty("hookGame").objectReferenceValue = player.GetComponent<HookMinigame>();
        enemyObject.FindProperty("bodyMaterial").objectReferenceValue = skin;
        enemyObject.FindProperty("eyeMaterial").objectReferenceValue = eyes;
        enemyObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform Marker(Transform parent, string name, Vector3 localPosition, float yaw)
    {
        Transform marker = new GameObject(name).transform;
        marker.SetParent(parent, false);
        marker.localPosition = localPosition;
        marker.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return marker;
    }

    static void SetVectors(SerializedObject serialized, string propertyName, params Vector3[] values)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            Debug.LogError("BasementEnemy has no vector array named " + propertyName + ".");
            return;
        }

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).vector3Value = values[i];
    }

    static void BuildAudio(Transform furnace)
    {
        GameObject root = new GameObject("Audio");
        GameAudio audio = root.AddComponent<GameAudio>();
        SerializedObject serialized = new SerializedObject(audio);
        AssignClips(serialized, "footstepWood",
            "footstep_wood_01.ogg",
            "footstep_wood_02.ogg",
            "footstep_wood_03.ogg",
            "footstep_wood_04.ogg",
            "footstep_wood_05.ogg",
            "footstep_wood_06.ogg",
            "footstep_wood_07.ogg");
        AssignClip(serialized, "jump", "jump.ogg");
        AssignClip(serialized, "land", "land.ogg");
        AssignClips(serialized, "floorboardStrain", "creak_01.ogg", "creak_02.ogg", "creak_03.ogg");
        AssignClip(serialized, "floorboardPop", "board_pop.ogg");
        AssignClip(serialized, "pickup", "pickup.ogg");
        AssignClip(serialized, "keyJingle", "key_jingle.ogg");
        AssignClip(serialized, "keySkid", "key_skid.wav");
        AssignClip(serialized, "crateSmash", "crate_smash.ogg");
        AssignClips(serialized, "axeWhoosh", "axe_whoosh_01.ogg", "axe_whoosh_02.ogg");
        AssignClips(serialized, "axeHitWood", "hit_wood_01.ogg", "hit_wood_02.ogg", "hit_wood_03.ogg");
        AssignClips(serialized, "axeHitHard", "hit_hard_01.ogg", "hit_hard_02.ogg");
        AssignClip(serialized, "flashlightOn", "flashlight_on.ogg");
        AssignClip(serialized, "flashlightOff", "flashlight_off.ogg");
        AssignClips(serialized, "lockUnlock", "lock_unlock_01.ogg", "lock_unlock_02.ogg");
        AssignClip(serialized, "lockRattle", "lock_rattle.wav");
        AssignClip(serialized, "barsSlide", "bars_slide.wav");
        AssignClips(serialized, "closetCreak", "closet_open_01.ogg", "closet_open_02.ogg");
        AssignClip(serialized, "catchFire", "fire_ignite.wav");
        AssignClip(serialized, "fireCrackle", "fire_crackle.wav");
        AssignClip(serialized, "hookTick", "hook_tick.ogg");
        AssignClip(serialized, "hookScratch", "creak_03.ogg");
        AssignClip(serialized, "hookSuccess", "hook_success.ogg");
        AssignClips(serialized, "stingers", "enemy_stinger.wav");
        AssignClip(serialized, "scream", "enemy_scream.wav");
        SerializedProperty master = serialized.FindProperty("masterVolume");
        if (master != null)
            master.floatValue = 0.85f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // Flat room tone. Kept quiet. GameAudio's master volume scales it with everything else.
        AudioSource bed = root.AddComponent<AudioSource>();
        ConfigureLoop(bed, Clip("ambient_basement.wav"), 0.16f, 0f, 1f, 5f);

        if (furnace == null)
            return;

        GameObject furnaceSound = new GameObject("FurnaceSound");
        furnaceSound.transform.SetParent(furnace, false);
        furnaceSound.transform.localPosition = new Vector3(0.15f, 0.7f, 0.1f);
        AudioSource roar = furnaceSound.AddComponent<AudioSource>();
        ConfigureLoop(roar, Clip("furnace_roar.wav"), 0.55f, 1f, 1.5f, 8f);
    }

    static void ConfigureLoop(AudioSource source, AudioClip clip, float volume, float spatialBlend, float minDistance, float maxDistance)
    {
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = true;
        source.volume = volume;
        source.spatialBlend = spatialBlend;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = Mathf.Max(minDistance + 0.1f, maxDistance);
        source.priority = spatialBlend > 0.5f ? 80 : 48;
    }

    static void AssignClip(SerializedObject serialized, string propertyName, string fileName)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError("GameAudio has no field named " + propertyName + ".");
            return;
        }

        property.objectReferenceValue = Clip(fileName);
    }

    static void AssignClips(SerializedObject serialized, string propertyName, params string[] fileNames)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            Debug.LogError("GameAudio has no clip array named " + propertyName + ".");
            return;
        }

        property.arraySize = fileNames.Length;
        for (int i = 0; i < fileNames.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = Clip(fileNames[i]);
    }

    static AudioClip Clip(string fileName)
    {
        string path = AudioFolder + "/" + fileName;
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
            Debug.LogError("Missing audio clip at " + path + ". See Assets/Audio/CREDITS.md.");
        return clip;
    }

    struct Palette
    {
        public Material floor;
        public Material wall;
        public Material ceiling;
        public Material wood;
        public Material metal;
        public Material fabric;
        public Material brass;
        public Material rust;
        public Material bulb;
        public Material ember;
        public Material glow;
        public Material paint;
    }
}
