using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 첫 고리 클릭 → 미리보기 → 다른 고리 클릭 순서로 실 발판을 만든다.
/// 같은 고리·빈 곳 클릭 또는 우클릭/Escape는 선택만 취소하고, 완성된 실은 유지한다.
/// 완성된 실을 클릭해 선택한 뒤 Q를 누르면 해당 실만 제거한다. 선택이 없으면 삭제하지 않는다.
/// 이 오브젝트와 고리·실의 부모는 회전 0, 크기 1인 2D 평면에서 사용하는 것을 기본으로 한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Lightknot/Hooks/Hook Connection Controller")]
public class HookConnectionController : MonoBehaviour
{
    private const float MinimumLength = 0.1f;

    [Header("마우스 입력")]
    [Tooltip("마우스 좌표를 게임 월드 좌표로 바꿀 카메라입니다. 비워두면 MainCamera 태그의 카메라를 사용합니다.")]
    [SerializeField] private Camera inputCamera;
    [Tooltip("이 관리자의 자식 고리 중 클릭할 레이어입니다. 기본값 Everything은 모든 레이어의 고리를 선택합니다.")]
    [SerializeField] private LayerMask hookLayers = ~0;

    [Header("실 연결 삭제")]
    [Tooltip("클릭해서 선택한 실 하나만 없애는 키입니다. 기본값은 Q이며 선택한 실이 없으면 아무것도 삭제하지 않습니다. 고리는 남아 다시 연결할 수 있습니다. None이면 단축키를 사용하지 않습니다.")]
    [SerializeField] private KeyCode removeThreadsKey = KeyCode.Q;
    [Tooltip("얇은 실을 쉽게 선택하도록 충돌체 바깥에 추가하는 클릭 여유 거리(유닛)입니다. 0이면 실 충돌체 안을 클릭해야 합니다. 물리 충돌 두께에는 영향을 주지 않습니다.")]
    [SerializeField, Min(0f)] private float threadClickPadding = 0.15f;

    [Header("생성할 실")]
    [Tooltip("완성된 실의 그림과 충돌체 두께(유닛)입니다. 새 연결부터 적용됩니다.")]
    [SerializeField, Min(0.02f)] private float threadThickness = 0.12f;
    [Tooltip("연결 가능한 최대 경사각(도)입니다. 수평은 0도이며, 너무 가파른 실이 벽이 되는 것을 막습니다. 기본값은 45도입니다.")]
    [SerializeField, Range(0f, 55f)] private float maximumSlope = 45f;
    [Tooltip("완성된 실의 색상입니다. 새 연결부터 적용됩니다.")]
    [SerializeField] private Color threadColor = new Color(0.85f, 0.97f, 1f, 1f);
    [Tooltip("고리와 실에 사용할 선 재질입니다. 데모 생성 도구가 자동으로 연결합니다.")]
    [SerializeField] private Material threadMaterial;
    [Tooltip("실 충돌체의 물리 재질입니다. 마찰 0인 PlayerNoFriction 재질을 권장하며 데모 생성 시 자동 연결됩니다.")]
    [SerializeField] private PhysicsMaterial2D threadPhysicsMaterial;

    [Header("연결 미리보기")]
    [Tooltip("마우스로 가리키는 연결이 가능한 경우의 미리보기 색상입니다. 미리보기에는 충돌체가 없습니다.")]
    [SerializeField] private Color previewColor = new Color(1f, 0.8f, 0.35f, 0.7f);
    [Tooltip("너무 가파르거나 플레이어 등 움직이는 몸체와 겹쳐 연결할 수 없을 때의 미리보기 색상입니다.")]
    [SerializeField] private Color blockedColor = new Color(1f, 0.3f, 0.3f, 0.8f);

    // 생성한 실과 질의 결과를 보관해 중복 연결과 매 프레임 배열 할당을 피한다.
    private readonly List<ThreadBridge> bridges = new List<ThreadBridge>();
    private readonly List<Collider2D> queryResults = new List<Collider2D>(16);
    private readonly List<HookAnchor> anchorCandidates = new List<HookAnchor>(8);
    private HookAnchor selectedAnchor;
    // 연결할 고리와 삭제할 실은 동시에 선택하지 않는다.
    private ThreadBridge selectedBridge;
    private LineRenderer preview;

    /// <summary>카메라와 이미 저장된 실을 찾는다. 선택 및 신규 생성은 클릭할 때만 수행한다.</summary>
    private void Awake()
    {
        if (inputCamera == null) inputCamera = Camera.main;
        bridges.AddRange(GetComponentsInChildren<ThreadBridge>(true));
    }

