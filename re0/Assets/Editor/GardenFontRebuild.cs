using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class GardenFontRebuild
{
    const string SourcePath = "Assets/Fonts/NotoSansSC-VF.ttf";
    const string FontPath = "Assets/Fonts/NotoSansSC SDF.asset";
    const string RuntimePath = "Assets/Resources/NotoSansSC SDF.asset";

    [MenuItem("Garden Prototype/0. Rebuild Noto font")]
    public static void Rebuild()
    {
        var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
        if (source == null) throw new Exception("Missing Noto Sans SC source font.");

        var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
            1024, 1024, AtlasPopulationMode.Dynamic);
        if (font == null) throw new Exception("Could not create Noto Sans SC TMP font asset.");

        font.name = "NotoSansSC SDF";
        font.atlasTextures[0].name = "NotoSansSC Atlas";
        font.material.name = "NotoSansSC Atlas Material";

        AssetDatabase.DeleteAsset(FontPath);
        AssetDatabase.CreateAsset(font, FontPath);
        AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
        AssetDatabase.AddObjectToAsset(font.material, font);
        AssetDatabase.SaveAssets();

        AssetDatabase.DeleteAsset(RuntimePath);
        if (!AssetDatabase.CopyAsset(FontPath, RuntimePath))
            throw new Exception("Could not copy Noto Sans SC font into Resources.");
        AssetDatabase.SaveAssets();
        Debug.Log("GARDEN_FONT_REBUILD_OK: Noto Sans SC TMP assets created from source font");
    }
}
