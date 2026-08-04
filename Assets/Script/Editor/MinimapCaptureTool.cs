using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// 미니맵 배경 이미지를 뽑기 위한 에디터 툴 — 씬을 위에서 내려다보는 직교 카메라로 한 번 렌더링해서
/// PNG로 저장한다. 지형이 바뀌면 언제든 다시 눌러서 재생성할 수 있다.
/// 캡처된 월드 좌표 범위(중심/크기)는 런타임 미니맵 스크립트의 월드→UV 변환 공식에 그대로
/// 재사용해야 하므로, 캡처 후 콘솔과 창에 함께 표시한다.
/// </summary>
public class MinimapCaptureTool : EditorWindow
{
    [Tooltip("비워두면 씬의 모든 Renderer를 합친 범위를 사용. 지정하면 그 하위 Renderer만 기준으로 범위를 계산")]
    private Transform boundsRoot;
    private LayerMask cullingMask = ~0;
    private int resolution = 2048;
    private float paddingMultiplier = 1.05f;
    private float cameraHeightMargin = 50f;
    private string outputFolder = "Assets/Textures/Minimap";

    private Bounds _lastCapturedBounds;
    private bool _hasCaptured;

    [MenuItem("Tools/Minimap/Capture Top-Down Map")]
    private static void Open()
    {
        GetWindow<MinimapCaptureTool>("Minimap Capture").minSize = new Vector2(360, 320);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("캡처 범위", EditorStyles.boldLabel);
        boundsRoot = (Transform)EditorGUILayout.ObjectField(
            new GUIContent("범위 기준 오브젝트 (선택)", "비워두면 씬 전체 Renderer 기준"),
            boundsRoot, typeof(Transform), true);
        cullingMask = LayerMaskField("촬영에 포함할 레이어", cullingMask);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("캡처 설정", EditorStyles.boldLabel);
        resolution = EditorGUILayout.IntField("해상도 (정사각형, px)", resolution);
        paddingMultiplier = EditorGUILayout.FloatField("여백 배율", paddingMultiplier);
        cameraHeightMargin = EditorGUILayout.FloatField("카메라 높이 여유(m)", cameraHeightMargin);
        outputFolder = EditorGUILayout.TextField("저장 폴더", outputFolder);

        EditorGUILayout.Space();
        if (GUILayout.Button("캡처", GUILayout.Height(32)))
            Capture();

        if (_hasCaptured)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("마지막 캡처 월드 범위 (런타임 스크립트에 그대로 사용)", EditorStyles.boldLabel);
            EditorGUILayout.Vector2Field("중심 (X, Z)", new Vector2(_lastCapturedBounds.center.x, _lastCapturedBounds.center.z));
            EditorGUILayout.Vector2Field("크기 (Width, Depth)", new Vector2(_lastCapturedBounds.size.x, _lastCapturedBounds.size.z));
        }
    }

    private static LayerMask LayerMaskField(string label, LayerMask mask)
    {
        var layers = UnityEditorInternal.InternalEditorUtility.layers;
        int layerNumbers = 0;
        for (int i = 0; i < layers.Length; i++)
            layerNumbers |= 1 << LayerMask.NameToLayer(layers[i]);

        int maskWithoutEmpty = InternalMaskToField(mask, layers);
        int newMaskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers);
        return FieldToInternalMask(newMaskWithoutEmpty, layers);
    }

    private static int InternalMaskToField(LayerMask mask, string[] layers)
    {
        int result = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            int layerIndex = LayerMask.NameToLayer(layers[i]);
            if ((mask.value & (1 << layerIndex)) != 0) result |= 1 << i;
        }
        return result;
    }

    private static LayerMask FieldToInternalMask(int field, string[] layers)
    {
        int mask = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            if ((field & (1 << i)) != 0)
                mask |= 1 << LayerMask.NameToLayer(layers[i]);
        }
        return mask;
    }

    private void Capture()
    {
        Bounds bounds = ComputeBounds();
        if (bounds.size.x <= 0f || bounds.size.z <= 0f)
        {
            Debug.LogError("[MinimapCaptureTool] 캡처 범위를 계산하지 못했습니다 (Renderer가 없습니다).");
            return;
        }

        float half = Mathf.Max(bounds.extents.x, bounds.extents.z) * paddingMultiplier;

        var camGo = new GameObject("__MinimapCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = half;
        cam.cullingMask = cullingMask;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = bounds.size.y + cameraHeightMargin * 2f + 10f;

        camGo.transform.position = new Vector3(bounds.center.x, bounds.max.y + cameraHeightMargin, bounds.center.z);
        camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 2
        };
        cam.targetTexture = rt;

        RenderTexture prevActive = RenderTexture.active;
        try
        {
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            tex.Apply();

            SaveTexture(tex);
            Object.DestroyImmediate(tex);
        }
        finally
        {
            RenderTexture.active = prevActive;
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
        }

        _lastCapturedBounds = new Bounds(bounds.center, new Vector3(half * 2f, bounds.size.y, half * 2f));
        _hasCaptured = true;

        Debug.Log($"[MinimapCaptureTool] 캡처 완료 — 중심: ({_lastCapturedBounds.center.x:F2}, {_lastCapturedBounds.center.z:F2}), " +
                  $"크기: ({_lastCapturedBounds.size.x:F2} x {_lastCapturedBounds.size.z:F2}). " +
                  "런타임 미니맵 스크립트의 월드→UV 변환에 이 값을 사용하세요.");
    }

    private Bounds ComputeBounds()
    {
        Renderer[] renderers = boundsRoot != null
            ? boundsRoot.GetComponentsInChildren<Renderer>()
            : Object.FindObjectsOfType<Renderer>();

        Bounds bounds = default;
        bool initialized = false;

        foreach (var r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!initialized) { bounds = r.bounds; initialized = true; }
            else bounds.Encapsulate(r.bounds);
        }

        return bounds;
    }

    private void SaveTexture(Texture2D tex)
    {
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string fileName = $"{sceneName}_Minimap.png";
        string fullPath = Path.Combine(outputFolder, fileName);

        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        AssetDatabase.Refresh();

        var importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        var asset = AssetDatabase.LoadAssetAtPath<Object>(fullPath);
        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
    }
}
