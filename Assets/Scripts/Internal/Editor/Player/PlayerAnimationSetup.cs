using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools 메뉴에서 Player의 이동·물리·Animator·카메라 연결과 테스트 발판을 구성한다.
/// Editor 폴더 전용 도구이므로 게임 빌드에 포함되지 않으며 Inspector용 필드는 없다.
/// 기존 애니메이션 클립과 Controller는 재사용해 사용자가 넣은 스프라이트를 보존한다.
/// </summary>
public static class PlayerAnimationSetup
{
    // 에셋 경로와 오브젝트 이름은 생성·검색 시 동일한 값을 사용하도록 한곳에 모은다.
    private const string PlayerFolder = "Assets/Player";
    private const string AnimationFolder = PlayerFolder + "/Animations";
    private const string ControllerPath = AnimationFolder + "/Player.controller";
    private const string MaterialPath = PlayerFolder + "/PlayerNoFriction.physicsMaterial2D";
    private const string PlayerObjectName = "Player";
    private const string PlatformGroupName = "Jump Platforms";

    // SerializedObject로 접근하는 기존 필드 이름이다. PlayerMove/CameraFollow와 함께 유지한다.
    private const string SpriteRendererProperty = "spriteRenderer";
    private const string JumpSpeedProperty = "jumpSpeed";
    private const string CameraTargetProperty = "target";

    // 샘플 발판의 개수, 실제 월드 크기, 좌우 간격, 높이 차이다.
    private const int PlatformCount = 3;
    private const float PlatformWidth = 2.2f;
    private const float PlatformHeight = 0.32f;
    private const float FirstPlatformX = 2.6f;
    private const float PlatformSpacingX = 2.4f;
    private const float PlatformStepHeight = 1.05f;

    /// <summary>현재 씬의 Player를 찾아 필요한 컴포넌트를 연결하고 에셋과 씬을 저장한다.</summary>
    [MenuItem("Tools/Setup Player Animations")]
    public static void Setup()
    {
        Scene scene = GetEditableScene();
        GameObject player = FindPlayer(scene);

        ConfigurePlayerComponents(player);
        EnsureFloorColliders(scene);
        AssignAnimatorController(player, GetOrCreateController());
        ConfigurePlayerPhysics(player);
        ConfigureExistingPlatforms(scene);
        ConfigureCamera(scene, player.transform);

        SaveSceneAndAssets(scene);
        Selection.activeGameObject = player;
    }

    /// <summary>가장 넓은 Floor의 모양을 이용해 3단 발판을 만든다. 같은 그룹이 있으면 중복 생성하지 않는다.</summary>
    [MenuItem("Tools/Create Jump Platforms")]
    public static void CreatePlatforms()
    {
        Scene scene = GetEditableScene();
        if (FindPlatformGroup(scene) != null) return;

        // 생성 전에 필수 대상을 검사해 대상 누락으로 발판 일부만 만들어지는 일을 피한다.
        GameObject player = FindPlayer(scene);
        SpriteRenderer floor = FindPlatformSource(scene);
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        PlayerMove move = player.GetComponent<PlayerMove>();
        if (body == null || move == null)
            throw new InvalidOperationException("먼저 Tools > Setup Player Animations로 플레이어를 설정하세요.");

        GameObject group = new GameObject(PlatformGroupName);
        Undo.RegisterCreatedObjectUndo(group, "점프 발판 생성");
        SceneManager.MoveGameObjectToScene(group, scene);

        for (int index = 0; index < PlatformCount; index++) CreatePlatform(group.transform, floor, index);

        ApplySampleJumpSettings(body, move);
        Physics2D.SyncTransforms();
        SaveSceneAndAssets(scene);
        SceneView.RepaintAll();
    }

    /// <summary>플레이 중 설정이 저장되거나 사라지는 것을 막고 현재 편집 중인 씬을 반환한다.</summary>
    private static Scene GetEditableScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play 모드를 종료한 후 설정 도구를 실행하세요.");

