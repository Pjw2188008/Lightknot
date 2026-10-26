using UnityEngine;

/// <summary>
/// 지정한 대상의 좌우 이동과 점프를 따라가는 2D 카메라다.
/// 시작할 때 대상 위치로 맞추고, 이후에는 LateUpdate에서 부드럽게 추적한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Lightknot/Camera/Camera Follow")]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("추적 대상 · 화면 위치")]
    [Tooltip("카메라가 따라갈 대상의 Transform입니다. 현재 씬의 Player를 연결하세요. 비어 있으면 카메라는 이동하지 않습니다.")]
    [SerializeField] private Transform target;

    [Tooltip("대상에서 카메라까지의 월드 좌표 거리입니다. X는 좌우, Y는 위아래 화면 중심, Z는 촬영 깊이입니다. 일반적인 2D 씬에서는 Z를 -10으로 사용합니다.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f);

    [Header("추적 움직임")]
    [Tooltip("목표 위치를 따라잡는 부드러움의 시간 값(초)입니다. 작을수록 빠르게, 클수록 느긋하게 따라갑니다. 0이면 지연 없이 즉시 이동합니다. 정확한 도착 시간을 뜻하지는 않습니다.")]
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;

    // SmoothDamp가 이전 프레임의 움직임을 이어 계산할 수 있도록 보관하는 내부 속도다.
    private Vector3 followVelocity;

    /// <summary>다시 활성화할 때 이전 추적 속도가 남아 카메라가 튀는 것을 방지한다.</summary>
    private void OnEnable() => followVelocity = Vector3.zero;

    /// <summary>첫 화면에서 원래 카메라 위치부터 이동하는 연출 없이 곧바로 대상을 보여준다.</summary>
    private void Start()
    {
        if (target != null) transform.position = GetTargetPosition();
    }

    /// <summary>플레이어의 프레임 갱신 후 카메라를 이동시켜 추적 시점 차이로 생기는 흔들림을 줄인다.</summary>
    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 destination = GetTargetPosition();
        Vector3 position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, destination, ref followVelocity, smoothTime)
            : destination;

        // X/Y만 부드럽게 움직이고 Z는 항상 지정한 상대 깊이를 유지한다.
        position.z = destination.z;
        transform.position = position;
    }

    /// <summary>플레이어 위치에 월드 좌표 오프셋을 더해 카메라가 향할 위치를 구한다.</summary>
    private Vector3 GetTargetPosition() => target.position + offset;
}
