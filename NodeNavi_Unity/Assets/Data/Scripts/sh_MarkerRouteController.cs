using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class sh_MarkerRouteController : MonoBehaviour
{
    private const int RequiredMarkerCount = 6;

    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private Transform routeContentRoot;
    [SerializeField] private List<sh_MarkerRouteData> markerRoutes = new List<sh_MarkerRouteData>(RequiredMarkerCount);

    private bool isSubscribed;
    private bool isInitialized;

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
        LogTrackedImages(eventArgs.added, "added");
        LogTrackedImages(eventArgs.updated, "updated");
    }

    private void LogTrackedImages(IReadOnlyList<ARTrackedImage> trackedImages, string source)
    {
        for (int index = 0; index < trackedImages.Count; index++)
        {
            ARTrackedImage trackedImage = trackedImages[index];
            if (trackedImage == null || trackedImage.trackingState != TrackingState.Tracking)
                continue;

            Debug.Log($"[sh_MarkerRouteController] trackedImagesChanged {source}: {trackedImage.referenceImage.name}", trackedImage.gameObject);
        }
    }
}
