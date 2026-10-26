# Lightknot 개발 안내

Unity 6 기반 2D 프로젝트입니다. 플레이어 이동·가변 높이 점프·카메라 추적과 고리를 실 발판으로 연결하는 기능이 있습니다.

## 스크립트 폴더

`Assets/Scripts`는 **직접 붙이는 스크립트(Attachable)**와 **자동 처리 코드(Internal)**로 나누고, 그 안에서 기능별로 구분합니다.

```text
Assets/Scripts/
├─ README.md                       # 팀원용 스크립트 구성·사용 안내
├─ Attachable/                     # 씬·프리팹에 직접 붙이는 컴포넌트
│  ├─ Player/PlayerMove.cs
│  ├─ Camera/CameraFollow.cs
│  └─ Hooks/
│     ├─ HookAnchor.cs
│     └─ HookConnectionController.cs
└─ Internal/                       # 직접 붙일 필요 없는 코드
   ├─ Hooks/ThreadBridge.cs         # 관리자가 실 생성 시 자동 추가
   └─ Editor/                      # Unity 편집기 전용, 빌드에서 제외
      ├─ Player/PlayerAnimationSetup.cs
      └─ Hooks/
         ├─ HookAnchorEditor.cs
         └─ HookThreadSetup.cs
```

### 직접 붙이는 기준

| 대상 오브젝트 | 붙일 스크립트 |
| --- | --- |
| Player | `Attachable/Player/PlayerMove.cs` |
| Main Camera | `Attachable/Camera/CameraFollow.cs` |
| 각 고리 또는 고리 프리팹 | `Attachable/Hooks/HookAnchor.cs` |
| 고리들을 묶는 부모 | `Attachable/Hooks/HookConnectionController.cs` |

Inspector의 **Add Component > Lightknot**에서도 위 네 컴포넌트를 찾을 수 있습니다. 기존 씬에 이미 붙어 있다면 다시 추가할 필요가 없습니다.

`Internal`은 폴더의 사용 목적을 나타내며 C# 접근 제한자와는 별개입니다. `ThreadBridge`도 MonoBehaviour이지만 실을 만들 때 관리자가 자동으로 붙이므로 직접 추가하지 않습니다. Add Component 메뉴에서도 숨겼습니다. `Internal/Editor`의 도구는 메뉴 또는 고리 Inspector를 통해 자동 실행되며 오브젝트에 붙이지 않습니다. `Internal/Hooks`는 실행 중 필요하므로 게임 빌드에 포함됩니다.

### 이번 리팩토링 기준

짧은 위임 함수와 조건문을 간결하게 정리하고, 입력 누적·상태 초기화와 중복 유효성 검사를 정리했습니다. 클릭 프레임에는 이미 찾은 고리를 재사용합니다. 고리 기즈모 그리기는 `HookAnchorEditor`로 옮겨 편집기 표시와 런타임 기능을 분리했습니다. 한국어 역할 주석과 Inspector 툴팁, 기존 직렬화 필드 이름, 스크립트 GUID는 유지합니다.

애니메이션과 물리 재질은 `Assets/Player`, 고리 데모 프리팹과 선 재질은 `Assets/Threads`, 씬은 `Assets/Scenes`에 있습니다. 이 경로는 편집기 생성 도구에서도 사용합니다.

## 스크립트별 역할

| 스크립트 | 붙이는 위치 / 실행 위치 | 담당 기능 |
| --- | --- | --- |
| [PlayerMove.cs](Attachable/Player/PlayerMove.cs) | Player | A/D 지상·공중 이동, Space 가변 높이 점프, 좌우 스프라이트 플립, Floor 접지 확인, Animator 파라미터 전달. |
| [CameraFollow.cs](Attachable/Camera/CameraFollow.cs) | Main Camera | 지정한 대상을 LateUpdate에서 부드럽게 추적. 오프셋과 추적 시간을 설정할 수 있음. |
| [HookAnchor.cs](Attachable/Hooks/HookAnchor.cs) | 각 고리 | 고리 모양, 클릭용 Trigger, 선택 색상, 실 연결 중심 제공. 씬 뷰에 크기와 클릭 범위 기즈모 표시. |
| [HookConnectionController.cs](Attachable/Hooks/HookConnectionController.cs) | 고리들을 묶는 부모 | 고리 클릭과 연결 미리보기, 실 생성, 중복·경사·몸체 겹침 검사, 완성된 실 선택과 Q 삭제. |
| [ThreadBridge.cs](Internal/Hooks/ThreadBridge.cs) | 실행 중 생성되는 실 | 두 고리 사이의 선과 단단한 BoxCollider2D 발판 생성·갱신. Floor 태그와 선택 강조 표시 관리. |
| [PlayerAnimationSetup.cs](Internal/Editor/Player/PlayerAnimationSetup.cs) | Tools 메뉴 | Player의 이동·충돌·Animator·카메라 연결 설정과 테스트 점프 발판 생성. 오브젝트에 붙이지 않음. |
| [HookThreadSetup.cs](Internal/Editor/Hooks/HookThreadSetup.cs) | Tools 메뉴 | A/B 고리와 연결 관리자가 있는 데모 배치. 선 재질과 물리 재질 연결. 오브젝트에 붙이지 않음. |
| [HookAnchorEditor.cs](Internal/Editor/Hooks/HookAnchorEditor.cs) | 고리 Inspector / Scene 뷰 | Inspector 수정 즉시 고리 그림과 클릭 범위를 갱신. 다중 선택, Undo/Redo, 프리팹 오버라이드 지원. 선택한 고리의 지름 표시. 자동으로 적용되며 직접 붙이지 않음. |

