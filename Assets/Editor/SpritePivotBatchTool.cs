using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEditor.U2D;
using System.Linq;

public class SpritePivotBatchTool : EditorWindow
{
    private static Vector2 customPivot = new Vector2(0.5f, 0f);

    [MenuItem("Tools/Sprite Pivot/Center")]
    static void SetCenter() => ApplyPivot(new Vector2(0.5f, 0.5f));

    [MenuItem("Tools/Sprite Pivot/Bottom")]
    static void SetBottom() => ApplyPivot(new Vector2(0.5f, 0f));

    [MenuItem("Tools/Sprite Pivot/Top")]
    static void SetTop() => ApplyPivot(new Vector2(0.5f, 1f));

    [MenuItem("Tools/Sprite Pivot/Left")]
    static void SetLeft() => ApplyPivot(new Vector2(0f, 0.5f));

    [MenuItem("Tools/Sprite Pivot/Right")]
    static void SetRight() => ApplyPivot(new Vector2(1f, 0.5f));

    [MenuItem("Tools/Sprite Pivot/Bottom Center")]
    static void SetBottomCenter() => ApplyPivot(new Vector2(0.5f, 0f));

    [MenuItem("Tools/Sprite Pivot/Custom")]
    static void OpenWindow()
    {
        GetWindow<SpritePivotBatchTool>("Custom Pivot");
    }

    void OnGUI()
    {
        GUILayout.Label("Custom Pivot", EditorStyles.boldLabel);

        customPivot = EditorGUILayout.Vector2Field("Pivot (0~1)", customPivot);

        if (GUILayout.Button("Apply To Selected Textures"))
        {
            ApplyPivot(customPivot);
        }
    }

    static void ApplyPivot(Vector2 pivot)
    {
        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null || importer.textureType != TextureImporterType.Sprite)
                continue;

            var factory = new SpriteDataProviderFactories();
            factory.Init();

            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            var rects = dataProvider.GetSpriteRects().ToList();

            for (int i = 0; i < rects.Count; i++)
            {
                var rect = rects[i];

                rect.alignment = SpriteAlignment.Custom;
                rect.pivot = pivot;

                rects[i] = rect;
            }

            dataProvider.SetSpriteRects(rects.ToArray());
            dataProvider.Apply();

            importer.SaveAndReimport();
        }

        Debug.Log("Sprite Pivot Batch Applied (Unity6 API)");
    }
}