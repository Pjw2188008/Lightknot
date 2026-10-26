using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Tools 메뉴에서 테스트용 A/B 고리와 실 연결 관리자를 배치한다. 기존 플레이어·발판은 이동하지 않는다.</summary>
public static class HookThreadSetup
{
    private const string RootName = "Hook Thread System";
    private const string MaterialPath = "Assets/Threads/Thread.mat";

    /// <summary>가장 넓은 Floor 위의 점프 가능한 높이에 고리 두 개를 배치하고 씬을 저장한다.</summary>
    [MenuItem("Tools/Create Hook Thread Demo")]
    public static void CreateDemo()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play 모드를 종료한 뒤 고리 데모를 생성하세요.");
        Scene scene = SceneManager.GetActiveScene();
        GameObject existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == RootName);
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            return;
        }
        SpriteRenderer floor = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>())
            .Where(sprite => sprite.CompareTag(PlayerMove.FloorTag))
            .OrderByDescending(sprite => sprite.bounds.size.x).FirstOrDefault();
        if (floor == null) throw new InvalidOperationException("고리 위치를 정할 Floor 바닥이 필요합니다.");

        Vector3 start = new Vector3(floor.bounds.center.x - 1.4f, floor.bounds.max.y + 1.17f, 0f);
        GameObject rig = CreateRig(start, start + Vector3.right * 5f);
        Undo.RegisterCreatedObjectUndo(rig, "A/B 고리 데모 생성");
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("고리 데모 씬을 저장하지 못했습니다.");
        Selection.activeGameObject = rig;
    }

    /// <summary>지정한 두 월드 좌표에 독립된 연결 시스템을 만든다. 실은 아직 생성하지 않고 클릭을 기다린다.</summary>
    public static GameObject CreateRig(Vector3 firstPosition, Vector3 secondPosition)
    {
        Material material = GetOrCreateLineMaterial();
        PhysicsMaterial2D physics = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Player/PlayerNoFriction.physicsMaterial2D");
        GameObject rig = new GameObject(RootName);
        HookConnectionController controller = rig.AddComponent<HookConnectionController>();
        SerializedObject settings = new SerializedObject(controller);
        settings.FindProperty("threadMaterial").objectReferenceValue = material;
        settings.FindProperty("threadPhysicsMaterial").objectReferenceValue = physics;
        settings.ApplyModifiedPropertiesWithoutUndo();

        CreateAnchor(rig.transform, "Hook A", "A", firstPosition, Color.white, material);
        CreateAnchor(rig.transform, "Hook B", "B", secondPosition, Color.white, material);
        return rig;
    }

    /// <summary>고리 모양을 초기화하고 A/B를 구분하는 작은 월드 좌표 글자를 추가한다.</summary>
    private static void CreateAnchor(Transform parent, string name, string label, Vector3 position, Color color, Material material)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        HookAnchor anchor = obj.AddComponent<HookAnchor>();
        SerializedObject settings = new SerializedObject(anchor);
        settings.FindProperty("ringMaterial").objectReferenceValue = material;
        settings.FindProperty("normalColor").colorValue = color;
        settings.ApplyModifiedPropertiesWithoutUndo();
        anchor.RefreshAppearance();

        GameObject textObject = new GameObject("Label " + label);
        textObject.transform.SetParent(obj.transform, false);
        textObject.transform.localPosition = new Vector3(0f, 0.52f, -0.01f);
        TextMesh text = textObject.AddComponent<TextMesh>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 48;
        text.characterSize = 0.12f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;
        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = text.font.material;
        renderer.sortingOrder = 6;
    }

    /// <summary>2D 조명에 가려지지 않는 선 재질을 준비한다. URP가 없는 테스트 프로젝트에서는 기본 스프라이트 셰이더를 쓴다.</summary>
    private static Material GetOrCreateLineMaterial()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Threads")) AssetDatabase.CreateFolder("Assets", "Threads");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) throw new InvalidOperationException("고리와 실을 표시할 셰이더를 찾을 수 없습니다.");
        material = new Material(shader) { name = "Thread" };
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }
}
