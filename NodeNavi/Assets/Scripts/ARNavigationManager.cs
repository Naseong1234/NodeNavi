using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[DisallowMultipleComponent]
public sealed class ARNavigationManager : MonoBehaviour
{
    [Serializable]
    public sealed class MarkerData
    {
        [Tooltip("경로 순서입니다. 1부터 시작하며 중복되면 안 됩니다.")]
        [Min(1)]
        public int routeIndex = 1;

        [Tooltip("XR Reference Image Library에 등록된 이미지 이름과 정확히 같아야 합니다.")]
        public string referenceImageName;

        [Tooltip(
            "현실 마커와 대응하는 가상 마커 Transform입니다. " +
            "반드시 World Root 하위에 있어야 합니다.")]
        public Transform virtualMarker;

        [Tooltip(
            "이 단계에서 표시할 내비게이션 프리팹입니다. " +
            "프리팹은 앱 시작 시 미리 생성됩니다.")]
        public GameObject navigationPrefab;

        [Tooltip(
            "프리팹을 배치할 위치입니다. 반드시 World Root 하위에 있어야 합니다. " +
            "비어 있으면 Virtual Marker 위치를 사용합니다.")]
        public Transform contentSpawnPoint;

        [NonSerialized]
        public GameObject spawnedObject;

        // WorldRoot가 움직이기 전 저장하는 가상 마커의 기준 좌표
        [NonSerialized]
        public Vector3 markerLocalPosition;

        [NonSerialized]
        public Quaternion markerLocalRotation;

        // 내비게이션 콘텐츠의 기준 좌표
        [NonSerialized]
        public Vector3 contentLocalPosition;

        [NonSerialized]
        public Quaternion contentLocalRotation;
    }

    [Header("AR 설정")]
    [SerializeField]
    private ARTrackedImageManager trackedImageManager;

    [Tooltip(
        "모든 가상 마커와 내비게이션 오브젝트를 포함하는 루트입니다. " +
        "Scale은 반드시 (1, 1, 1)을 권장합니다.")]
    [SerializeField]
    private Transform worldRoot;

    [Header("마커 및 경로 데이터")]
    [SerializeField]
    private List<MarkerData> markerList = new();

    [Header("표시 범위")]
    [Tooltip("현재 단계 이전에 유지할 오브젝트 개수입니다. 2이면 현재 포함 최대 3개가 표시됩니다.")]
    [SerializeField, Min(0)]
    private int previousVisibleCount = 2;

    [Header("월드 정렬")]
    [Tooltip("켜면 WorldRoot가 목표 위치로 부드럽게 이동합니다.")]
    [SerializeField]
    private bool useSmoothing = true;

    [Tooltip("위치 보간 속도입니다.")]
    [SerializeField, Min(0.01f)]
    private float positionSmoothSpeed = 12f;

    [Tooltip("회전 보간 속도입니다.")]
    [SerializeField, Min(0.01f)]
    private float rotationSmoothSpeed = 12f;

    [Tooltip(
        "마커가 이미 정렬된 상태에서 이 거리보다 작게 움직이면 재정렬하지 않습니다. " +
        "미세한 추적 떨림을 줄이는 데 사용합니다.")]
    [SerializeField, Min(0f)]
    private float positionDeadZone = 0.005f;

    [Tooltip(
        "마커 회전 변화가 이 각도보다 작으면 재정렬하지 않습니다.")]
    [SerializeField, Min(0f)]
    private float rotationDeadZone = 0.5f;

    [Header("추적 정책")]
    [Tooltip(
        "현재 경로보다 이전 번호의 마커를 다시 봤을 때 경로 단계를 뒤로 돌릴지 설정합니다.")]
    [SerializeField]
    private bool allowBackwardProgress = true;

    [Tooltip(
        "Limited 상태에서도 마지막으로 관측된 마커 자세를 사용할지 설정합니다. " +
        "일반적으로 Tracking 상태만 사용하는 것이 안정적입니다.")]
    [SerializeField]
    private bool acceptLimitedTracking = false;

    private readonly Dictionary<string, MarkerData> markerByImageName =
        new(StringComparer.Ordinal);

    private readonly List<MarkerData> orderedMarkers = new();

    private MarkerData currentMarkerData;
    private int currentRouteIndex = -1;

    private Vector3 targetWorldPosition;
    private Quaternion targetWorldRotation;
    private bool hasAlignmentTarget;
    private bool initialized;

    private void Awake()
    {
        if (!ValidateRequiredReferences())
        {
            enabled = false;
            return;
        }

        BuildMarkerLookup();
        CacheAuthoredLocalPoses();
        PrewarmNavigationObjects();

        initialized = true;
    }

    private void OnEnable()
    {
        if (trackedImageManager != null)
        {
            // AR Foundation 6 권장 이벤트
            trackedImageManager.trackablesChanged.AddListener(OnTrackablesChanged);
        }
    }