    /// <summary>카메라 이동을 반영한 마우스 위치로 고리를 찾고 클릭·취소·미리보기를 처리한다.</summary>
    private void Update()
    {
        // 마우스가 고리 위에 없어도 삭제할 수 있도록 카메라·마우스 질의보다 먼저 처리한다.
        if (removeThreadsKey != KeyCode.None && Input.GetKeyDown(removeThreadsKey))
        {
            RemoveSelectedConnection();
            return;
        }

        if (selectedAnchor != null && !selectedAnchor.isActiveAndEnabled) CancelSelection();
        if (selectedBridge != null && !selectedBridge.IsSelectable) CancelSelection();
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelSelection();
            return;
        }

        if (!TryGetMouseWorldPosition(out Vector3 mousePosition)) return;
        HookAnchor hovered = FindAnchorAt(mousePosition);
        if (Input.GetMouseButtonDown(0)) SelectAt(mousePosition, hovered);
        UpdatePreview(hovered != null ? hovered.ConnectionPoint : mousePosition);
    }

    /// <summary>화면상의 클릭 위치를 이 시스템의 Z 평면과 교차시킨다. 카메라가 이동해도 정확히 고리를 선택한다.</summary>
    private bool TryGetMouseWorldPosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (inputCamera == null) inputCamera = Camera.main;
        if (inputCamera == null || !inputCamera.pixelRect.Contains(Input.mousePosition)) return false;
        Ray ray = inputCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.forward, transform.position);
        if (!plane.Raycast(ray, out float distance)) return false;
        position = ray.GetPoint(distance);
        return true;
    }

    /// <summary>자식 고리의 클릭 영역을 직접 검사한다. 바닥과 겹치거나 전역 Trigger 질의가 꺼져 있어도 선택할 수 있다.</summary>
    public HookAnchor FindAnchorAt(Vector2 position)
    {
        GetComponentsInChildren(false, anchorCandidates);
        HookAnchor closest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (HookAnchor anchor in anchorCandidates)
        {
            if (!anchor.isActiveAndEnabled || (hookLayers.value & (1 << anchor.gameObject.layer)) == 0) continue;
            CircleCollider2D clickArea = anchor.GetComponent<CircleCollider2D>();
            if (!clickArea.enabled || !clickArea.OverlapPoint(position)) continue;
            float distance = ((Vector2)anchor.ConnectionPoint - position).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            closest = anchor;
            nearestDistance = distance;
        }
        return closest;
    }
    /// <summary>고리를 우선 선택하고, 고리가 없는 위치에서는 완성된 실을 선택한다. 빈 곳은 선택을 취소한다.</summary>
    public void SelectAt(Vector2 position) => SelectAt(position, FindAnchorAt(position));

    /// <summary>이미 찾은 고리를 재사용해 클릭 프레임의 자식 검색을 중복 수행하지 않는다.</summary>
    private void SelectAt(Vector2 position, HookAnchor anchor)
    {
        if (anchor != null) SelectAnchor(anchor);
        else SelectBridge(FindBridgeAt(position));
    }

    /// <summary>이 관리자의 유효한 실 중 클릭 위치와 가장 가까운 실을 찾는다. 경사진 실도 충돌체 형태대로 검사한다.</summary>
    public ThreadBridge FindBridgeAt(Vector2 position)
    {
        ThreadBridge closest = null;
        float nearestDistance = float.PositiveInfinity;
        float padding = Mathf.Max(0f, threadClickPadding);
        foreach (ThreadBridge bridge in bridges)
        {
            if (bridge == null || !bridge.IsSelectable) continue;
            float distance = (bridge.GetComponent<BoxCollider2D>().ClosestPoint(position) - position).sqrMagnitude;
            if (distance > padding * padding || distance >= nearestDistance) continue;
            closest = bridge;
            nearestDistance = distance;
        }
        return closest;
    }

    /// <summary>완성된 실 하나를 강조한다. 이전 실 선택과 고리 연결 미리보기를 정리하며 빈 곳은 선택을 해제한다.</summary>
    public void SelectBridge(ThreadBridge bridge)
    {
        CancelSelection();
        if (bridge == null || !bridges.Contains(bridge) || !bridge.IsSelectable) return;
        selectedBridge = bridge;
        selectedBridge.SetSelected(true);
    }

    /// <summary>첫 클릭은 선택, 두 번째 다른 고리 클릭은 연결이다. 빈 곳과 같은 고리를 누르면 선택을 취소한다.</summary>
    public void SelectAnchor(HookAnchor anchor)
    {
        if (selectedBridge != null) CancelSelection();
        if (anchor == null || anchor == selectedAnchor || !anchor.isActiveAndEnabled)
        {
            CancelSelection();
            return;
        }
        if (selectedAnchor == null)
        {
            selectedAnchor = anchor;
            selectedAnchor.SetSelected(true);
            return;
        }
        if (TryConnect(selectedAnchor, anchor)) CancelSelection();
    }

    /// <summary>서로 다른 두 고리를 연결한다. 같은 연결은 재사용하고, 몸체를 가두는 위치에는 생성하지 않는다.</summary>
    public bool TryConnect(HookAnchor first, HookAnchor second)
    {
        if (first == null || second == null || first == second
            || !first.isActiveAndEnabled || !second.isActiveAndEnabled) return false;

        for (int index = bridges.Count - 1; index >= 0; index--)
        {
            if (bridges[index] == null) bridges.RemoveAt(index);
            else if (bridges[index].Connects(first, second)) return true;
        }
        if (!CanBuild(first.ConnectionPoint, second.ConnectionPoint)) return false;

        GameObject bridgeObject = new GameObject($"Thread ({first.name} - {second.name})");
        bridgeObject.transform.SetParent(transform, false);
        ThreadBridge bridge = bridgeObject.AddComponent<ThreadBridge>();
        bridge.Initialize(first, second, threadThickness, threadColor, threadMaterial, threadPhysicsMaterial);
        bridges.Add(bridge);
        return true;
    }

    /// <summary>길이·경사·깊이를 검사하고 실 생성 위치에 플레이어 같은 Dynamic 몸체가 있는지 확인한다.</summary>
    private bool CanBuild(Vector3 first, Vector3 second)
    {
        Vector2 direction = second - first;
        float length = direction.magnitude;
        if (length < MinimumLength || Mathf.Abs(first.z - second.z) > 0.01f) return false;
        float slope = Mathf.Atan2(Mathf.Abs(direction.y), Mathf.Abs(direction.x)) * Mathf.Rad2Deg;
        if (slope > maximumSlope) return false;

        ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        Vector2 size = new Vector2(length, Mathf.Max(0.02f, threadThickness) + 0.02f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        gameObject.scene.GetPhysicsScene2D().OverlapBox((first + second) * 0.5f, size, angle, filter, queryResults);
        foreach (Collider2D collider in queryResults)
        {
            Rigidbody2D body = collider.attachedRigidbody;
            if (body != null && body.bodyType == RigidbodyType2D.Dynamic) return false;
        }
        return true;
    }

    /// <summary>충돌체가 없는 가는 선으로 연결 위치와 가능 여부를 표시한다.</summary>
    private void UpdatePreview(Vector3 destination)
    {
        if (selectedAnchor == null)
        {
            if (preview != null) preview.enabled = false;
            return;
        }
        if (preview == null)
        {
            GameObject previewObject = new GameObject("Thread Preview");
            previewObject.transform.SetParent(transform, false);
            preview = previewObject.AddComponent<LineRenderer>();
            preview.useWorldSpace = true;
            preview.positionCount = 2;
            preview.sortingOrder = 4;
            if (threadMaterial != null) preview.sharedMaterial = threadMaterial;
        }
        preview.enabled = true;
        preview.widthMultiplier = Mathf.Max(0.02f, threadThickness * 0.5f);
        preview.SetPosition(0, selectedAnchor.ConnectionPoint);
        preview.SetPosition(1, destination);
        preview.startColor = preview.endColor = CanBuild(selectedAnchor.ConnectionPoint, destination) ? previewColor : blockedColor;
    }

    /// <summary>선택된 실 하나만 삭제한다. 선택이 없으면 고리 연결 미리보기를 포함한 현재 상태를 유지한다.</summary>
    public void RemoveSelectedConnection()
    {
        if (selectedBridge == null) return;
        ThreadBridge bridge = selectedBridge;
        CancelSelection();
        // 삭제 직후 재연결할 수 있도록 해당 실만 목록에서 빼고, 지연 삭제 전에 충돌을 즉시 해제한다.
        bridges.Remove(bridge);
        bridge.gameObject.SetActive(false);
        Destroy(bridge.gameObject);
    }

    /// <summary>선택 표시와 미리보기만 정리한다. 이미 완성된 실 발판은 삭제하지 않는다.</summary>
    public void CancelSelection()
    {
        if (selectedAnchor != null) selectedAnchor.SetSelected(false);
        selectedAnchor = null;
        if (selectedBridge != null) selectedBridge.SetSelected(false);
        selectedBridge = null;
        if (preview != null) preview.enabled = false;
    }

    /// <summary>관리자가 꺼질 때 고리와 실이 선택 색상으로 남지 않도록 정리한다.</summary>
    private void OnDisable() => CancelSelection();
}
