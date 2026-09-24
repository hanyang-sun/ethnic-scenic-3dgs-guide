using System;
using System.IO;
using System.Reflection;
using GaussianSplatting.Editor;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEngine;

public static class GardenImport
{
    const string InputPath = "SourceAssets/Garden/garden.ply";
    const string AssetPath = "Assets/GaussianAssets/Garden/garden.asset";

    [MenuItem("Garden Prototype/1. Import 3DGS")]
    public static void Import()
    {
        if (!File.Exists(InputPath)) throw new FileNotFoundException("Garden PLY is missing", InputPath);
        var existing = AssetDatabase.LoadAssetAtPath<GaussianSplatAsset>(AssetPath);
        if (existing != null && existing.cameras != null && existing.cameras.Length > 0)
        {
            Debug.Log($"GARDEN_IMPORT_ALREADY_DONE count={existing.splatCount} cameras={existing.cameras.Length} bounds={existing.boundsMin}..{existing.boundsMax}");
            return;
        }

        var type = typeof(GaussianSplatAssetCreator);
        var creator = ScriptableObject.CreateInstance<GaussianSplatAssetCreator>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetField("m_InputFile", flags).SetValue(creator, Path.GetFullPath(InputPath));
        type.GetField("m_OutputFolder", flags).SetValue(creator, "Assets/GaussianAssets/Garden");
        var qualityField = type.GetField("m_Quality", flags);
        qualityField.SetValue(creator, Enum.ToObject(qualityField.FieldType, 2)); // Medium
        type.GetMethod("ApplyQualityLevel", flags).Invoke(creator, null);
        type.GetMethod("CreateAsset", flags).Invoke(creator, null);
        UnityEngine.Object.DestroyImmediate(creator);

        var asset = AssetDatabase.LoadAssetAtPath<GaussianSplatAsset>(AssetPath);
        if (asset == null || asset.splatCount == 0) throw new Exception("Garden 3DGS conversion produced no asset. Check Editor log.");
        Debug.Log($"GARDEN_IMPORT_OK count={asset.splatCount} cameras={asset.cameras?.Length ?? 0} bounds={asset.boundsMin}..{asset.boundsMax}");
    }
}
