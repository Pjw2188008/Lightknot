using UnityEngine;

/// <summary>두 고리를 잇는 실 그림과 일반 충돌체를 함께 관리한다. Floor 태그를 사용해 이동·점프를 지원한다.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("")]
[RequireComponent(typeof(LineRenderer), typeof(BoxCollider2D))]
public class ThreadBridge : MonoBehaviour
{
    [Header("연결된 고리")]
    [Tooltip("실의 시작 고리입니다. 연결 관리자가 클릭한 고리를 자동으로 지정합니다.")]
    [SerializeField] private HookAnchor firstAnchor;
    [Tooltip("실의 끝 고리입니다. 시작 고리와 다른 고리를 연결해야 합니다.")]
    [SerializeField] private HookAnchor secondAnchor;

    [Header("실 발판")]
    [Tooltip("실 그림과 충돌체에 함께 적용할 두께(유닛)입니다. 값이 클수록 발판이 두꺼워집니다.")]
    [SerializeField, Min(0.02f)] private float thickness = 0.12f;
    [Tooltip("완성된 실의 색상입니다.")]
    [SerializeField] private Color threadColor = new Color(0.85f, 0.97f, 1f, 1f);
    [Tooltip("실을 그릴 재질입니다. 비워두면 LineRenderer의 기존 재질을 사용합니다.")]
    [SerializeField] private Material threadMaterial;
    [Tooltip("실의 물리 재질입니다. 마찰이 0인 재질을 연결하면 옆면에 달라붙는 현상을 줄일 수 있습니다.")]
    [SerializeField] private PhysicsMaterial2D physicsMaterial;

    [Header("선택 표시")]
    [Tooltip("클릭해서 삭제 대상으로 선택한 실의 색상입니다. 선택을 해제하면 원래 실 색상으로 돌아갑니다.")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.76f, 0.25f, 1f);

    private LineRenderer line;
    private BoxCollider2D surface;
    // 물리 갱신으로 선을 다시 그려도 선택 표시를 유지한다.
    private bool selected;

    /// <summary>숨겨졌거나 연결이 유효하지 않은 실은 클릭으로 선택하지 않는다.</summary>
    public bool IsSelectable => isActiveAndEnabled && line != null && line.enabled
        && surface != null && surface.enabled && HasValidAnchors;

    /// <summary>선택 판정과 물리 갱신이 같은 고리 유효성 조건을 사용한다.</summary>
    private bool HasValidAnchors => firstAnchor != null && secondAnchor != null && firstAnchor != secondAnchor
        && firstAnchor.isActiveAndEnabled && secondAnchor.isActiveAndEnabled;

    /// <summary>삭제 대상 실의 선택 상태와 색상을 즉시 갱신한다.</summary>
    public void SetSelected(bool value)
    {
        selected = value;
        if (line == null) line = GetComponent<LineRenderer>();
        line.startColor = line.endColor = selected ? selectedColor : threadColor;
    }

    /// <summary>생성 직후 고리와 표현 설정을 전달받고 즉시 밟을 수 있는 충돌체를 만든다.</summary>
    public void Initialize(HookAnchor first, HookAnchor second, float width, Color color,
        Material visualMaterial, PhysicsMaterial2D collisionMaterial)
    {
        firstAnchor = first;
        secondAnchor = second;
        thickness = Mathf.Max(0.02f, width);
        threadColor = color;
        threadMaterial = visualMaterial;
        physicsMaterial = collisionMaterial;
        RefreshGeometry();
    }

    /// <summary>이미 연결한 두 고리인지 순서와 무관하게 확인해 중복 발판 생성을 막는다.</summary>
    public bool Connects(HookAnchor first, HookAnchor second)
    {
        return (firstAnchor == first && secondAnchor == second)
            || (firstAnchor == second && secondAnchor == first);
    }

    /// <summary>씬에 미리 저장된 실도 실행 시 같은 초기화 과정을 거친다.</summary>
    private void Awake() => RefreshGeometry();

    /// <summary>고리 위치가 바뀌면 다음 물리 처리 전에 실의 길이·각도·충돌체를 맞춘다.</summary>
    private void FixedUpdate() => RefreshGeometry();

    /// <summary>두 점 사이의 중점·길이·각도로 그림과 충돌체를 일치시킨다. 고리가 없으면 발판을 끈다.</summary>
    public void RefreshGeometry()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (surface == null) surface = GetComponent<BoxCollider2D>();
        bool valid = HasValidAnchors;
        Vector2 difference = valid ? (Vector2)(secondAnchor.ConnectionPoint - firstAnchor.ConnectionPoint) : Vector2.zero;
        valid &= difference.sqrMagnitude > 0.0025f;
        line.enabled = surface.enabled = valid;
        if (!valid) return;

        // 고리와 완성된 실은 2D 평면에서 사용한다. 실 오브젝트의 부모는 크기 1을 유지한다.
        transform.position = (firstAnchor.ConnectionPoint + secondAnchor.ConnectionPoint) * 0.5f;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
        transform.localScale = Vector3.one;
        float length = difference.magnitude;
        gameObject.tag = PlayerMove.FloorTag;
        surface.isTrigger = false;
        surface.usedByEffector = false;
        surface.sharedMaterial = physicsMaterial;
        surface.offset = Vector2.zero;
        surface.size = new Vector2(length, Mathf.Max(0.02f, thickness));

        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = 2;
        line.SetPosition(0, new Vector3(-length * 0.5f, 0f, 0f));
        line.SetPosition(1, new Vector3(length * 0.5f, 0f, 0f));
        line.widthMultiplier = surface.size.y;
        SetSelected(selected);
        line.sortingOrder = 3;
        if (threadMaterial != null) line.sharedMaterial = threadMaterial;
    }
}
