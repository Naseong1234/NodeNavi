using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class sh_MarkerRouteController : MonoBehaviour
{
    private const int RequiredMarkerCount = 6;
    private const int MaxVisibleRouteCount = 3;

    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private Transform buildingContentRoot;
    [SerializeField] private Transform routeContentRoot;
    [SerializeField] private List<sh_MarkerRouteData> markerRoutes = new List<sh_MarkerRouteData>(RequiredMarkerCount);
    [SerializeField] private int editorTestRouteOrder;

    private bool isSubscribed;
    private bool isInitialized;
    private bool hasAlignedBuildingRoot;

    private void Awake()
    {
        InitializeRoutePool();
    }

    private void OnEnable()
    {
        SubscribeTrackedImageEvents();
    }

    private void OnDisable()
    {
        UnsubscribeTrackedImageEvents();
    }

    private void InitializeRoutePool()
    {
        if (isInitialized)
            return;

        if (!ValidateConfiguration())
            return;

        for (int index = 0; index < markerRoutes.Count; index++)
        {
            sh_MarkerRouteData routeData = markerRoutes[index];
            GameObject instance = Instantiate(routeData.RoutePrefab, routeContentRoot);
            instance.name = $"{routeData.RoutePrefab.name}_Runtime";
            instance.SetActive(false);
            routeData.RuntimeInstance = instance;
        }

        isInitialized = true;
        Debug.Log($"[sh_MarkerRouteController] 경로 prefab {markerRoutes.Count}개를 한 번만 생성하고 비활성화했습니다.", this);
    }

    private bool ValidateConfiguration()
    {
        if (trackedImageManager == null)
        {
            Debug.LogError("[sh_MarkerRouteController] AR Tracked Image Manager 참조가 비어 있습니다.", this);
            return false;
        }

        if (buildingContentRoot == null)
        {
            Debug.LogError("[sh_MarkerRouteController] Building Content Root 참조가 비어 있습니다.", this);
            return false;
        }

        if (routeContentRoot == null)
        {
            Debug.LogError("[sh_MarkerRouteController] Route Content Root 참조가 비어 있습니다.", this);
            return false;
        }

        if (markerRoutes == null || markerRoutes.Count != RequiredMarkerCount)
        {
            Debug.LogError($"[sh_MarkerRouteController] 마커 데이터는 정확히 {RequiredMarkerCount}개여야 합니다.", this);
            return false;
        }

        HashSet<string> markerNames = new HashSet<string>();
        HashSet<int> routeOrders = new HashSet<int>();

        for (int index = 0; index < markerRoutes.Count; index++)
        {
            sh_MarkerRouteData routeData = markerRoutes[index];

            if (routeData == null)
            {
                Debug.LogError($"[sh_MarkerRouteController] Marker Routes {index}번 항목이 비어 있습니다.", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(routeData.MarkerName))
            {
                Debug.LogError($"[sh_MarkerRouteController] Marker Routes {index}번의 markerName이 비어 있습니다.", this);
                return false;
            }

            if (!markerNames.Add(routeData.MarkerName))
            {
                Debug.LogError($"[sh_MarkerRouteController] 중복 markerName이 있습니다: {routeData.MarkerName}", this);
                return false;
            }

            if (routeData.KnownMarkerTransform == null)
            {
                Debug.LogError($"[sh_MarkerRouteController] {routeData.MarkerName}의 knownMarkerTransform이 비어 있습니다.", this);
                return false;
            }

            if (routeData.RoutePrefab == null)
            {
                Debug.LogError($"[sh_MarkerRouteController] {routeData.MarkerName}의 routePrefab이 비어 있습니다.", this);
                return false;
            }

            if (!routeOrders.Add(routeData.RouteOrder))
            {
                Debug.LogError($"[sh_MarkerRouteController] 중복 routeOrder가 있습니다: {routeData.RouteOrder}", this);
                return false;
            }

            if (routeData.RouteOrder < 0 || routeData.RouteOrder >= RequiredMarkerCount)
            {
                Debug.LogError($"[sh_MarkerRouteController] {routeData.MarkerName}의 routeOrder는 0부터 {RequiredMarkerCount - 1} 사이여야 합니다.", this);
                return false;
            }
        }

        return true;
    }

    private void SubscribeTrackedImageEvents()
    {
        if (isSubscribed)
            return;

        if (!isInitialized)
            return;

        if (trackedImageManager == null)
            return;

        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        isSubscribed = true;
    }

    private void UnsubscribeTrackedImageEvents()
    {
        if (!isSubscribed || trackedImageManager == null)
            return;

        trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        isSubscribed = false;
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        HandleTrackedImages(eventArgs.added, "added");
        HandleTrackedImages(eventArgs.updated, "updated");
    }

    private void HandleTrackedImages(IReadOnlyList<ARTrackedImage> trackedImages, string source)
    {
        for (int index = 0; index < trackedImages.Count; index++)
        {
            ARTrackedImage trackedImage = trackedImages[index];
            if (trackedImage == null || trackedImage.trackingState != TrackingState.Tracking)
                continue;

            string markerName = trackedImage.referenceImage.name;
            if (!TryGetRouteData(markerName, out sh_MarkerRouteData routeData))
            {
                Debug.LogWarning($"[sh_MarkerRouteController] 마커 데이터와 일치하지 않는 이미지 이름입니다: {markerName}", trackedImage.gameObject);
                continue;
            }

            if (!hasAlignedBuildingRoot)
                AlignBuildingContentRoot(trackedImage.transform, routeData);

            SetActiveRoutes(routeData.RouteOrder);
            Debug.Log($"[sh_MarkerRouteController] trackedImagesChanged {source}: {markerName}, routeOrder={routeData.RouteOrder}", trackedImage.gameObject);
        }
    }

    private void AlignBuildingContentRoot(Transform trackedMarkerTransform, sh_MarkerRouteData routeData)
    {
        Vector3 knownMarkerLocalPosition = routeData.KnownMarkerTransform.localPosition;
        Quaternion knownMarkerLocalRotation = routeData.KnownMarkerTransform.localRotation;

        Quaternion rootRotation = trackedMarkerTransform.rotation * Quaternion.Inverse(knownMarkerLocalRotation);
        Vector3 rootPosition = trackedMarkerTransform.position - (rootRotation * knownMarkerLocalPosition);

        buildingContentRoot.SetPositionAndRotation(rootPosition, rootRotation);
        hasAlignedBuildingRoot = true;

        Debug.Log($"[sh_MarkerRouteController] 첫 마커 정렬 완료: {routeData.MarkerName}", buildingContentRoot.gameObject);
    }

    public void SetActiveRoutes(int currentRouteOrder)
    {
        if (!isInitialized)
            return;

        if (currentRouteOrder < 0 || currentRouteOrder >= RequiredMarkerCount)
        {
            Debug.LogWarning($"[sh_MarkerRouteController] currentRouteOrder는 0부터 {RequiredMarkerCount - 1} 사이여야 합니다: {currentRouteOrder}", this);
            return;
        }

        int minRouteOrder = Mathf.Max(0, currentRouteOrder - (MaxVisibleRouteCount - 1));

        for (int index = 0; index < markerRoutes.Count; index++)
        {
            sh_MarkerRouteData routeData = markerRoutes[index];
            if (routeData.RuntimeInstance == null)
                continue;

            bool shouldBeActive = routeData.RouteOrder >= minRouteOrder && routeData.RouteOrder <= currentRouteOrder;
            routeData.RuntimeInstance.SetActive(shouldBeActive);
        }
    }

    [ContextMenu("Task04/Test Current Route Order")]
    private void TestEditorRouteOrder()
    {
        SetActiveRoutes(editorTestRouteOrder);
        Debug.Log($"[sh_MarkerRouteController] Editor 테스트 routeOrder={editorTestRouteOrder}", this);
    }

    public void RunEditorRouteOrderTest()
    {
        TestEditorRouteOrder();
    }

    private bool TryGetRouteData(string markerName, out sh_MarkerRouteData routeData)
    {
        for (int index = 0; index < markerRoutes.Count; index++)
        {
            if (markerRoutes[index].MarkerName == markerName)
            {
                routeData = markerRoutes[index];
                return true;
            }
        }

        routeData = null;
        return false;
    }
}
