using System;
using UnityEngine;

[Serializable]
public class sh_MarkerRouteData
{
    [SerializeField] private string markerName;
    [SerializeField] private int routeOrder;
    [SerializeField] private Transform knownMarkerTransform;
    [SerializeField] private GameObject routePrefab;
    [SerializeField, HideInInspector] private GameObject runtimeInstance;

    private Vector3 cachedKnownLocalPosition;
    private Quaternion cachedKnownLocalRotation = Quaternion.identity;
    private bool isLocalPoseCached;

    public string MarkerName => markerName;
    public int RouteOrder => routeOrder;
    public Transform KnownMarkerTransform => knownMarkerTransform;
    public GameObject RoutePrefab => routePrefab;
    public GameObject RuntimeInstance
    {
        get => runtimeInstance;
        set => runtimeInstance = value;
    }

    public Vector3 CachedKnownLocalPosition => cachedKnownLocalPosition;
    public Quaternion CachedKnownLocalRotation => cachedKnownLocalRotation;
    public bool IsLocalPoseCached => isLocalPoseCached;

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
