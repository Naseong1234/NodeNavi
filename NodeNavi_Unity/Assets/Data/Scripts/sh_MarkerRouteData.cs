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

    public string MarkerName => markerName;
    public int RouteOrder => routeOrder;
    public Transform KnownMarkerTransform => knownMarkerTransform;
    public GameObject RoutePrefab => routePrefab;
    public GameObject RuntimeInstance
    {
        get => runtimeInstance;
        set => runtimeInstance = value;
    }
}
