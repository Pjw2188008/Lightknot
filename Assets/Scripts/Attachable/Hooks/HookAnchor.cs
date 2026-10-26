using UnityEngine;

/// <summary>고리 그림과 클릭 영역을 제공한다. 씬 뷰 기즈모로 크기와 클릭 범위를 표시하며 플레이어 이동은 막지 않는다.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Lightknot/Hooks/Hook Anchor")]
[RequireComponent(typeof(CircleCollider2D), typeof(LineRenderer))]
public class HookAnchor : MonoBehaviour
{
    private const int CircleSegments = 48;

    [Header("고리 모양 · 클릭 범위")]
    [Tooltip("표시할 고리의 반지름(유닛)입니다. 고리 크기만 바꾸며 클릭 범위는 Click Radius로 별도 설정합니다.")]
    [SerializeField, Min(0.05f)] private float ringRadius = 0.28f;
    [Tooltip("고리 테두리의 두께(유닛)입니다.")]
    [SerializeField, Min(0.01f)] private float ringThickness = 0.07f;
    [Tooltip("마우스로 선택할 수 있는 원형 범위의 반지름(유닛)입니다. 고리보다 조금 크게 설정하면 클릭하기 쉽습니다.")]
    [SerializeField, Min(0.05f)] private float clickRadius = 0.48f;

    [Header("선택 표시")]
    [Tooltip("선택하지 않은 일반 고리의 색상입니다. 기본값은 흰색이며 선택을 취소하거나 실을 삭제하면 이 색상으로 돌아옵니다.")]
    [SerializeField] private Color normalColor = Color.white;
    [Tooltip("첫 번째 연결 지점으로 선택했을 때의 색상입니다.")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.76f, 0.25f, 1f);
    [Tooltip("고리 선을 그릴 재질입니다. 비워두면 LineRenderer의 기존 재질을 사용합니다.")]
    [SerializeField] private Material ringMaterial;
    [Tooltip("고리의 렌더링 순서입니다. 같은 Sorting Layer에서 큰 값이 앞에 그려집니다.")]
    [SerializeField] private int sortingOrder = 5;

    [Header("씬 뷰 기즈모")]
    [Tooltip("씬 뷰에 흰색 고리 중심선과 노란색 연결 중심을 표시합니다. Scene 뷰의 Gizmos도 켜야 보이며 게임 빌드에는 포함되지 않습니다.")]
    [SerializeField] private bool showGizmos = true;
    [Tooltip("클릭 가능한 영역을 청록색 원으로 표시합니다. 고리 그림 크기와 별개이며 Show Gizmos가 켜져 있어야 보입니다.")]
    [SerializeField] private bool showClickRange = true;

    private LineRenderer ring;
    private bool selected;

    /// <summary>실이 연결될 고리 중심의 월드 좌표다.</summary>
    public Vector3 ConnectionPoint => transform.position;

    /// <summary>편집기 크기 안내와 실제 원주 생성에서 공통으로 사용하는 로컬 반지름이다.</summary>
    public float RingRadius => Mathf.Max(0.05f, ringRadius);

    /// <summary>CircleCollider2D처럼 X/Y 중 큰 월드 배율을 적용한 실제 클릭 반지름이다.</summary>
    public float WorldClickRadius => Mathf.Max(0.05f, clickRadius)
        * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));

    /// <summary>편집기에서 선택한 고리의 크기 안내를 표시할지 결정한다.</summary>
    public bool ShowGizmos => showGizmos;

    /// <summary>편집기 전용 기즈모가 클릭 범위를 그릴지 결정한다.</summary>
    public bool ShowClickRange => showClickRange;

    /// <summary>실행 시 저장된 설정으로 고리 그림과 클릭용 Trigger를 준비한다.</summary>
    private void Awake() => RefreshAppearance();

    /// <summary>편집기 생성 도구와 실행 코드가 공통으로 사용하는 고리 표시 초기화다.</summary>
    public void RefreshAppearance()
    {
        ring = GetComponent<LineRenderer>();
        CircleCollider2D clickArea = GetComponent<CircleCollider2D>();
        clickArea.isTrigger = true;
        clickArea.radius = Mathf.Max(0.05f, clickRadius);
        clickArea.offset = Vector2.zero;

        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = CircleSegments;
        ring.widthMultiplier = Mathf.Max(0.01f, ringThickness);
        ring.sortingOrder = sortingOrder;
        if (ringMaterial != null) ring.sharedMaterial = ringMaterial;

        // 별도 이미지 없이 원주 위의 점을 이어 테스트용 고리를 그린다.
        for (int index = 0; index < CircleSegments; index++)
        {
            float angle = index * Mathf.PI * 2f / CircleSegments;
            ring.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * RingRadius);
        }
        SetSelected(selected);
    }

    /// <summary>고리 색상만 변경한다. 클릭 판정과 실제 실 발판에는 영향을 주지 않는다.</summary>
    public void SetSelected(bool value)
    {
        selected = value;
        if (ring == null) ring = GetComponent<LineRenderer>();
        ring.startColor = ring.endColor = selected ? selectedColor : normalColor;
    }

}
