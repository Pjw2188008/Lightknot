using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A/D 이동, Space를 누른 시간에 따른 가변 높이 점프, 좌우 플립과 Animator 상태를 관리한다.
/// 지상과 공중에서 좌우 조작이 가능하며, 점프는 Floor 위에 서 있을 때만 허용한다.
/// 스프라이트 프레임은 코드에서 교체하지 않고 사용자가 작성한 AnimationClip으로 재생한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Lightknot/Player/Player Move")]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMove : MonoBehaviour
{
    // 편집기 설정 도구와 실제 이동 코드가 같은 태그와 Animator 파라미터 이름을 사용한다.
    public const string FloorTag = "Floor";
    public const string MovingParameterName = "IsMoving";
    public const string GroundedParameterName = "IsGrounded";

    // 위쪽을 향한 접촉만 바닥으로 인정한다. 수직 벽과 천장은 점프 지지대가 될 수 없다.
    private const float MinimumGroundNormalY = 0.5f;
    private const float VelocityTolerance = 0.01f;

    [Header("이동 · 점프")]
    [Tooltip("A/D를 눌렀을 때의 좌우 이동 속도(유닛/초)입니다. 공중에서도 동일하게 적용됩니다. 0이면 좌우 이동을 멈춥니다.")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;

    [Tooltip("Space를 눌렀을 때의 위쪽 초기 속도(유닛/초)입니다. 계속 누르면 이 속도와 Gravity Scale로 정해지는 최대 높이까지 올라갑니다. 0이면 점프하지 않습니다.")]
    [SerializeField, Min(0f)] private float jumpSpeed = 8f;

    [Header("가변 점프 높이")]
    [Tooltip("상승 중 Space를 놓았을 때 남길 세로 속도의 비율입니다. 0이면 상승을 즉시 멈추고, 1이면 높이를 줄이지 않습니다. 작을수록 짧게 눌렀을 때 더 낮게 뜁니다. 한 점프에 한 번만 적용되며 다시 눌러도 상승이 회복되지 않습니다.")]
    [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.45f;

    [Header("캐릭터 표시 · 방향")]
    [Tooltip("좌우로 뒤집을 SpriteRenderer입니다. 비워두면 시작할 때 플레이어 자신과 활성 자식에서 자동으로 찾습니다. 콜라이더는 뒤집지 않습니다.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Flip X가 꺼진 원본 스프라이트가 오른쪽을 바라보면 체크하세요. 원본이 왼쪽을 바라보면 체크를 해제해야 이동 방향과 캐릭터 방향이 일치합니다.")]
    [SerializeField] private bool spriteFacesRight = true;

    // 접촉 목록을 재사용해 매 물리 프레임마다 불필요한 리스트 생성을 피한다.
    private readonly List<ContactPoint2D> floorContacts = new List<ContactPoint2D>(8);

    // Animator 파라미터 이름을 한 번만 숫자 ID로 변환해 반복 문자열 조회를 줄인다.
    private static readonly int IsMoving = Animator.StringToHash(MovingParameterName);
    private static readonly int IsGrounded = Animator.StringToHash(GroundedParameterName);

    // 시작 시 찾은 컴포넌트를 보관한다. Animator가 없어도 이동과 점프는 동작한다.
    private Animator animator;
    private Rigidbody2D body;

    // Update에서 읽은 입력을 FixedUpdate가 소비한다. 양쪽 키를 동시에 누르면 0이다.
    private float moveInput;
    private bool jumpRequested;

    // 누르고 있는 상태와 키를 놓은 순간을 별도로 보관해 물리 프레임 사이의 짧은 탭도 반영한다.
    private bool jumpHeld;
    private bool jumpCutRequested;

    // 직접 시작한 점프의 상승 중에만 한 번 높이를 줄인다. 낙하나 외부 힘에는 적용하지 않는다.
    private bool canCutJump;

    // 도약 직후의 오래된 접촉만 무시한다. 경사진 실을 걸어 올라갈 때는 상승 속도가 있어도 접지를 인정한다.
    private bool takingOff;

    /// <summary>필요한 컴포넌트를 찾고 물리 충돌로 캐릭터가 회전하지 않도록 고정한다.</summary>
    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>화면 프레임마다 입력을 읽어 짧게 누른 Space도 다음 물리 처리까지 보관한다.</summary>
    private void Update()
    {
        moveInput = (Input.GetKey(KeyCode.D) ? 1f : 0f)
                  - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        jumpHeld = Input.GetKey(KeyCode.Space);

        // 다음 물리 프레임까지 입력을 누적해 짧은 탭도 놓치지 않는다.
        jumpRequested |= Input.GetKeyDown(KeyCode.Space);
        jumpCutRequested |= Input.GetKeyUp(KeyCode.Space);
    }

    /// <summary>일정한 물리 시간 간격으로 이동·점프를 계산하고 표시 방향과 Animator를 갱신한다.</summary>
    private void FixedUpdate()
    {
        bool hasGroundContact = IsStandingOnFloor();
        // 실제로 바닥에서 떨어지거나 상승이 끝나면 다음 착지부터 정상 접지 판정을 사용한다.
        if (takingOff && (!hasGroundContact || body.linearVelocity.y <= VelocityTolerance)) takingOff = false;
        bool grounded = hasGroundContact && !takingOff;
        Vector2 velocity = body.linearVelocity;

        // 가로 속도만 입력으로 결정한다. 세로 속도는 중력과 충돌 계산 결과를 유지한다.
        velocity.x = moveInput * moveSpeed;
        if (TryJump(ref velocity, grounded)) grounded = false;

        ApplyJumpHeight(ref velocity, grounded);
        body.linearVelocity = velocity;

        UpdateFacingDirection();
        UpdateAnimation(grounded, velocity.x);
    }

    /// <summary>점프 입력을 한 번 소비하고, 바닥에서 도약 속도를 적용했다면 true를 반환한다.</summary>
    private bool TryJump(ref Vector2 velocity, bool grounded)
    {
        bool canJump = grounded && jumpRequested && jumpSpeed > 0f;
        // 공중에서 누른 Space는 저장하지 않는다. 착지 후 새로 눌러야 다시 점프한다.
        jumpRequested = false;

        if (!canJump) return false;

        velocity.y = jumpSpeed;
        canCutJump = takingOff = true;
        return true;
    }

    /// <summary>상승 중 키를 놓으면 남은 상승 속도를 한 번 줄이고, 정점 이후에는 자연 낙하를 유지한다.</summary>
    private void ApplyJumpHeight(ref Vector2 velocity, bool grounded)
    {
        // 착지·정점·천장 충돌로 상승이 끝났다면 이전 점프의 높이 조절 권한도 끝낸다.
        if (grounded || velocity.y <= 0f) canCutJump = false;

        if (canCutJump && (jumpCutRequested || !jumpHeld))
        {
            // 길게 누를 때는 추가 힘을 주지 않으므로 최대 높이가 무한히 늘어나지 않는다.
            velocity.y *= Mathf.Clamp01(jumpCutMultiplier);
            canCutJump = false;
        }

        // 키를 뗐다가 다시 눌러도 같은 점프의 상승 속도를 복구하거나 여러 번 줄이지 않는다.
        jumpCutRequested = false;
    }

    /// <summary>입력 방향에 맞춰 그림만 뒤집는다. 정지하면 마지막으로 바라보던 방향을 유지한다.</summary>
    private void UpdateFacingDirection()
    {
        if (moveInput == 0f || spriteRenderer == null) return;

        spriteRenderer.flipX = spriteFacesRight ? moveInput < 0f : moveInput > 0f;
    }

    /// <summary>접지와 이동 여부를 전달한다. 실제 클립 선택은 Animator Controller의 전환 조건이 담당한다.</summary>
    private void UpdateAnimation(bool grounded, float horizontalVelocity)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        // 공중에서는 IsGrounded=false가 Jump를 선택하므로 이동 애니메이션 조건은 지상에서만 켠다.
        animator.SetBool(IsGrounded, grounded);
        animator.SetBool(IsMoving, grounded && Mathf.Abs(horizontalVelocity) > VelocityTolerance);
    }

    /// <summary>활성화된 접촉 중 Floor 태그를 가진 표면이 아래에서 플레이어를 지지하는지 확인한다.</summary>
    private bool IsStandingOnFloor()
    {
        body.GetContacts(floorContacts);
        foreach (ContactPoint2D contact in floorContacts)
        {
            // 비활성 접촉과 벽·천장 접촉은 제외하고, 실제로 발밑을 지지하는 접촉만 사용한다.
            if (contact.enabled && contact.normal.y > MinimumGroundNormalY
                && contact.collider.CompareTag(FloorTag)) return true;
        }

        return false;
    }

    /// <summary>컴포넌트를 끄면 입력과 가로 속도를 초기화한다. 낙하에 필요한 세로 속도는 유지한다.</summary>
    private void OnDisable()
    {
        moveInput = 0f;
        jumpRequested = jumpHeld = jumpCutRequested = canCutJump = takingOff = false;

        if (body != null) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }
}
