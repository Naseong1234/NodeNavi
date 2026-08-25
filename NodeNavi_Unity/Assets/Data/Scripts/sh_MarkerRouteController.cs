using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    [SerializeField] private int requestedMaxMovingImages = RequiredMarkerCount;

    [Header("1번 마커 선택 UI")]
    [SerializeField] private string selectionMarkerName = "Marker_01";
    [SerializeField] private GameObject pcSelectionPanel;
    [SerializeField] private TMP_Text selectionStateText;
    [SerializeField] private string noSelectionLabel = "선택 안 됨";
    [SerializeField] private string pc1SelectionLabel = "1번 PC 선택";
    [SerializeField] private string pc2SelectionLabel = "2번 PC 선택";

    [Header("마커 부착 방식 (벽면 수직 부착 보정)")]
    [Tooltip("마커가 벽(수직면)에 부착된 경우 체크합니다. 체크 시 건물이 벽에 세워지지 않고 바닥(땅 위)에 정상적으로 생성됩니다.")]
    [SerializeField] private bool isWallMountedMarker = true;
    [Tooltip("벽면 마커 좌표계를 표준 Unity 좌표계로 변환하는 오프셋 각도입니다.")]
    [SerializeField] private Vector3 wallMarkerOffsetEuler = new Vector3(90f, 0f, 0f);

    [Header("보정 및 정렬 설정")]
    [Tooltip("재인식 시 위치/회전을 부드럽게 보간할지 여부입니다. 일반 오차에서는 켜 두는 것을 권장합니다.")]
    [SerializeField] private bool useSmoothInterpolation = true;
    [SerializeField] private float interpolationSpeed = 10f;
    [SerializeField] private float poseStableDuration = 0.3f;
    [SerializeField] private float minPositionThreshold = 0.05f;
    [SerializeField] private float minRotationThreshold = 3f;
    [SerializeField] private float largePositionThreshold = 0.35f;
    [SerializeField] private float largeRotationThreshold = 25f;
    [Tooltip("일반 재정렬은 이 시간 간격 이상 지났을 때만 실행합니다.")]
    [SerializeField] private float alignmentCooldown = 1f;
    [Tooltip("큰 오차가 감지되면 재정렬 쿨타임을 기다리지 않고 즉시 보정합니다.")]
    [SerializeField] private bool realignLargeOffsetImmediately = true;
    [Tooltip("If enabled, the building/root transform is fixed after the first tracked marker alignment.")]
    [SerializeField] private bool lockAlignmentAfterFirstRecognition = true;
    [Tooltip("If enabled, BuildingContentRoot position and rotation are fixed when marker 1 is first recognized.")]
    [SerializeField] private bool lockBuildingPoseAfterSelectionMarkerRecognition = true;
    [SerializeField] private GameObject reAlignmentIndicator;
    [SerializeField] private float indicatorVisibleDuration = 0.75f;

    [Header("상태 안내 UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string waitingMessage = "마커를 비춰 주세요";
    [SerializeField] private string successMessage = "경로를 표시했습니다";
    [SerializeField] private string reAligningMessage = "위치를 재정렬하고 있습니다";
    [SerializeField] private string selectionPromptMessage = "확인할 PC를 선택해 주세요";
    [SerializeField] private string selectionCompletedMessage = "선택한 PC의 안내 경로를 따라가세요.";
    [SerializeField] private string selectionRequiredMessage = "먼저 1번 마커에서 PC를 선택해 주세요";
    [SerializeField] private int destinationRouteOrder = 2;
    [SerializeField] private string destinationReachedMessage = "목적지에 도착 하였습니다. 장비옆 안내 창을 확인하세요.";

    private bool isSubscribed;
    private bool isInitialized;
    private bool hasAlignedBuildingRoot;
    private bool hasRecognizedAnyMarker;
    private bool hasConfirmedPathSelection;
    private bool hasReachedDestination;
    private bool isAlignmentLocked;
    private bool hasLockedBuildingPose;
    private Coroutine alignmentCoroutine;
    private string currentReferenceMarkerName;
    private Vector3 lockedBuildingPosition;
    private Quaternion lockedBuildingRotation;
    private float indicatorHideAtTime;
    private float nextAlignmentAllowedTime;
    private sh_PCPathOption currentPathOption = sh_PCPathOption.None;
    private int currentVisibleRouteOrder = -1;

    public sh_PCPathOption CurrentPathOption => currentPathOption;
    public bool HasConfirmedPathSelection => hasConfirmedPathSelection;

    private void Awake()
    {
        ConfigureTrackedImageManager();
        InitializeRoutePool();
        HidePCSelectionPanel();
        RefreshSelectionStateText();
        SetStatusMessage(waitingMessage);
    }

    private void OnEnable()
    {
        ConfigureTrackedImageManager();
        SubscribeTrackedImageEvents();
    }

    private void OnDisable()
    {
        UnsubscribeTrackedImageEvents();
    }

    private void Update()
    {
        if (hasLockedBuildingPose && buildingContentRoot != null)
        {
            if (Vector3.Distance(buildingContentRoot.position, lockedBuildingPosition) > 0.001f ||
                Quaternion.Angle(buildingContentRoot.rotation, lockedBuildingRotation) > 0.01f)
            {
                buildingContentRoot.SetPositionAndRotation(lockedBuildingPosition, lockedBuildingRotation);
            }
        }

        if (reAlignmentIndicator != null &&
            reAlignmentIndicator.activeSelf &&
            Time.time >= indicatorHideAtTime)
        {
            reAlignmentIndicator.SetActive(false);
            SetStatusMessage(GetCurrentSuccessMessage());
        }
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
            routeData.CacheLocalPose(buildingContentRoot);
            CreateRuntimeInstance(routeData, sh_PCPathOption.None);
            CreateRuntimeInstance(routeData, sh_PCPathOption.PC1);
            CreateRuntimeInstance(routeData, sh_PCPathOption.PC2);
        }

        isInitialized = true;
        Debug.Log($"[sh_MarkerRouteController] 마커 {markerRoutes.Count}개 로컬 좌표 캐싱 완료 및 PC별 경로 prefab 풀링 준비 완료.", this);
    }

    private void CreateRuntimeInstance(sh_MarkerRouteData routeData, sh_PCPathOption pathOption)
    {
        GameObject prefab = null;
        switch (pathOption)
        {
            case sh_PCPathOption.PC1:
                prefab = routeData.PC1RoutePrefab;
                break;
            case sh_PCPathOption.PC2:
                prefab = routeData.PC2RoutePrefab;
                break;
            default:
                prefab = routeData.BothPrefab;
                break;
        }

        if (prefab == null)
            return;

        if (routeData.GetRuntimeInstance(pathOption) != null)
            return;

        GameObject instance = Instantiate(prefab, routeContentRoot);
        instance.name = $"{prefab.name}_{pathOption}_Runtime";
        instance.SetActive(false);
        routeData.SetRuntimeInstance(pathOption, instance);
    }

    private bool ValidateConfiguration()
    {
        if (trackedImageManager == null)
        {
            Debug.LogError("[sh_MarkerRouteController] 'Tracked Image Manager' 필드가 비어 있습니다. ARScene의 MarkerRouteController 오브젝트에서 XR Origin의 AR Tracked Image Manager를 연결하세요.", this);
            return false;
        }

        if (buildingContentRoot == null)
        {
            Debug.LogError("[sh_MarkerRouteController] 'Building Content Root' 필드가 비어 있습니다. ARScene의 MarkerRouteController 오브젝트에서 BuildingContentRoot를 연결하세요.", this);
            return false;
        }

        if (routeContentRoot == null)
        {
            Debug.LogError("[sh_MarkerRouteController] 'Route Content Root' 필드가 비어 있습니다. ARScene의 MarkerRouteController 오브젝트에서 RouteContentRoot를 연결하세요.", this);
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

            if (routeData.RouteOrder == 0)
            {
                if (!routeData.HasBothPrefab && !routeData.HasPCSpecificRoutes)
                {
                    Debug.LogError($"[sh_MarkerRouteController] {routeData.MarkerName}은 1번 마커이므로 Both Prefab 또는 PC별 prefab 중 하나 이상이 필요합니다.", this);
                    return false;
                }

                continue;
            }

            if (!routeData.HasBothPrefab)
            {
                Debug.LogWarning($"[sh_MarkerRouteController] {routeData.MarkerName}의 Both Prefab이 비어 있습니다. 공통 경로는 표시되지 않고 PC별 prefab만 표시됩니다.", this);
            }

            if (routeData.PC1RoutePrefab == null || routeData.PC2RoutePrefab == null)
            {
                Debug.LogWarning($"[sh_MarkerRouteController] {routeData.MarkerName}의 PC1/PC2용 prefab이 일부 비어 있습니다. 해당 PC 전용 경로는 표시되지 않습니다.", this);
            }
        }

        return true;
    }

    private void ConfigureTrackedImageManager()
    {
        if (trackedImageManager == null)
            return;

        int targetCount = Mathf.Clamp(requestedMaxMovingImages, 1, RequiredMarkerCount);
        if (trackedImageManager.requestedMaxNumberOfMovingImages == targetCount)
            return;

        trackedImageManager.requestedMaxNumberOfMovingImages = targetCount;
        Debug.Log($"[sh_MarkerRouteController] AR Tracked Image Manager 최대 동시 추적 수를 {targetCount}로 설정했습니다.", this);
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
        if (TrySelectTrackedImage(eventArgs.added, true, out ARTrackedImage trackedImage) ||
            TrySelectTrackedImage(eventArgs.updated, true, out trackedImage) ||
            TrySelectTrackedImage(eventArgs.updated, false, out trackedImage) ||
            TrySelectTrackedImage(eventArgs.added, false, out trackedImage))
        {
            HandleTrackedImage(trackedImage);
            return;
        }

        SetStatusMessage(GetDefaultStatusMessage());
    }

    private bool TrySelectTrackedImage(
        IReadOnlyList<ARTrackedImage> trackedImages,
        bool preferDifferentMarker,
        out ARTrackedImage selectedTrackedImage)
    {
        selectedTrackedImage = null;

        if (trackedImages == null)
            return false;

        for (int index = 0; index < trackedImages.Count; index++)
        {
            ARTrackedImage trackedImage = trackedImages[index];
            if (trackedImage == null || trackedImage.trackingState != TrackingState.Tracking)
                continue;

            bool isCurrentMarker = trackedImage.referenceImage.name == currentReferenceMarkerName;
            if (preferDifferentMarker)
            {
                if (!isCurrentMarker)
                {
                    selectedTrackedImage = trackedImage;
                    return true;
                }
            }
            else if (isCurrentMarker)
            {
                selectedTrackedImage = trackedImage;
                return true;
            }
        }

        return false;
    }

    private void HandleTrackedImage(ARTrackedImage trackedImage)
    {
        string markerName = trackedImage.referenceImage.name;
        if (!TryGetRouteData(markerName, out sh_MarkerRouteData routeData))
        {
            Debug.LogWarning($"[sh_MarkerRouteController] 마커 데이터와 일치하지 않는 이미지 이름입니다: {markerName}", trackedImage.gameObject);
            return;
        }

        hasRecognizedAnyMarker = true;

        if (!hasAlignedBuildingRoot)
        {
            if (markerName != selectionMarkerName)
            {
                SetStatusMessage(selectionRequiredMessage);
                return;
            }

            bool didAlign = AlignBuildingContentRoot(trackedImage.transform, routeData);
            currentReferenceMarkerName = markerName;
            isAlignmentLocked = lockAlignmentAfterFirstRecognition && didAlign;
            if (didAlign && lockBuildingPoseAfterSelectionMarkerRecognition)
                LockBuildingPoseAtSelectionMarkerRecognition();
            UpdateRouteAndUIForMarker(routeData);
            return;
        }

        if (isAlignmentLocked)
        {
            if (markerName != currentReferenceMarkerName)
                currentReferenceMarkerName = markerName;

            UpdateRouteAndUIForMarker(routeData);
            return;
        }

        if (markerName == currentReferenceMarkerName)
        {
            AlignBuildingContentRoot(trackedImage.transform, routeData);
            UpdateRouteAndUIForMarker(routeData);

            return;
        }

        AlignBuildingContentRoot(trackedImage.transform, routeData);
        currentReferenceMarkerName = markerName;
        UpdateRouteAndUIForMarker(routeData);

        if (markerName != selectionMarkerName && routeData.RouteOrder > 0)
            SetStatusMessage(GetDefaultStatusMessage());
    }

    private void UpdateRouteAndUIForMarker(sh_MarkerRouteData routeData)
    {
        if (routeData == null)
            return;

        currentVisibleRouteOrder = routeData.RouteOrder;
        bool isSelectionMarker = routeData.MarkerName == selectionMarkerName || routeData.RouteOrder == 0;
        if (routeData.RouteOrder == destinationRouteOrder)
        {
            if (hasConfirmedPathSelection)
            {
                HidePCSelectionPanel();
                SetActiveRoutes(routeData.RouteOrder);
            }
            else
            {
                HideAllRoutes();
            }

            hasReachedDestination = true;
            SetStatusMessage(destinationReachedMessage);
            return;
        }

        if (isSelectionMarker)
        {
            ShowPCSelectionPanel();
            if (currentPathOption == sh_PCPathOption.None)
                HideAllRoutes();
            else
                ApplyLiveSelectionPreview(routeData.RouteOrder);
            UpdateStatusForMarker(routeData);
            return;
        }

        if (!hasConfirmedPathSelection)
        {
            HideAllRoutes();
            SetStatusMessage(GetDefaultStatusMessage());
            return;
        }

        HidePCSelectionPanel();
        SetActiveRoutes(routeData.RouteOrder);
        SetStatusMessage(GetStatusMessageForRoute(routeData.RouteOrder));
    }

    private void UpdateStatusForMarker(sh_MarkerRouteData routeData)
    {
        if (routeData == null)
            return;

        bool isSelectionMarker = routeData.MarkerName == selectionMarkerName || routeData.RouteOrder == 0;
        if (routeData.RouteOrder == destinationRouteOrder)
        {
            hasReachedDestination = true;
            SetStatusMessage(destinationReachedMessage);
            return;
        }

        if (isSelectionMarker && !hasConfirmedPathSelection)
        {
            SetStatusMessage(selectionPromptMessage);
            return;
        }

        if (!hasConfirmedPathSelection && routeData.RouteOrder > 0)
        {
            SetStatusMessage(GetDefaultStatusMessage());
            return;
        }

        SetStatusMessage(GetCurrentSuccessMessage());
    }

    public void SelectPC1()
    {
        SetCurrentPathOption(sh_PCPathOption.PC1);
    }

    public void SelectPC2()
    {
        SetCurrentPathOption(sh_PCPathOption.PC2);
    }

    public void ConfirmPCSelection()
    {
        if (currentPathOption == sh_PCPathOption.None)
        {
            SetStatusMessage(selectionPromptMessage);
            return;
        }

        hasConfirmedPathSelection = true;
        HidePCSelectionPanel();

        if (currentVisibleRouteOrder >= 0)
            SetActiveRoutes(currentVisibleRouteOrder);

        SetStatusMessage(GetStatusMessageForRoute(currentVisibleRouteOrder));
    }

    public void ResetPCSelection()
    {
        hasConfirmedPathSelection = false;
        hasReachedDestination = false;
        currentPathOption = sh_PCPathOption.None;
        currentVisibleRouteOrder = -1;
        RefreshSelectionStateText();
        HideAllRoutes();
        SetStatusMessage(GetDefaultStatusMessage());
    }

    private void SetCurrentPathOption(sh_PCPathOption pathOption)
    {
        currentPathOption = pathOption;
        hasConfirmedPathSelection = false;
        RefreshSelectionStateText();
        SetStatusMessage(selectionPromptMessage);
        UpdateLiveRoutePreview();
    }

    private void LockBuildingPoseAtSelectionMarkerRecognition()
    {
        if (buildingContentRoot == null)
            return;

        if (alignmentCoroutine != null)
        {
            StopCoroutine(alignmentCoroutine);
            alignmentCoroutine = null;
        }

        lockedBuildingPosition = buildingContentRoot.position;
        lockedBuildingRotation = buildingContentRoot.rotation;
        hasLockedBuildingPose = true;
        isAlignmentLocked = true;
        buildingContentRoot.SetPositionAndRotation(lockedBuildingPosition, lockedBuildingRotation);

        Debug.Log("[sh_MarkerRouteController] Marker 1 fixed BuildingContentRoot position and rotation.", buildingContentRoot.gameObject);
    }

    private void UpdateLiveRoutePreview()
    {
        if (!isInitialized)
            return;

        if (currentVisibleRouteOrder < 0)
            return;

        ApplyLiveSelectionPreview(currentVisibleRouteOrder);
    }

    private void ApplyLiveSelectionPreview(int routeOrder)
    {
        if (!isInitialized)
            return;

        if (routeOrder < 0 || routeOrder >= RequiredMarkerCount)
            return;

        if (currentPathOption == sh_PCPathOption.None)
        {
            HideAllRoutes();
            return;
        }

        for (int index = 0; index < markerRoutes.Count; index++)
        {
            sh_MarkerRouteData routeData = markerRoutes[index];
            routeData.DisableAllRuntimeInstances();

            if (routeData.RouteOrder != routeOrder)
                continue;

            if (routeOrder == 0 && !hasConfirmedPathSelection)
                SetSelectedPCRouteOnly(routeData);
            else
                SetRouteInstancesActive(routeData, currentPathOption != sh_PCPathOption.None);
        }
    }

    private string GetCurrentSuccessMessage()
    {
        if (!hasConfirmedPathSelection)
            return GetDefaultStatusMessage();

        return GetStatusMessageForRoute(currentVisibleRouteOrder);
    }

    private string GetDefaultStatusMessage()
    {
        if (hasReachedDestination)
            return destinationReachedMessage;

        if (!hasRecognizedAnyMarker)
            return waitingMessage;

        if (hasConfirmedPathSelection)
            return selectionCompletedMessage;

        if (currentPathOption != sh_PCPathOption.None)
            return selectionPromptMessage;

        if (pcSelectionPanel != null && pcSelectionPanel.activeSelf)
            return selectionPromptMessage;

        return selectionPromptMessage;
    }

    private string GetStatusMessageForRoute(int routeOrder)
    {
        if (routeOrder == destinationRouteOrder)
        {
            hasReachedDestination = true;
            return destinationReachedMessage;
        }

        return selectionCompletedMessage;
    }

    private void ShowPCSelectionPanel()
    {
        if (pcSelectionPanel != null)
            pcSelectionPanel.SetActive(true);

        RefreshSelectionStateText();
    }

    private void HidePCSelectionPanel()
    {
        if (pcSelectionPanel != null)
            pcSelectionPanel.SetActive(false);
    }

    /// <summary>
    /// 실제 마커의 월드 Pose와 건물 좌표계 내 가상 마커 Pose를 역산하여 BuildingContentRoot를 정렬합니다.
    /// 벽면에 부착된 마커인 경우 수직-수평 축 오프셋을 자동 보정합니다.
    /// </summary>
    private bool AlignBuildingContentRoot(Transform trackedMarkerTransform, sh_MarkerRouteData routeData)
    {
        if (trackedMarkerTransform == null || routeData == null || buildingContentRoot == null)
            return false;

        if (!routeData.IsLocalPoseCached)
            routeData.CacheLocalPose(buildingContentRoot);

        Vector3 knownMarkerLocalPosition = routeData.CachedKnownLocalPosition;
        Quaternion knownMarkerLocalRotation = routeData.CachedKnownLocalRotation;

        Quaternion trackedRotation = trackedMarkerTransform.rotation;
        if (isWallMountedMarker)
            trackedRotation = trackedRotation * Quaternion.Euler(wallMarkerOffsetEuler);

        Quaternion horizontalTrackedRotation = GetHorizontalRotation(trackedRotation);
        Quaternion horizontalKnownRotation = GetHorizontalRotation(knownMarkerLocalRotation);

        Quaternion targetRootRotation = horizontalTrackedRotation * Quaternion.Inverse(horizontalKnownRotation);
        Vector3 targetRootPosition = trackedMarkerTransform.position - (targetRootRotation * knownMarkerLocalPosition);

        if (!hasAlignedBuildingRoot)
        {
            buildingContentRoot.SetPositionAndRotation(targetRootPosition, targetRootRotation);
            hasAlignedBuildingRoot = true;
            nextAlignmentAllowedTime = Time.time + alignmentCooldown;
            Debug.Log($"[sh_MarkerRouteController] 첫 마커 기반 좌표계 정렬 완료: {routeData.MarkerName}", buildingContentRoot.gameObject);
            return true;
        }

        float posDiff = Vector3.Distance(buildingContentRoot.position, targetRootPosition);
        float rotDiff = Quaternion.Angle(buildingContentRoot.rotation, targetRootRotation);

        if (posDiff < minPositionThreshold && rotDiff < minRotationThreshold)
            return false;

        if (posDiff >= largePositionThreshold || rotDiff >= largeRotationThreshold)
        {
            if (!realignLargeOffsetImmediately && Time.time < nextAlignmentAllowedTime)
                return false;

            if (alignmentCoroutine != null)
            {
                StopCoroutine(alignmentCoroutine);
                alignmentCoroutine = null;
            }

            buildingContentRoot.SetPositionAndRotation(targetRootPosition, targetRootRotation);
            nextAlignmentAllowedTime = Time.time + alignmentCooldown;
            ShowReAlignmentIndicator();
            Debug.Log($"[sh_MarkerRouteController] 큰 오차 재정렬 완료: {routeData.MarkerName} (이동 거리: {posDiff:F3}m, 회전 각도: {rotDiff:F1}°)", buildingContentRoot.gameObject);
            return true;
        }

        if (Time.time < nextAlignmentAllowedTime)
            return false;

        nextAlignmentAllowedTime = Time.time + alignmentCooldown;

        if (alignmentCoroutine != null)
            StopCoroutine(alignmentCoroutine);

        if (useSmoothInterpolation)
            alignmentCoroutine = StartCoroutine(SmoothAlignRoutine(targetRootPosition, targetRootRotation));
        else
            buildingContentRoot.SetPositionAndRotation(targetRootPosition, targetRootRotation);

        Debug.Log($"[sh_MarkerRouteController] 마커 재정렬 완료: {routeData.MarkerName} (이동 거리: {posDiff:F3}m, 회전 각도: {rotDiff:F1}°)", buildingContentRoot.gameObject);
        return true;
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

    private void ShowReAlignmentIndicator()
    {
        if (reAlignmentIndicator == null)
        {
            SetStatusMessage(reAligningMessage);
            return;
        }

        reAlignmentIndicator.SetActive(true);
        indicatorHideAtTime = Time.time + indicatorVisibleDuration;
        SetStatusMessage(reAligningMessage);
    }

    private Quaternion GetHorizontalRotation(Quaternion sourceRotation)
    {
        Vector3 forwardOnPlane = Vector3.ProjectOnPlane(sourceRotation * Vector3.forward, Vector3.up);
        if (forwardOnPlane.sqrMagnitude < 0.0001f)
            forwardOnPlane = Vector3.ProjectOnPlane(sourceRotation * Vector3.right, Vector3.up);

        if (forwardOnPlane.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(forwardOnPlane.normalized, Vector3.up);
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
            routeData.DisableAllRuntimeInstances();

            bool shouldBeActive = routeData.RouteOrder >= minRouteOrder && routeData.RouteOrder <= currentRouteOrder;
            if (!shouldBeActive)
                continue;

            SetRouteInstancesActive(routeData, hasConfirmedPathSelection);
        }
    }

    private void SetRouteInstancesActive(sh_MarkerRouteData routeData, bool includeSelectedPC)
    {
        if (routeData == null)
            return;

        // 목적지 마커를 포함해 어떤 경로가 먼저 인식되더라도 필요한 인스턴스를 보장합니다.
        GameObject bothInstance = GetOrCreateRuntimeInstance(routeData, sh_PCPathOption.None);
        if (bothInstance != null)
            bothInstance.SetActive(true);

        if (!includeSelectedPC || currentPathOption == sh_PCPathOption.None)
            return;

        GameObject selectedInstance = GetOrCreateRuntimeInstance(routeData, currentPathOption);
        if (selectedInstance != null)
            selectedInstance.SetActive(true);
    }

    private GameObject GetOrCreateRuntimeInstance(sh_MarkerRouteData routeData, sh_PCPathOption pathOption)
    {
        GameObject runtimeInstance = routeData.GetRuntimeInstance(pathOption);
        if (runtimeInstance != null)
            return runtimeInstance;

        if (routeData.GetPrefabForOption(pathOption) == null)
            return null;

        CreateRuntimeInstance(routeData, pathOption);
        return routeData.GetRuntimeInstance(pathOption);
    }

    private void SetSelectedPCRouteOnly(sh_MarkerRouteData routeData)
    {
        if (routeData == null || currentPathOption == sh_PCPathOption.None)
            return;

        GameObject selectedInstance = routeData.GetRuntimeInstance(currentPathOption);
        if (selectedInstance != null)
            selectedInstance.SetActive(true);
    }

    private void HideAllRoutes()
    {
        for (int index = 0; index < markerRoutes.Count; index++)
            markerRoutes[index].DisableAllRuntimeInstances();
    }

    private void RefreshSelectionStateText()
    {
        if (selectionStateText == null)
            return;

        switch (currentPathOption)
        {
            case sh_PCPathOption.PC1:
                selectionStateText.text = pc1SelectionLabel;
                break;
            case sh_PCPathOption.PC2:
                selectionStateText.text = pc2SelectionLabel;
                break;
            default:
                selectionStateText.text = noSelectionLabel;
                break;
        }
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

    private void SetStatusMessage(string message)
    {
        if (statusText == null)
            return;

        if (hasReachedDestination && message != destinationReachedMessage)
            return;

        statusText.text = message;
    }
}
