#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(sh_MarkerRouteController))]
public class sh_MarkerRouteControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        sh_MarkerRouteController controller = (sh_MarkerRouteController)target;
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Task04 Test Current Route Order"))
                controller.RunEditorRouteOrderTest();
        }

        if (!EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("플레이 모드에서만 테스트 버튼이 동작합니다.", MessageType.Info);
    }
}
#endif
