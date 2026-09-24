using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GardenCapture
{
    [MenuItem("Garden Prototype/3. Capture preview")]
    public static void Capture()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GardenPrototype.unity", OpenSceneMode.Single);
        var camera = Camera.main;
        if (camera == null) throw new System.Exception("Garden scene has no Main Camera.");
        const int width = 1280, height = 720;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            Directory.CreateDirectory("Preview");
            string path = "Preview/Garden.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Debug.Log("GARDEN_CAPTURE_OK " + path);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(texture);
        }
    }
}
