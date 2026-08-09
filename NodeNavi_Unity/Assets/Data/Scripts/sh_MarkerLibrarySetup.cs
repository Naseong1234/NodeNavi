#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Task 02에 필요한 6개 마커 이미지 라이브러리와 ARScene 기본 구조를 생성합니다.
/// Unity Editor 메뉴 NodeNavi > AR > Set Up Six Markers에서 한 번 실행합니다.
/// </summary>
public static class sh_MarkerLibrarySetup
{
    private const string ArScenePath = "Assets/Scene/ARScene.unity";
    private const string LibraryPath = "Assets/Data/XR/MarkerReferenceImageLibrary.asset";
    private const string ImageDirectory = "Assets/Data/Images/Marker_Image";
    private const float PrintedWidthInMeters = 0.15f;

    [MenuItem("NodeNavi/AR/Set Up Six Markers")]
    private static void SetUpSixMarkers()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        XRReferenceImageLibrary imageLibrary = CreateOrUpdateImageLibrary();
        if (imageLibrary == null)
            return;

        Scene arScene = EditorSceneManager.OpenScene(ArScenePath, OpenSceneMode.Single);
        GameObject xrOrigin = GameObject.Find("XR Origin");
        if (xrOrigin == null)
        {
            Debug.LogError("ARScene에서 'XR Origin'을 찾지 못했습니다.");
            return;
        }

        ARTrackedImageManager imageManager = xrOrigin.GetComponent<ARTrackedImageManager>();
        if (imageManager == null)
            imageManager = Undo.AddComponent<ARTrackedImageManager>(xrOrigin);

        imageManager.referenceLibrary = imageLibrary;

        GameObject buildingContentRoot = FindOrCreateRoot("BuildingContentRoot");
        GameObject virtualMarkers = FindOrCreateChild(buildingContentRoot.transform, "VirtualMarkers");
        FindOrCreateChild(buildingContentRoot.transform, "RouteContentRoot");

        for (int index = 1; index <= 6; index++)
            FindOrCreateChild(virtualMarkers.transform, $"Marker_{index:00}");

        EditorSceneManager.MarkSceneDirty(arScene);
        EditorSceneManager.SaveScene(arScene);
        Selection.activeObject = imageLibrary;
        Debug.Log("마커 라이브러리 6개와 ARScene 기본 구조를 설정했습니다. 가상 마커 위치·회전은 실측값으로 조정하세요.");
    }

    private static XRReferenceImageLibrary CreateOrUpdateImageLibrary()
    {
        XRReferenceImageLibrary library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        while (library.count > 0)
            library.RemoveAt(0);

        for (int index = 1; index <= 6; index++)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ImageDirectory}/Marker{index}.png");
            if (texture == null)
            {
                Debug.LogError($"Marker{index}.png 파일을 찾지 못했습니다.");
                return null;
            }

            library.Add();
            int libraryIndex = library.count - 1;
            library.SetTexture(libraryIndex, texture, true);
            library.SetName(libraryIndex, $"Marker_{index:00}");
            library.SetSpecifySize(libraryIndex, true);
            library.SetSize(libraryIndex, new Vector2(PrintedWidthInMeters, PrintedWidthInMeters * texture.height / texture.width));
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        return library;
    }

    private static GameObject FindOrCreateRoot(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        return existing != null ? existing : new GameObject(objectName);
    }

    private static GameObject FindOrCreateChild(Transform parent, string objectName)
    {
        Transform child = parent.Find(objectName);
        if (child != null)
            return child.gameObject;

        GameObject created = new GameObject(objectName);
        created.transform.SetParent(parent, false);
        return created;
    }
}
#endif