    private void OnDisable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
        }
    }

    private void Update()
    {
        if (!initialized || !hasAlignmentTarget)
        {
            return;
        }

        if (!useSmoothing)
        {
            worldRoot.SetPositionAndRotation(
                targetWorldPosition,
                targetWorldRotation);

            hasAlignmentTarget = false;
            return;
        }

        // 프레임레이트와 무관한 지수 보간
        float positionT =
            1f - Mathf.Exp(-positionSmoothSpeed * Time.deltaTime);

        float rotationT =
            1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);

        Vector3 nextPosition = Vector3.Lerp(
            worldRoot.position,
            targetWorldPosition,
            positionT);

        Quaternion nextRotation = Quaternion.Slerp(
            worldRoot.rotation,
            targetWorldRotation,
            rotationT);

        worldRoot.SetPositionAndRotation(nextPosition, nextRotation);

        bool positionReached =
            Vector3.SqrMagnitude(worldRoot.position - targetWorldPosition)
            <= 0.000001f;

        bool rotationReached =
            Quaternion.Angle(worldRoot.rotation, targetWorldRotation)
            <= 0.05f;

        if (positionReached && rotationReached)
        {
            worldRoot.SetPositionAndRotation(
                targetWorldPosition,
                targetWorldRotation);

            hasAlignmentTarget = false;
        }
    }

    private void OnTrackablesChanged(
        ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            ProcessTrackedImage(trackedImage);
        }

        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            ProcessTrackedImage(trackedImage);
        }
    }

    private void ProcessTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage == null)
        {
            return;
        }

        bool validTrackingState =
            trackedImage.trackingState == TrackingState.Tracking ||
            (acceptLimitedTracking &&
             trackedImage.trackingState == TrackingState.Limited);

        if (!validTrackingState)
        {
            return;
        }

        string imageName = trackedImage.referenceImage.name;

        if (string.IsNullOrWhiteSpace(imageName))
        {
            return;
        }

        if (!markerByImageName.TryGetValue(imageName, out MarkerData markerData))
        {
            return;
        }

        bool markerChanged = currentMarkerData != markerData;

        if (markerChanged)
        {
            if (!allowBackwardProgress &&
                currentRouteIndex >= 0 &&
                markerData.routeIndex < currentRouteIndex)
            {
                return;
            }

            currentMarkerData = markerData;
            currentRouteIndex = markerData.routeIndex;

            UpdateObjectVisibility(markerData.routeIndex);
        }

        RequestWorldAlignment(
            trackedImage.transform.position,
            trackedImage.transform.rotation,
            markerData);
    }

    /// <summary>
    /// 현실 마커의 월드 자세와 가상 마커의 WorldRoot 기준 로컬 자세를 이용해
    /// WorldRoot의 목표 월드 자세를 계산합니다.
    /// </summary>
    private void RequestWorldAlignment(
        Vector3 physicalMarkerPosition,
        Quaternion physicalMarkerRotation,
        MarkerData markerData)
    {
        Quaternion calculatedRootRotation =
            physicalMarkerRotation *
            Quaternion.Inverse(markerData.markerLocalRotation);

        Vector3 calculatedRootPosition =
            physicalMarkerPosition -
            calculatedRootRotation * markerData.markerLocalPosition;

        if (hasAlignmentTarget)
        {
            float positionDifference = Vector3.Distance(
                targetWorldPosition,
                calculatedRootPosition);

            float rotationDifference = Quaternion.Angle(
                targetWorldRotation,
                calculatedRootRotation);

            if (positionDifference < positionDeadZone &&
                rotationDifference < rotationDeadZone)
            {
                return;
            }
        }

        targetWorldPosition = calculatedRootPosition;
        targetWorldRotation = calculatedRootRotation;
        hasAlignmentTarget = true;
    }

    private void UpdateObjectVisibility(int detectedRouteIndex)
    {
        int minimumVisibleIndex =
            Mathf.Max(1, detectedRouteIndex - previousVisibleCount);

        foreach (MarkerData marker in orderedMarkers)
        {
            bool shouldBeVisible =
                marker.routeIndex >= minimumVisibleIndex &&
                marker.routeIndex <= detectedRouteIndex;

            if (marker.spawnedObject != null &&
                marker.spawnedObject.activeSelf != shouldBeVisible)
            {
                marker.spawnedObject.SetActive(shouldBeVisible);
            }
        }
    }

    private void CacheAuthoredLocalPoses()
    {
        foreach (MarkerData marker in orderedMarkers)
        {
            marker.markerLocalPosition =
                worldRoot.InverseTransformPoint(
                    marker.virtualMarker.position);

            marker.markerLocalRotation =
                Quaternion.Inverse(worldRoot.rotation) *
                marker.virtualMarker.rotation;

            Transform spawnPoint =
                marker.contentSpawnPoint != null
                    ? marker.contentSpawnPoint
                    : marker.virtualMarker;

            marker.contentLocalPosition =
                worldRoot.InverseTransformPoint(spawnPoint.position);

            marker.contentLocalRotation =
                Quaternion.Inverse(worldRoot.rotation) *
                spawnPoint.rotation;
        }
    }

    private void PrewarmNavigationObjects()
    {
        foreach (MarkerData marker in orderedMarkers)
        {
            if (marker.navigationPrefab == null)
            {
                continue;
            }

            GameObject instance = Instantiate(
                marker.navigationPrefab,
                worldRoot);

            instance.name =
                $"{marker.navigationPrefab.name}_Route_{marker.routeIndex:00}";

            instance.transform.localPosition =
                marker.contentLocalPosition;

            instance.transform.localRotation =
                marker.contentLocalRotation;

            // 프리팹 자체의 스케일을 유지
            instance.SetActive(false);

            marker.spawnedObject = instance;
        }
    }

    private void BuildMarkerLookup()
    {
        markerByImageName.Clear();
        orderedMarkers.Clear();

        HashSet<int> usedRouteIndices = new();

        foreach (MarkerData marker in markerList)
        {
            if (marker == null)
            {
                continue;
            }

            if (marker.routeIndex < 1)
            {
                Debug.LogError(
                    "Route Index는 1 이상이어야 합니다.",
                    this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(marker.referenceImageName))
            {
                Debug.LogError(
                    $"Route {marker.routeIndex}: 이미지 이름이 비어 있습니다.",
                    this);
                continue;
            }

            if (marker.virtualMarker == null)
            {
                Debug.LogError(
                    $"Route {marker.routeIndex}: Virtual Marker가 없습니다.",
                    this);
                continue;
            }

            if (!marker.virtualMarker.IsChildOf(worldRoot))
            {
                Debug.LogError(
                    $"Route {marker.routeIndex}: Virtual Marker " +
                    $"'{marker.virtualMarker.name}'는 World Root 하위여야 합니다.",
                    marker.virtualMarker);
                continue;
            }

            if (marker.contentSpawnPoint != null &&
                !marker.contentSpawnPoint.IsChildOf(worldRoot))
            {
                Debug.LogError(
                    $"Route {marker.routeIndex}: Content Spawn Point " +
                    $"'{marker.contentSpawnPoint.name}'는 World Root 하위여야 합니다.",
                    marker.contentSpawnPoint);
                continue;
            }

            if (!usedRouteIndices.Add(marker.routeIndex))
            {
                Debug.LogError(
                    $"Route Index {marker.routeIndex}가 중복되었습니다.",
                    this);
                continue;
            }

            if (!markerByImageName.TryAdd(
                    marker.referenceImageName,
                    marker))
            {
                Debug.LogError(
                    $"Reference Image Name " +
                    $"'{marker.referenceImageName}'이 중복되었습니다.",
                    this);
                continue;
            }

            orderedMarkers.Add(marker);
        }

        orderedMarkers.Sort(
            (a, b) => a.routeIndex.CompareTo(b.routeIndex));

        if (orderedMarkers.Count == 0)
        {
            Debug.LogError(
                "사용 가능한 마커 데이터가 없습니다.",
                this);
        }
    }

    private bool ValidateRequiredReferences()
    {
        bool valid = true;

        if (trackedImageManager == null)
        {
            Debug.LogError(
                "ARTrackedImageManager가 할당되지 않았습니다.",
                this);
            valid = false;
        }

        if (worldRoot == null)
        {
            Debug.LogError(
                "World Root가 할당되지 않았습니다.",
                this);
            valid = false;
        }

        if (markerList == null || markerList.Count == 0)
        {
            Debug.LogError(
                "Marker List가 비어 있습니다.",
                this);
            valid = false;
        }

        if (worldRoot != null)
        {
            Vector3 scale = worldRoot.lossyScale;

            if (!ApproximatelyOne(scale.x) ||
                !ApproximatelyOne(scale.y) ||
                !ApproximatelyOne(scale.z))
            {
                Debug.LogWarning(
                    "World Root의 월드 스케일이 (1, 1, 1)이 아닙니다. " +
                    "마커 정렬 오차를 방지하려면 World Root와 상위 부모의 " +
                    "스케일을 모두 1로 설정하는 것을 권장합니다.",
                    worldRoot);
            }
        }

        return valid;
    }

    private static bool ApproximatelyOne(float value)
    {
        return Mathf.Abs(value - 1f) <= 0.001f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        previousVisibleCount = Mathf.Max(0, previousVisibleCount);
        positionSmoothSpeed = Mathf.Max(0.01f, positionSmoothSpeed);
        rotationSmoothSpeed = Mathf.Max(0.01f, rotationSmoothSpeed);
        positionDeadZone = Mathf.Max(0f, positionDeadZone);
        rotationDeadZone = Mathf.Max(0f, rotationDeadZone);
    }
#endif
}