using UnityEditor;
using UnityEngine;

/// <summary>
/// Packs individual PBR textures into a single URP Terrain Mask Map.
/// R = Metallic, G = AO, B = Height/Displacement, A = Smoothness.
/// </summary>
public class MaskMapPacker : EditorWindow
{
    private Texture2D metallicTex;
    private Texture2D aoTex;
    private Texture2D heightTex;
    private Texture2D roughnessTex;

    [Tooltip("Used when no metallic texture is assigned.")]
    private float defaultMetallic = 0f;
    [Tooltip("Used when no AO texture is assigned.")]
    private float defaultAO = 1f;
    [Tooltip("Used when no height texture is assigned.")]
    private float defaultHeight = 0.5f;
    [Tooltip("Used when no roughness texture is assigned.")]
    private float defaultSmoothness = 0.2f;

    private bool invertRoughness = true;
    private int resolution = 1024;
    private string savePath = "Assets/Materials/Terrain/Layers";

    [MenuItem("Tools/Terrain/Mask Map Packer")]
    public static void ShowWindow()
    {
        GetWindow<MaskMapPacker>("Mask Map Packer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Input Textures (leave empty to use default value)", EditorStyles.boldLabel);
        metallicTex  = (Texture2D)EditorGUILayout.ObjectField("Metallic",     metallicTex,  typeof(Texture2D), false);
        defaultMetallic = EditorGUILayout.Slider("  Default Metallic", defaultMetallic, 0f, 1f);

        EditorGUILayout.Space(2);
        aoTex        = (Texture2D)EditorGUILayout.ObjectField("AO",           aoTex,        typeof(Texture2D), false);
        defaultAO = EditorGUILayout.Slider("  Default AO", defaultAO, 0f, 1f);

        EditorGUILayout.Space(2);
        heightTex    = (Texture2D)EditorGUILayout.ObjectField("Height/Disp",  heightTex,    typeof(Texture2D), false);
        defaultHeight = EditorGUILayout.Slider("  Default Height", defaultHeight, 0f, 1f);

        EditorGUILayout.Space(2);
        roughnessTex = (Texture2D)EditorGUILayout.ObjectField("Roughness",    roughnessTex, typeof(Texture2D), false);
        invertRoughness = EditorGUILayout.Toggle("  Invert to Smoothness", invertRoughness);
        defaultSmoothness = EditorGUILayout.Slider("  Default Smoothness", defaultSmoothness, 0f, 1f);

        EditorGUILayout.Space(8);
        resolution = EditorGUILayout.IntPopup("Resolution", resolution,
            new[] { "256", "512", "1024", "2048", "4096" },
            new[] { 256, 512, 1024, 2048, 4096 });

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Pack & Save Mask Map"))
        {
            string dir = InferDirectory();
            string name = InferName();
            string path = System.IO.Path.Combine(dir, name + "_MaskMap.png");
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            Pack(path);
        }
    }

    private string InferDirectory()
    {
        Texture2D[] candidates = { heightTex, roughnessTex, aoTex, metallicTex };
        foreach (Texture2D tex in candidates)
        {
            if (tex == null) continue;
            string assetPath = AssetDatabase.GetAssetPath(tex);
            if (!string.IsNullOrEmpty(assetPath))
                return System.IO.Path.GetDirectoryName(assetPath);
        }
        return "Assets";
    }

    private string InferName()
    {
        Texture2D[] candidates = { heightTex, roughnessTex, aoTex, metallicTex };
        foreach (Texture2D tex in candidates)
        {
            if (tex == null) continue;
            string name = tex.name;
            // Strip common suffixes to get the base name.
            string[] suffixes = { "_Displacement", "_Roughness", "_AO", "_Metalness", "_Metallic", "_Height", "_Disp" };
            foreach (string s in suffixes)
                if (name.EndsWith(s, System.StringComparison.OrdinalIgnoreCase))
                    { name = name.Substring(0, name.Length - s.Length); break; }
            return name;
        }
        return "MaskMap";
    }

    private void Pack(string path)
    {
        Texture2D result = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true);

        Color[] metalPixels  = ReadPixels(metallicTex);
        Color[] aoPixels     = ReadPixels(aoTex);
        Color[] heightPixels = ReadPixels(heightTex);
        Color[] roughPixels  = ReadPixels(roughnessTex);

        Color[] output = new Color[resolution * resolution];
        for (int i = 0; i < output.Length; i++)
        {
            float r = metalPixels  != null ? metalPixels[i].r  : defaultMetallic;
            float g = aoPixels     != null ? aoPixels[i].r     : defaultAO;
            float b = heightPixels != null ? heightPixels[i].r : defaultHeight;
            float a;
            if (roughPixels != null)
                a = invertRoughness ? 1f - roughPixels[i].r : roughPixels[i].r;
            else
                a = defaultSmoothness;

            output[i] = new Color(r, g, b, a);
        }

        result.SetPixels(output);
        result.Apply();

        byte[] png = result.EncodeToPNG();
        DestroyImmediate(result);

        System.IO.File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path);

        // Set texture import settings for a mask map (linear, no sRGB).
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null)
        {
            importer.sRGBTexture = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        Debug.Log($"[MaskMapPacker] Saved mask map to {path}");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
    }

    private Color[] ReadPixels(Texture2D tex)
    {
        if (tex == null) return null;

        // Make texture readable via a temporary RenderTexture.
        RenderTexture rt = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(tex, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readable = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
        readable.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        readable.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color[] pixels = readable.GetPixels();
        DestroyImmediate(readable);
        return pixels;
    }
}
