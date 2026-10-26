using UnityEditor;
using UnityEngine;

/// <summary>고리 설정을 편집 즉시 그림·클릭 영역에 반영하고 선택한 고리의 월드 크기를 안내한다. 빌드에는 포함되지 않는다.</summary>
[CustomEditor(typeof(HookAnchor)), CanEditMultipleObjects]
public class HookAnchorEditor : Editor
{
    /// <summary>선택 여부와 관계없이 크기를 안내한다. DrawingScope가 색상과 변환을 자동 복구한다.</summary>
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawSizeGizmos(HookAnchor anchor, GizmoType gizmoType)
    {
        if (!anchor.ShowGizmos) return;
        using (new Handles.DrawingScope(Color.white, anchor.transform.localToWorldMatrix))
            Handles.DrawWireDisc(Vector3.zero, Vector3.forward, anchor.RingRadius);
        // 클릭 원은 CircleCollider2D와 같은 월드 반지름을 사용한다.
        using (new Handles.DrawingScope(Color.cyan, Matrix4x4.Translate(anchor.ConnectionPoint)))
            if (anchor.ShowClickRange) Handles.DrawWireDisc(Vector3.zero, Vector3.forward, anchor.WorldClickRadius);
        using (new Handles.DrawingScope(Color.yellow, Matrix4x4.Translate(anchor.ConnectionPoint)))
        {
            Handles.DrawLine(Vector3.left * 0.06f, Vector3.right * 0.06f);
            Handles.DrawLine(Vector3.down * 0.06f, Vector3.up * 0.06f);
        }
    }

    /// <summary>실행 전에도 고리를 처음 선택하는 순간 현재 설정에 맞는 모습을 보여준다.</summary>
    private void OnEnable()
    {
        Undo.undoRedoPerformed += RefreshAfterUndo;
        RefreshTargets(false);
    }

    /// <summary>Inspector가 닫힐 때 편집기 이벤트를 해제한다.</summary>
    private void OnDisable()
    {
        Undo.undoRedoPerformed -= RefreshAfterUndo;
    }

    /// <summary>기본 Inspector의 툴팁과 다중 편집을 유지하면서 값 변경을 실제 컴포넌트에도 적용한다.</summary>
    public override void OnInspectorGUI()
    {
        if (DrawDefaultInspector()) RefreshTargets(true);
        EditorGUILayout.HelpBox("씬 뷰에서 Gizmos를 켜세요. 흰색: 고리 중심선 / 청록색: 클릭 범위 / 노란 십자: 실 연결 중심. 크기는 유닛 기준입니다. 고리 테두리 두께는 실제 LineRenderer 그림에 반영됩니다.", MessageType.Info);
    }

    /// <summary>Undo/Redo로 복원된 설정을 고리 그림과 클릭 판정에도 반영한다.</summary>
    private void RefreshAfterUndo() => RefreshTargets(false);

    /// <summary>선택한 모든 고리를 갱신한다. 파생 컴포넌트 변경도 Undo와 프리팹 오버라이드에 포함한다.</summary>
    private void RefreshTargets(bool recordUndo)
    {
        foreach (Object item in targets)
        {
            HookAnchor anchor = item as HookAnchor;
            if (anchor == null) continue;
            LineRenderer line = anchor.GetComponent<LineRenderer>();
            CircleCollider2D collider = anchor.GetComponent<CircleCollider2D>();
            if (recordUndo) Undo.RecordObjects(new Object[] { line, collider }, "고리 모양 및 클릭 범위 변경");
            anchor.RefreshAppearance();
            if (recordUndo)
            {
                EditorUtility.SetDirty(line);
                EditorUtility.SetDirty(collider);
                if (PrefabUtility.IsPartOfPrefabInstance(anchor))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(line);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                }
            }
        }
        SceneView.RepaintAll();
    }

    /// <summary>선택한 고리 옆에 부모 배율을 반영한 지름을 표시한다. 흰색 중심선 기준이며 테두리 두께는 제외한다.</summary>
    private void OnSceneGUI()
    {
        HookAnchor anchor = (HookAnchor)target;
        if (anchor == null || !anchor.ShowGizmos) return;
        SceneView view = SceneView.currentDrawingSceneView;
        if (view != null && !view.drawGizmos) return;
        float width = anchor.transform.TransformVector(Vector3.right * anchor.RingRadius * 2f).magnitude;
        float height = anchor.transform.TransformVector(Vector3.up * anchor.RingRadius * 2f).magnitude;
        float clickDiameter = anchor.WorldClickRadius * 2f;
        Vector3 labelPosition = anchor.ConnectionPoint + Vector3.up * (Mathf.Max(height, clickDiameter) * 0.5f + 0.15f);
        Handles.Label(labelPosition, $"고리 지름: {width:0.##} × {height:0.##} 유닛\n클릭 지름: {clickDiameter:0.##} 유닛");
    }
}
