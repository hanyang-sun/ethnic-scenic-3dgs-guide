using TMPro;
using UnityEngine;

public static class PrototypeFonts
{
    static TMP_FontAsset chinese;
    public static TMP_FontAsset Chinese => chinese != null ? chinese : chinese = Resources.Load<TMP_FontAsset>("NotoSansSC SDF");

    public static void Apply(TextMeshProUGUI text)
    {
        if (Chinese != null) text.font = Chinese;
    }
}