        return SceneManager.GetActiveScene();
    }

    /// <summary>지정한 씬의 루트와 자식을 검색한다. 다른 열린 씬의 오브젝트는 변경하지 않는다.</summary>
    private static IEnumerable<T> GetSceneComponents<T>(Scene scene, bool includeInactive = true) where T : Component
        => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(includeInactive));

    /// <summary>이름이 Player인 대상을 유일하게 찾는다. 중복이나 누락 시 잘못된 대상 수정을 중단한다.</summary>
    private static GameObject FindPlayer(Scene scene)
    {
        Transform[] players = GetSceneComponents<Transform>(scene)
            .Where(item => item.name == PlayerObjectName).ToArray();

        if (players.Length != 1) throw new InvalidOperationException("현재 씬에는 이름이 Player인 오브젝트가 정확히 하나 있어야 합니다.");

        return players[0].gameObject;
    }

    /// <summary>기존 컴포넌트를 재사용하고, 없을 때만 Undo 가능한 방식으로 추가한다.</summary>
    private static T GetOrAddComponent<T>(GameObject owner) where T : Component
    {
        T component = owner.GetComponent<T>();
        // Unity 오브젝트는 자체 null 판정을 사용하므로 ?? 대신 == null로 확인한다.
        return component == null ? Undo.AddComponent<T>(owner) : component;
    }

    /// <summary>이동 스크립트와 충돌체를 준비하고 SpriteRenderer를 Inspector 참조에 연결한다.</summary>
    private static void ConfigurePlayerComponents(GameObject player)
    {
        SpriteRenderer sprite = player.GetComponent<SpriteRenderer>();
        if (sprite == null) throw new InvalidOperationException("Player에 SpriteRenderer가 필요합니다.");

        PlayerMove move = GetOrAddComponent<PlayerMove>(player);

        if (player.GetComponent<Collider2D>() == null) Undo.AddComponent<BoxCollider2D>(player);

        SerializedObject properties = new SerializedObject(move);
        properties.FindProperty(SpriteRendererProperty).objectReferenceValue = sprite;
        properties.ApplyModifiedProperties();
    }

    /// <summary>그림만 있고 충돌체가 없는 Floor에 충돌체를 추가한다. 기존 충돌체는 유지한다.</summary>
    private static void EnsureFloorColliders(Scene scene)
    {
        foreach (SpriteRenderer floor in GetSceneComponents<SpriteRenderer>(scene))
        {
            if (floor.CompareTag(PlayerMove.FloorTag) && floor.GetComponent<Collider2D>() == null)
                Undo.AddComponent<BoxCollider2D>(floor.gameObject);
        }
    }

    /// <summary>기존 Controller를 우선 사용하고, 없을 때만 기본 3개 상태와 전환을 만든다.</summary>
    private static AnimatorController GetOrCreateController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null) return controller;

        if (!AssetDatabase.IsValidFolder(AnimationFolder)) AssetDatabase.CreateFolder(PlayerFolder, "Animations");

        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter(PlayerMove.MovingParameterName, AnimatorControllerParameterType.Bool);
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = PlayerMove.GroundedParameterName,
            type = AnimatorControllerParameterType.Bool,
            defaultBool = true
        });
        CreateAnimationStates(controller.layers[0].stateMachine);
        return controller;
    }

    /// <summary>Idle을 기본으로 하고, 지상 이동과 공중 상태에 따라 3개 클립이 전환되도록 구성한다.</summary>
    private static void CreateAnimationStates(AnimatorStateMachine machine)
    {
        // Animator 창에서 상태와 연결선을 읽기 쉽도록 배치한다.
        machine.entryPosition = new Vector3(20, 100, 0);
        machine.anyStatePosition = new Vector3(20, 300, 0);
        machine.exitPosition = new Vector3(750, 100, 0);

        AnimatorState idle = CreateState(machine, "Idle", true, new Vector3(250, 100, 0));
        AnimatorState moving = CreateState(machine, "Move", true, new Vector3(500, 100, 0));
        AnimatorState jump = CreateState(machine, "Jump", false, new Vector3(375, 300, 0));
        machine.defaultState = idle;

        AddGroundedTransition(idle, moving, true);
        AddGroundedTransition(moving, idle, false);
        AddGroundedTransition(jump, idle, false);
        AddGroundedTransition(jump, moving, true);

        // 점프하거나 발판에서 떨어지면 현재 지상 상태와 관계없이 Jump로 전환한다.
        AnimatorStateTransition toJump = machine.AddAnyStateTransition(jump);
        ConfigureInstantTransition(toJump);
        toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, PlayerMove.GroundedParameterName);
    }

    /// <summary>상태를 만들고 이름이 같은 기존 클립을 연결한다. 없는 클립만 빈 상태로 생성한다.</summary>
    private static AnimatorState CreateState(AnimatorStateMachine machine, string name, bool loop, Vector3 position)
    {
        string path = AnimationFolder + "/" + name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = name, frameRate = 12f };
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
        }

        AnimatorState state = machine.AddState(name, position);
        state.motion = clip;
        state.writeDefaultValues = false;
        return state;
    }

    /// <summary>접지 상태와 이동 여부를 함께 검사하는 Idle/Move/착지 전환을 추가한다.</summary>
    private static void AddGroundedTransition(AnimatorState from, AnimatorState to, bool moving)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        ConfigureInstantTransition(transition);
        transition.AddCondition(AnimatorConditionMode.If, 0f, PlayerMove.GroundedParameterName);
        transition.AddCondition(moving ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            0f, PlayerMove.MovingParameterName);
    }

    /// <summary>클립 종료 대기와 혼합 지연을 없애 입력에 즉시 반응하는 스프라이트 전환으로 설정한다.</summary>
    private static void ConfigureInstantTransition(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.duration = 0f;
        transition.canTransitionToSelf = false;
    }

    /// <summary>Player의 Animator에 Controller를 연결하고 이동은 물리 코드가 담당하도록 Root Motion을 끈다.</summary>
    private static void AssignAnimatorController(GameObject player, AnimatorController controller)
    {
        Animator animator = GetOrAddComponent<Animator>(player);
        Undo.RecordObject(animator, "플레이어 Animator 연결");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
    }

    /// <summary>마찰 없는 재질과 연속 충돌 검사를 설정해 벽 달라붙음과 얇은 바닥 관통을 줄인다.</summary>
    private static void ConfigurePlayerPhysics(GameObject player)
    {
        PhysicsMaterial2D material = GetOrCreateFrictionlessMaterial();

        Rigidbody2D body = GetOrAddComponent<Rigidbody2D>(player);
        Undo.RecordObject(body, "플레이어 충돌 설정");
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        body.sharedMaterial = material;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        foreach (Collider2D collider in player.GetComponentsInChildren<Collider2D>())
        {
            // 감지용 Trigger와 별도 Rigidbody 소유의 자식 충돌체는 변경하지 않는다.
            if (collider.attachedRigidbody != body || collider.isTrigger) continue;

            Undo.RecordObject(collider, "플레이어 충돌체 마찰 제거");
            collider.sharedMaterial = material;
        }
    }

    /// <summary>MainCamera 태그의 카메라에 Player를 연결한다. 기존 오프셋과 추적 속도는 유지한다.</summary>
    private static void ConfigureCamera(Scene scene, Transform target)
    {
        Camera camera = GetSceneComponents<Camera>(scene, false)
            .FirstOrDefault(item => item.CompareTag("MainCamera"));
        if (camera == null) return;

        CameraFollow follow = GetOrAddComponent<CameraFollow>(camera.gameObject);
        SerializedObject properties = new SerializedObject(follow);
        properties.FindProperty(CameraTargetProperty).objectReferenceValue = target;
        properties.ApplyModifiedProperties();
    }

    /// <summary>이 도구가 만든 발판 그룹을 찾아 기본 바닥과 구별한다.</summary>
    private static GameObject FindPlatformGroup(Scene scene)
        => scene.GetRootGameObjects().FirstOrDefault(item => item.name == PlatformGroupName);

    /// <summary>발판 그룹의 기존 통과 설정을 제거하고 마찰 없는 일반 충돌체로 설정한다.</summary>
    private static void ConfigureExistingPlatforms(Scene scene)
    {
        GameObject group = FindPlatformGroup(scene);
        if (group == null) return;

        foreach (BoxCollider2D collider in group.GetComponentsInChildren<BoxCollider2D>()) ConfigurePlatform(collider);
    }

    /// <summary>발판의 모든 면에서 충돌을 유지하고, 마찰로 옆면에 매달리는 현상을 방지한다.</summary>
    private static void ConfigurePlatform(BoxCollider2D collider)
    {
        Undo.RecordObject(collider, "발판 일반 충돌과 마찰 설정");
        collider.usedByEffector = false;
        collider.isTrigger = false;
        collider.sharedMaterial = GetOrCreateFrictionlessMaterial();

        // 예전에 추가한 일방향 통과 기능만 제거한다. 발판 충돌체 자체는 계속 활성화한다.
        PlatformEffector2D effector = collider.GetComponent<PlatformEffector2D>();
        if (effector != null) Undo.DestroyObjectImmediate(effector);
    }

    /// <summary>플레이어와 발판이 공유하는 마찰·반발력 0의 물리 재질을 생성하거나 재사용한다.</summary>
    private static PhysicsMaterial2D GetOrCreateFrictionlessMaterial()
    {
        PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
        if (material == null)
        {
            material = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        return material;
    }
    /// <summary>가장 넓은 Floor의 스프라이트와 재질을 새 발판의 시각적 원본으로 선택한다.</summary>
    private static SpriteRenderer FindPlatformSource(Scene scene)
    {
        SpriteRenderer floor = GetSceneComponents<SpriteRenderer>(scene)
            .Where(item => item.CompareTag(PlayerMove.FloorTag) && item.sprite != null)
            .OrderByDescending(item => item.bounds.size.x).FirstOrDefault();
        if (floor == null) throw new InvalidOperationException("발판을 만들려면 스프라이트가 지정된 Floor 바닥이 필요합니다.");

        return floor;
    }

    /// <summary>월드 크기가 일정한 발판 하나를 만들고 Floor 태그와 마찰 없는 일반 충돌체를 지정한다.</summary>
    private static void CreatePlatform(Transform parent, SpriteRenderer source, int index)
    {
        GameObject platform = new GameObject("Platform " + (index + 1));
        platform.transform.SetParent(parent);
        platform.tag = PlayerMove.FloorTag;
        platform.transform.position = new Vector3(FirstPlatformX + index * PlatformSpacingX,
            source.bounds.max.y + PlatformStepHeight * (index + 1) - PlatformHeight * 0.5f, 0f);

        SpriteRenderer sprite = platform.AddComponent<SpriteRenderer>();
        sprite.sprite = source.sprite;
        sprite.sharedMaterial = source.sharedMaterial;
        sprite.sortingLayerID = source.sortingLayerID;
        sprite.sortingOrder = source.sortingOrder;
        // 높은 발판일수록 조금 더 밝은 청록색으로 구별한다.
        sprite.color = new Color(0.23f + index * 0.07f, 0.74f + index * 0.04f, 0.79f + index * 0.05f, 1f);
        platform.transform.localScale = new Vector3(PlatformWidth / sprite.sprite.bounds.size.x,
            PlatformHeight / sprite.sprite.bounds.size.y, 1f);
        ConfigurePlatform(platform.AddComponent<BoxCollider2D>());
    }

    /// <summary>새 샘플 발판의 높이 차이에 맞춰 초기 점프 속도와 중력을 설정한다.</summary>
    private static void ApplySampleJumpSettings(Rigidbody2D body, PlayerMove move)
    {
        Undo.RecordObject(body, "샘플 발판용 점프 중력 설정");
        body.gravityScale = 2f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        SerializedObject properties = new SerializedObject(move);
        properties.FindProperty(JumpSpeedProperty).floatValue = 8f;
        properties.ApplyModifiedProperties();
    }

    /// <summary>생성한 에셋과 수정한 씬을 저장하며, 저장 실패 시 성공으로 보고하지 않는다.</summary>
    private static void SaveSceneAndAssets(Scene scene)
    {
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("씬을 저장하지 못했습니다. 저장 경로를 확인하세요.");
    }
}