## 조작

| 입력 | 동작 |
| --- | --- |
| A / D | 좌우 이동. 공중에서도 조작 가능. |
| Space | Floor 위에서 점프. 짧게 누르면 낮게, 길게 누르면 높게 점프. |
| 고리 → 다른 고리 클릭 | 두 고리 사이에 실 발판 생성. |
| 같은 고리 재클릭 | 진행 중인 고리 선택 취소. |
| 완성된 실 클릭 | 해당 실을 금색으로 선택. |
| Q | 선택한 실 하나만 삭제. 실 선택이 없으면 아무것도 삭제하지 않음. |
| 빈 곳 클릭 / 우클릭 / Esc | 선택과 연결 미리보기 해제. 완성된 실은 유지. |

Space를 계속 누른다고 착지 후 자동으로 다시 점프하지 않습니다. 공중 재점프나 키를 다시 눌러 상승을 회복하는 기능은 없습니다.

## 고리 배치와 기즈모

1. 빈 부모 오브젝트에 `HookConnectionController`를 붙입니다.
2. 그 아래에 `HookAnchor`가 붙은 고리 두 개 이상을 배치합니다. 같은 고리 프리팹을 복제해도 됩니다. `CircleCollider2D`와 `LineRenderer`는 필수 컴포넌트로 자동 추가됩니다.
3. 고리와 연결 관리자에 선 재질을 지정하고, 관리자의 실 물리 재질에는 `Assets/Player/PlayerNoFriction.physicsMaterial2D`를 연결합니다. 필요하면 `Tools > Create Hook Thread Demo`로 기본 구성을 만듭니다.
4. Scene 뷰의 **Gizmos**를 켭니다. 고리의 **Show Gizmos**도 켜져 있어야 합니다. 고리를 선택하면 지름 수치가 나타납니다.
5. Inspector에서 반지름·테두리 두께·클릭 반지름을 조절합니다. 재생하지 않아도 실제 그림과 클릭 영역이 갱신됩니다.

| 표시 / 설정 | 의미 |
| --- | --- |
| 흰색 원 | 고리 선의 중심 반지름을 나타내는 원. Transform을 반영하며 테두리 두께는 실제 고리 그림으로 확인. |
| 청록색 원 | 마우스 클릭 영역. `Click Radius`와 X/Y 중 큰 월드 배율을 반영. `Show Click Range`로 숨길 수 있음. |
| 노란 십자 | 실이 이어지는 고리 중심. |
| 고리 지름 안내 | 고리 중심선 기준 로컬 X/Y 방향의 월드 길이. 테두리 두께는 제외. |
| 클릭 지름 안내 | 실제 클릭 원의 월드 지름. |
| Ring Radius | 고리 그림의 로컬 반지름. 기본값 0.28이므로 배율 1에서 중심선 지름은 0.56유닛. |
| Ring Thickness | 고리 선의 두께. 기본값 0.07유닛. |
| Click Radius | 클릭 원의 로컬 반지름. 기본값 0.48이므로 배율 1에서 클릭 지름은 0.96유닛. |

기즈모는 Scene 뷰 편집 안내입니다. Game 뷰에서 Gizmos를 켜면 Unity가 그 뷰에도 안내를 표시할 수 있지만 게임 빌드에는 나오지 않습니다. 고리 크기는 Transform 배율보다 `Ring Radius`로 조절하는 방식을 권장합니다. 연결 시스템은 XY 평면에서 사용하고, 실의 부모는 회전 0·크기 1을 유지하세요.

