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
        BuildProps(props, palette);
        BuildJail(jail, palette);
        BuildFlashlightPickup(jail, palette);
        BuildStairs(stairs, palette);
        BuildLighting(lighting, palette);
        GameObject player = PlacePlayer(playerPrefab, actions);
        AttachFlashlight(player, palette);
        BuildInteractionUi(player, actions);

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
            "Assets/Scenes/Basement is open.\n\nPress Play. The jail is in front of you. A flashlight is on the floor just outside the bars.\n\nLook at it and press E to pick it up, then press F to turn it on. The stairs and the sealed door are behind you.",
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

        Box(parent, "Floor", new Vector3(0f, -Thick * 0.5f, 0f), new Vector3(floorWidth, Thick, floorDepth), palette.floor);
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

    static void BuildProps(Transform parent, Palette palette)
    {
        BuildFurnace(parent, palette, new Vector3(-4.65f, 0f, 1.9f));
        BuildWorkbench(parent, palette, new Vector3(-4.45f, 0f, -0.55f));
        BuildCloset(parent, palette, new Vector3(4.7f, 0f, 2.15f));
        BuildBed(parent, palette, new Vector3(4.15f, 0f, -0.85f));
        BuildShelf(parent, palette, new Vector3(5.15f, 0f, 4.15f));
        BuildCrateStack(parent, palette, "CratesWest", new Vector3(-3.7f, 0f, 4.15f));
        BuildCrateStack(parent, palette, "CratesEast", new Vector3(2.6f, 0f, 3.35f));

        // Water heater in the west corner, clear of the stair opening.
        Transform heater = new GameObject("WaterHeater").transform;
        heater.SetParent(parent, false);
        heater.position = new Vector3(-4.7f, 0f, -2.15f);
        Cylinder(heater, "Tank", new Vector3(0f, 0.7f, 0f), new Vector3(0.7f, 0.7f, 0.7f), palette.metal);
        Cylinder(heater, "Vent", new Vector3(0f, 1.55f, 0f), new Vector3(0.12f, 0.2f, 0.12f), palette.rust);
    }

    static void BuildFurnace(Transform parent, Palette palette, Vector3 origin)
    {
        Transform root = new GameObject("Furnace").transform;
        root.SetParent(parent, false);
        root.position = origin;

        Box(root, "Body", new Vector3(0f, 0.7f, 0f), new Vector3(1.15f, 1.4f, 0.85f), palette.rust);
        Box(root, "Door", new Vector3(0.58f, 0.55f, 0f), new Vector3(0.06f, 0.7f, 0.5f), palette.metal);
        Box(root, "Mouth", new Vector3(0.62f, 0.48f, 0f), new Vector3(0.04f, 0.28f, 0.22f), palette.ember, false);
        Cylinder(root, "Flue", new Vector3(-0.15f, 1.85f, 0.1f), new Vector3(0.18f, 0.55f, 0.18f), palette.metal);
        Cylinder(root, "FlueElbow", new Vector3(-0.15f, 2.35f, 0.45f), new Vector3(0.16f, 0.28f, 0.16f), palette.metal, Quaternion.Euler(90f, 0f, 0f));
    }

    static void BuildWorkbench(Transform parent, Palette palette, Vector3 origin)
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
    }

    static void BuildCloset(Transform parent, Palette palette, Vector3 origin)
    {
        Transform root = new GameObject("Closet").transform;
        root.SetParent(parent, false);
        root.position = origin;

        Box(root, "Carcass", new Vector3(0f, 1.1f, 0f), new Vector3(1.2f, 2.2f, 0.6f), palette.wood);
        Box(root, "DoorLeft", new Vector3(-0.28f, 1.05f, 0.32f), new Vector3(0.56f, 2.0f, 0.04f), palette.wall);
        Box(root, "DoorRight", new Vector3(0.28f, 1.05f, 0.32f), new Vector3(0.56f, 2.0f, 0.04f), palette.wall);
        Box(root, "KnobL", new Vector3(-0.06f, 1.05f, 0.36f), new Vector3(0.04f, 0.04f, 0.04f), palette.brass, false);
        Box(root, "KnobR", new Vector3(0.06f, 1.05f, 0.36f), new Vector3(0.04f, 0.04f, 0.04f), palette.brass, false);
    }

    static void BuildBed(Transform parent, Palette palette, Vector3 origin)
    {
        Transform root = new GameObject("Bed").transform;
        root.SetParent(parent, false);
        root.position = origin;

        Box(root, "Frame", new Vector3(0f, 0.22f, 0f), new Vector3(1.15f, 0.28f, 2.05f), palette.wood);
        Box(root, "Mattress", new Vector3(0f, 0.46f, 0.02f), new Vector3(1.05f, 0.22f, 1.9f), palette.fabric);
        Box(root, "Pillow", new Vector3(0f, 0.62f, 0.78f), new Vector3(0.55f, 0.12f, 0.32f), palette.fabric);
        Box(root, "Headboard", new Vector3(0f, 0.7f, 1.0f), new Vector3(1.15f, 0.7f, 0.08f), palette.wood);
    }

    static void BuildShelf(Transform parent, Palette palette, Vector3 origin)
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
    }

    static void BuildCrateStack(Transform parent, Palette palette, string name, Vector3 origin)
    {
        Transform root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.position = origin;

        Box(root, "CrateA", new Vector3(0f, 0.28f, 0f), new Vector3(0.6f, 0.55f, 0.6f), palette.wood);
        Box(root, "CrateB", new Vector3(0.1f, 0.78f, 0.05f), new Vector3(0.45f, 0.45f, 0.45f), palette.wood);
    }

    static void BuildJail(Transform parent, Palette palette)
    {
        // The cell is the strip between the bars and the north wall. Bars are visual.
        // Solid invisible boxes do the blocking, including across the closed door.
        float barZ = 5.15f;
        float doorHalf = 0.7f;
        float y = WallHeight * 0.5f;

        Blocker(parent, "BarBlockerLeft", new Vector3(-(HalfWidth + doorHalf) * 0.5f, y, barZ), new Vector3(HalfWidth - doorHalf, WallHeight, 0.14f));
        Blocker(parent, "BarBlockerRight", new Vector3((HalfWidth + doorHalf) * 0.5f, y, barZ), new Vector3(HalfWidth - doorHalf, WallHeight, 0.14f));
        Blocker(parent, "DoorBlocker", new Vector3(0f, y, barZ), new Vector3(doorHalf * 2f, WallHeight, 0.14f));

        float barHeightScale = WallHeight * 0.5f;
        for (float x = -HalfWidth + 0.35f; x <= HalfWidth - 0.3f; x += 0.34f)
        {
            bool door = Mathf.Abs(x) < doorHalf - 0.05f;
            string barName = door ? "DoorBar" : "Bar";
            Cylinder(parent, barName, new Vector3(x, y, barZ), new Vector3(0.07f, barHeightScale, 0.07f), palette.metal, Quaternion.identity, false);
        }

        // The lock sits on the room side of the door so the ray hits it before the blocker.
        Transform lockRoot = new GameObject("Lock").transform;
        lockRoot.SetParent(parent, false);
        lockRoot.position = new Vector3(0.55f, 1.2f, barZ - 0.45f);
        Box(lockRoot, "Body", Vector3.zero, new Vector3(0.16f, 0.22f, 0.1f), palette.brass);
        Box(lockRoot, "Shackle", new Vector3(0f, 0.14f, 0f), new Vector3(0.12f, 0.08f, 0.06f), palette.metal);
        // A cot and a bucket on the far side of the bars, so the cell reads as a room and not an empty gap.
        Box(parent, "CellCot", new Vector3(-2.1f, 0.18f, 6.9f), new Vector3(0.9f, 0.28f, 1.7f), palette.wood);
        Box(parent, "CellMattress", new Vector3(-2.1f, 0.36f, 6.9f), new Vector3(0.8f, 0.1f, 1.5f), palette.fabric);
        Cylinder(parent, "Bucket", new Vector3(2.2f, 0.18f, 6.4f), new Vector3(0.32f, 0.16f, 0.32f), palette.metal);
        BoxCollider lockCollider = lockRoot.gameObject.AddComponent<BoxCollider>();
        lockCollider.center = new Vector3(0f, 0.04f, 0f);
        lockCollider.size = new Vector3(0.28f, 0.4f, 0.22f);
        SetInteractable(lockRoot.gameObject, "Interact", "You need a key");
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
        GameObject door = Box(parent, "SealedDoor", new Vector3(0f, doorCenterY, doorZ), new Vector3(1.05f, doorHeight, 0.08f), palette.wood);
        Box(parent, "JambLeft", new Vector3(-0.58f, doorCenterY, doorZ + 0.02f), new Vector3(0.08f, doorHeight + 0.08f, 0.1f), palette.wood);
        Box(parent, "JambRight", new Vector3(0.58f, doorCenterY, doorZ + 0.02f), new Vector3(0.08f, doorHeight + 0.08f, 0.1f), palette.wood);
        Box(parent, "Header", new Vector3(0f, StairRise + doorHeight + 0.04f, doorZ + 0.02f), new Vector3(1.24f, 0.1f, 0.1f), palette.wood);
        SetInteractable(door, "Interact", "It's sealed shut");
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
        player.transform.position = new Vector3(0f, 0.05f, -1.1f);
        player.transform.rotation = Quaternion.identity;

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
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildFlashlightPickup(Transform jail, Palette palette)
    {
        // Bars are at z = 5.15. This sits on the floor in front of them, on the
        // player's side, so it can be walked to. The cell behind the bars cannot.
        Transform root = new GameObject("Flashlight").transform;
        root.SetParent(jail, false);
        root.position = new Vector3(0.8f, 0.05f, 4.05f);
        // LookAt points forward (+Z) at the player, so the glowing lens on that end faces them.
        root.LookAt(new Vector3(0.8f, 0.05f, 0f));

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

    static void Blocker(Transform parent, string name, Vector3 position, Vector3 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.size = size;
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
    }
}
