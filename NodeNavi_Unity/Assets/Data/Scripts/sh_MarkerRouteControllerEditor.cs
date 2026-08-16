#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(sh_MarkerRouteController))]
public class sh_MarkerRouteControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        sh_MarkerRouteController controller = (sh_MarkerRouteController)target;
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("선택한 번호로 경로 활성화 & 건물 위치 정렬 테스트", GUILayout.Height(30)))
            {
                controller.RunEditorRouteOrderTest();
            }
        }

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이(▶) 모드에서 위 버튼을 누르면 설정한 번호(editorTestRouteOrder)에 맞추어 선로 활성화와 BuildingContentRoot 위치 이동이 테스트됩니다.", MessageType.Info);
        }
    }
}
#endif
