using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class sh_MarkerRouteController : MonoBehaviour
{
    private const int RequiredMarkerCount = 6;
    private const int MaxVisibleRouteCount = 3;

    [Header("참조 설정")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private Transform buildingContentRoot;
    [SerializeField] private Transform routeContentRoot;
    [SerializeField] private List<sh_MarkerRouteData> markerRoutes = new List<sh_MarkerRouteData>(RequiredMarkerCount);

    [Header("마커 부착 방식 (벽면 수직 부착 보정)")]
    [Tooltip("마커가 벽(수직면)에 부착된 경우 체크합니다. 체크 시 건물이 벽에 세워지지 않고 바닥(땅 위)에 정상적으로 생성됩니다.")]
    [SerializeField] private bool isWallMountedMarker = true;
    [Tooltip("벽면 마커 좌표계를 표준 Unity 좌표계로 변환하는 오프셋 각도입니다.")]
    [SerializeField] private Vector3 wallMarkerOffsetEuler = new Vector3(90f, 0f, 0f);

    [Header("보정 및 정렬 설정")]
    [Tooltip("재인식 시 위치/회전을 부드럽게 보간할지 여부입니다. 끄면 즉시 위치가 맞추어집니다.")]
    [SerializeField] private bool useSmoothInterpolation = false;
    [SerializeField] private float interpolationSpeed = 10f;
    [SerializeField] private float minPositionThreshold = 0.005f; // 5mm
    [SerializeField] private float minRotationThreshold = 0.5f;   // 0.5도

    [Header("에디터 테스트")]
    [SerializeField] private int editorTestRouteOrder;

    private bool isSubscribed;
    private bool isInitialized;
    private bool hasAlignedBuildingRoot;
    private Coroutine alignmentCoroutine;

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

            // BuildingContentRoot 기준의 상대 로컬 Pose를 캐싱
            routeData.CacheLocalPose(buildingContentRoot);

            // 경로 Prefab 인스턴스 생성 및 비활성화 풀링
            if (routeData.RoutePrefab != null)
            {
                GameObject instance = Instantiate(routeData.RoutePrefab, routeContentRoot);
                instance.name = $"{routeData.RoutePrefab.name}_Runtime";
                instance.SetActive(false);
                routeData.RuntimeInstance = instance;
            }
        }

        isInitialized = true;
        Debug.Log($"[sh_MarkerRouteController] 마커 {markerRoutes.Count}개 로컬 좌표 캐싱 완료 및 경로 prefab 풀링 준비 완료.", this);
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
        if (isSubscribed || trackedImageManager == null)
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

            // 마커 인식 시 BuildingContentRoot 좌표계 정렬
            AlignBuildingContentRoot(trackedImage.transform, routeData);

            // 해당 구간 선로 표시 활성화 (현재 + 이전 2개)
            SetActiveRoutes(routeData.RouteOrder);
            Debug.Log($"[sh_MarkerRouteController] 마커 감지 ({source}): {markerName}, routeOrder={routeData.RouteOrder}", trackedImage.gameObject);
        }
    }

    /// <summary>
    /// 실제 마커의 월드 Pose와 건물 좌표계 내 가상 마커 Pose를 역산하여 BuildingContentRoot를 정렬합니다.
    /// 벽면에 부착된 마커인 경우 수직-수평 축 오프셋을 자동 보정합니다.
    /// </summary>
    private void AlignBuildingContentRoot(Transform trackedMarkerTransform, sh_MarkerRouteData routeData)
    {
        if (trackedMarkerTransform == null || routeData == null || buildingContentRoot == null)
            return;

        // 캐싱되지 않은 경우 즉시 캐싱
        if (!routeData.IsLocalPoseCached)
            routeData.CacheLocalPose(buildingContentRoot);

        Vector3 knownMarkerLocalPosition = routeData.CachedKnownLocalPosition;
        Quaternion knownMarkerLocalRotation = routeData.CachedKnownLocalRotation;

        // 벽면 마커인 경우: AR Foundation의 이미지 로컬 축(Y=법선, Z=상단)을 Unity 표준 축(Y=상단, Z=법선)으로 90도 회전 보정
        Quaternion trackedRotation = trackedMarkerTransform.rotation;
        if (isWallMountedMarker)
        {
            trackedRotation = trackedRotation * Quaternion.Euler(wallMarkerOffsetEuler);
        }

        // 역산 수식:
        // rootRotation = trackedRotation * inverse(knownMarkerLocalRotation)
        // rootPosition = trackedMarkerPosition - (rootRotation * knownMarkerLocalPosition)
        Quaternion targetRootRotation = trackedRotation * Quaternion.Inverse(knownMarkerLocalRotation);
        Vector3 targetRootPosition = trackedMarkerTransform.position - (targetRootRotation * knownMarkerLocalPosition);

        if (!hasAlignedBuildingRoot)
        {
            // 첫 마커 인식 시에는 즉시 정렬
            buildingContentRoot.SetPositionAndRotation(targetRootPosition, targetRootRotation);
            hasAlignedBuildingRoot = true;
            Debug.Log($"[sh_MarkerRouteController] 첫 마커 기반 좌표계 정렬 완료: {routeData.MarkerName}", buildingContentRoot.gameObject);
        }
        else
        {
            // 재인식 시: 드리프트 보정
            float posDiff = Vector3.Distance(buildingContentRoot.position, targetRootPosition);
            float rotDiff = Quaternion.Angle(buildingContentRoot.rotation, targetRootRotation);

            if (posDiff < minPositionThreshold && rotDiff < minRotationThreshold)
                return; // 미세 오차는 무시하여 화면 떨림 방지

            if (useSmoothInterpolation)
            {
                if (alignmentCoroutine != null)
                    StopCoroutine(alignmentCoroutine);

                alignmentCoroutine = StartCoroutine(SmoothAlignRoutine(targetRootPosition, targetRootRotation));
            }
            else
            {
                buildingContentRoot.SetPositionAndRotation(targetRootPosition, targetRootRotation);
            }

            Debug.Log($"[sh_MarkerRouteController] 마커 재정렬 완료: {routeData.MarkerName} (이동 거리: {posDiff:F3}m, 회전 각도: {rotDiff:F1}°)", buildingContentRoot.gameObject);
        }
    }

    private IEnumerator SmoothAlignRoutine(Vector3 targetPosition, Quaternion targetRotation)
    {
        while (Vector3.Distance(buildingContentRoot.position, targetPosition) > 0.001f ||
               Quaternion.Angle(buildingContentRoot.rotation, targetRotation) > 0.1f)
        {
            buildingContentRoot.position = Vector3.Lerp(buildingContentRoot.position, targetPosition, Time.deltaTime * interpolationSpeed);
            buildingContentRoot.rotation = Quaternion.Slerp(buildingContentRoot.rotation, targetRotation, Time.deltaTime * interpolationSpeed);
            yield return null;
        }

        buildingContentRoot.SetPositionAndRotation(targetPosition, targetRotation);
        alignmentCoroutine = null;
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

    [ContextMenu("Test/Run Editor Route & Alignment Test")]
    public void RunEditorRouteOrderTest()
    {
        if (!isInitialized)
            InitializeRoutePool();

        if (editorTestRouteOrder < 0 || editorTestRouteOrder >= markerRoutes.Count)
        {
            Debug.LogWarning($"[sh_MarkerRouteController] 유효하지 않은 editorTestRouteOrder입니다: {editorTestRouteOrder}", this);
            return;
        }

        sh_MarkerRouteData targetRoute = markerRoutes.Find(r => r.RouteOrder == editorTestRouteOrder);
        if (targetRoute != null && targetRoute.KnownMarkerTransform != null)
        {
            // 에디터 테스트 시 가상 카메라 위치(월드 원점 앞 1.5m 높이)에 해당 마커가 오도록 가상 정렬 시뮬레이션
            GameObject dummyTrackedImage = new GameObject("Temp_Editor_TrackedMarker");
            dummyTrackedImage.transform.position = new Vector3(0f, 1.5f, 1.0f);
            dummyTrackedImage.transform.rotation = Quaternion.identity;

            AlignBuildingContentRoot(dummyTrackedImage.transform, targetRoute);
            DestroyImmediate(dummyTrackedImage);
        }

        SetActiveRoutes(editorTestRouteOrder);
        Debug.Log($"[sh_MarkerRouteController] [Editor 테스트] RouteOrder={editorTestRouteOrder} 선로 활성화 및 가상 정렬 완료", this);
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
