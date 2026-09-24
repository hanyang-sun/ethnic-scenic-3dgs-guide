using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GardenUiSettings
{
    [MenuItem("Garden Prototype/Apply smaller UI")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/GardenPrototype.unity", OpenSceneMode.Single);
        Configure(
            Object.FindFirstObjectByType<POILabel>(),
            Object.FindFirstObjectByType<DestinationSelector>(),
            Object.FindFirstObjectByType<DistanceHUD>(),
            Object.FindFirstObjectByType<ArrivalPanel>());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("GARDEN_UI_UPDATED: compact labels, buttons, distance HUD and arrival panel");
    }

    public static void Configure(POILabel labels, DestinationSelector selector, DistanceHUD hud, ArrivalPanel arrival)
    {
        if (labels == null || selector == null || hud == null || arrival == null)
            throw new System.Exception("Garden scene is missing a UI component.");
        labels.worldScale = 0.004f;
        labels.fontSize = 32f;
        labels.scaleLimits = new Vector2(0.75f, 1.6f);
        selector.buttonWidth = 176f;
        selector.buttonHeight = 40f;
        hud.width = 360f;
        hud.fontSize = 22f;
        arrival.panelWidth = 520f;
        arrival.panelHeight = 210f;
    }
}