고리들은 같은 연결 관리자의 자식이어야 합니다. 중간에 묶음 오브젝트를 추가해도 됩니다. 서로 다른 관리자가 맡은 고리끼리는 클릭으로 연결하지 않습니다. 클릭 영역이 겹치면 가까운 고리가 우선 선택되고, 실 끝에서는 고리 선택이 실 선택보다 우선합니다.

## 실 발판 설정

- `Thread Thickness`: 새 실의 그림과 충돌체 두께. 실은 모든 면에서 충돌하는 일반 발판입니다.
- `Maximum Slope`: 연결 가능한 최대 경사각. 기본값 45도이며 수직에 가까운 실 생성을 제한합니다.
- `Thread Click Padding`: 얇은 실을 선택하기 위한 추가 클릭 여유 거리. 기본값 0.15유닛이며 충돌체 두께는 바꾸지 않습니다.
- `Remove Threads Key`: 선택한 실을 삭제하는 키. 기본 Q, None이면 단축키 비활성화.
- 고리의 높이(Z)가 다르거나 너무 가까운 경우, 실 생성 위치가 플레이어 같은 Dynamic Rigidbody2D와 겹치는 경우에는 연결하지 않습니다.
- 연결 미리보기는 가능할 때 금색, 불가능할 때 붉은색이며 충돌체가 없습니다.
- 같은 고리 쌍은 중복 연결하지 않습니다. 실을 삭제하면 즉시 충돌이 해제되고 같은 고리를 다시 연결할 수 있습니다.

## 플레이어·애니메이션·카메라

Player에는 `PlayerMove`, `Rigidbody2D`, 비 Trigger `Collider2D`를 사용합니다. 밟을 수 있는 바닥에는 **Floor** 태그를 지정합니다. 벽이나 천장 접촉은 점프 가능한 바닥으로 취급하지 않습니다. 마찰 0 물리 재질과 연속 충돌 검사는 발판 옆면에 달라붙거나 얇은 발판을 통과하는 현상을 줄입니다.

`Move Speed`, `Jump Speed`, Rigidbody2D의 `Gravity Scale`, `Jump Cut Multiplier`로 이동과 점프감을 조절합니다. `Jump Cut Multiplier`는 상승 중 Space를 놓았을 때 남길 세로 속도 비율입니다.

Animator는 **Idle / Move / Jump** 상태를 사용합니다. `PlayerMove`가 `IsMoving`, `IsGrounded` Bool 파라미터를 전달하고 Controller가 상태를 전환합니다. 스프라이트 시트의 프레임은 사용자가 AnimationClip에 직접 넣습니다. 코드가 스프라이트 프레임을 교체하지 않습니다.

Main Camera의 `CameraFollow.Target`에 Player를 연결합니다. `Offset`은 화면 중심과 촬영 깊이, `Smooth Time`은 추적의 부드러움을 조절합니다. Target이 비어 있으면 카메라가 이동하지 않습니다.

## 편집기 도구

| 메뉴 | 결과 |
| --- | --- |
| Tools > Setup Player Animations | 현재 씬의 Player 컴포넌트, Animator, 물리 설정, 카메라 연결을 구성하고 저장. 기존 클립과 Controller는 재사용. |
| Tools > Create Jump Platforms | Floor 모양을 이용한 3단 테스트 발판 생성과 샘플 점프값 설정, 씬 저장. 이미 그룹이 있으면 중복 생성하지 않음. |
| Tools > Create Hook Thread Demo | Floor 위에 Hook Thread System과 A/B 고리 배치, 씬 저장. 이미 시스템이 있으면 해당 오브젝트 선택. 실은 플레이 중 클릭으로 생성. |

도구 메뉴는 Play 모드를 종료한 뒤 사용합니다. 실제 씬 설정을 바꾸고 저장하는 메뉴이므로 시험 배치에는 별도 테스트 씬을 사용할 수 있습니다.

## 새 기능을 추가할 때

- 직접 붙이는 컴포넌트는 `Assets/Scripts/Attachable/<기능>`, 자동 생성 컴포넌트·내부 런타임 코드는 `Assets/Scripts/Internal/<기능>`, 편집기 도구는 `Assets/Scripts/Internal/Editor/<기능>`에 둡니다.
- 클래스·메서드·핵심 처리에는 한국어 주석을, Inspector 필드에는 한국어 Tooltip을 작성합니다.
- 스크립트 이동 시 `.meta`를 함께 옮겨 GUID와 씬·프리팹 참조를 유지합니다. 클래스 이름이나 직렬화된 필드를 변경할 때는 기존 연결과 설정의 이전을 고려합니다.
- 새 스크립트를 추가하거나 역할을 바꾸면 이 README의 스크립트 목록과 사용법도 함께 갱신합니다.
