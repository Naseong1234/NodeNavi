using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum sh_PCPathOption
{
    None = 0,
    PC1 = 1,
    PC2 = 2,
}

[Serializable]
public class sh_MarkerRouteData
{
    [SerializeField] private string markerName;
    [SerializeField] private int routeOrder;
    [SerializeField] private Transform knownMarkerTransform;
    [FormerlySerializedAs("routePrefab")]
    [SerializeField] private GameObject bothPrefab;
    [SerializeField] private GameObject pc1RoutePrefab;
    [SerializeField] private GameObject pc2RoutePrefab;
    [FormerlySerializedAs("runtimeInstance")]
    [SerializeField, HideInInspector] private GameObject bothRuntimeInstance;
    [SerializeField, HideInInspector] private GameObject pc1RuntimeInstance;
    [SerializeField, HideInInspector] private GameObject pc2RuntimeInstance;

    private Vector3 cachedKnownLocalPosition;
    private Quaternion cachedKnownLocalRotation = Quaternion.identity;
    private bool isLocalPoseCached;

    public string MarkerName => markerName;
    public int RouteOrder => routeOrder;
    public Transform KnownMarkerTransform => knownMarkerTransform;
    public GameObject BothPrefab => bothPrefab;
    public GameObject PC1RoutePrefab => pc1RoutePrefab;
    public GameObject PC2RoutePrefab => pc2RoutePrefab;
    public Vector3 CachedKnownLocalPosition => cachedKnownLocalPosition;
    public Quaternion CachedKnownLocalRotation => cachedKnownLocalRotation;
    public bool IsLocalPoseCached => isLocalPoseCached;

    public bool HasBothPrefab => bothPrefab != null;
    public bool HasPCSpecificRoutes => pc1RoutePrefab != null || pc2RoutePrefab != null;

    public GameObject GetPrefabForOption(sh_PCPathOption pathOption)
    {
        switch (pathOption)
        {
            case sh_PCPathOption.PC1:
                return pc1RoutePrefab;
            case sh_PCPathOption.PC2:
                return pc2RoutePrefab;
            default:
                return bothPrefab;
        }
    }

    public GameObject GetRuntimeInstance(sh_PCPathOption pathOption)
    {
        switch (pathOption)
        {
            case sh_PCPathOption.PC1:
                return pc1RuntimeInstance;
            case sh_PCPathOption.PC2:
                return pc2RuntimeInstance;
            default:
                return bothRuntimeInstance;
        }
    }

    public void SetRuntimeInstance(sh_PCPathOption pathOption, GameObject instance)
    {
        switch (pathOption)
        {
            case sh_PCPathOption.PC1:
                pc1RuntimeInstance = instance;
                break;
            case sh_PCPathOption.PC2:
                pc2RuntimeInstance = instance;
                break;
            default:
                bothRuntimeInstance = instance;
                break;
        }
    }

    public void DisableAllRuntimeInstances()
    {
        if (bothRuntimeInstance != null)
            bothRuntimeInstance.SetActive(false);

        if (pc1RuntimeInstance != null)
            pc1RuntimeInstance.SetActive(false);

        if (pc2RuntimeInstance != null)
            pc2RuntimeInstance.SetActive(false);
    }

    /// <summary>
    /// BuildingContentRoot 기준의 설계 로컬 좌표 및 로컬 회전을 캐싱합니다.
    /// 하위 계층(예: MarkersPosition)에 속해 있어도 올바른 루트 상대 Pose를 구합니다.
    /// </summary>
    public void CacheLocalPose(Transform rootTransform)
    {
        if (knownMarkerTransform == null || rootTransform == null)
            return;

        cachedKnownLocalPosition = rootTransform.InverseTransformPoint(knownMarkerTransform.position);
        cachedKnownLocalRotation = Quaternion.Inverse(rootTransform.rotation) * knownMarkerTransform.rotation;
        isLocalPoseCached = true;
    }
}
