using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class ScaleTestScripts : MonoBehaviour
{
    private bool isAligned = false; // 최초 1회 정렬을 위한 플래그

    // ==========================================
    // 질문자님의 커스텀 슬라이더 로직 (100% 원본 유지)
    // ==========================================
    public void ScaleMoveX(Slider slider)
    {
        GameObject map = GameObject.FindWithTag("Map");
        if (map == null || !isAligned) return;
        Vector3 currentScale = map.transform.localScale;
        map.transform.localScale = new Vector3(slider.value, currentScale.y, currentScale.z);
    }

    public void ScaleMoveZ(Slider slider)
    {
        GameObject map = GameObject.FindWithTag("Map");
        if (map == null || !isAligned) return;
        Vector3 currentScale = map.transform.localScale;
        map.transform.localScale = new Vector3(currentScale.x, currentScale.y, slider.value);
    }

    public void PositionMoveX(Slider slider)
    {
        GameObject map = GameObject.FindWithTag("Map");
        if (map == null || !isAligned) return;
        Vector3 currentLocalPos = map.transform.localPosition;
        map.transform.localPosition = new Vector3(slider.value, currentLocalPos.y, currentLocalPos.z);
    }

    public void PositionMoveY(Slider slider)
    {
        GameObject map = GameObject.FindWithTag("Map");
        if (map == null || !isAligned) return;
        Vector3 currentLocalPos = map.transform.localPosition;
        map.transform.localPosition = new Vector3(currentLocalPos.x, slider.value, currentLocalPos.z);
    }

    public void PositionMoveZ(Slider slider)
    {
        GameObject map = GameObject.FindWithTag("Map");
        if (map == null || !isAligned) return;
        Vector3 currentLocalPos = map.transform.localPosition;
        map.transform.localPosition = new Vector3(currentLocalPos.x, currentLocalPos.y, slider.value);
    }

    // ==========================================
    // AR 앵커 및 마커 인식 로직
    // ==========================================
    public void OnMarkerChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            if (!isAligned)
            {
                AlignAndAnchorMap(trackedImage.transform);
            }
        }
    }

    private void AlignAndAnchorMap(Transform markerTransform)
    {
        GameObject map = GameObject.FindWithTag("Map");
        GameObject point = GameObject.FindWithTag("point");

        if (map == null || point == null) return;

        // 1. 마커와 point의 위치 차이(Offset)를 계산하여 맵을 현실 마커 위치로 이동시킵니다.
        Vector3 positionOffset = markerTransform.position - point.transform.position;
        map.transform.position += positionOffset;

        // 2. 맵이 이동한 현재 위치에 "투명한 닻(Anchor)" 역할을 할 빈 오브젝트를 생성합니다.
        GameObject anchorObject = new GameObject("AR_World_Anchor");
        anchorObject.transform.position = map.transform.position;
        anchorObject.transform.rotation = map.transform.rotation; // 기존 축 방향 유지

        // 3. 이 빈 오브젝트에 ARAnchor 컴포넌트를 붙여서 현실 월드 공간에 꽉 고정시킵니다.
        anchorObject.AddComponent<ARAnchor>();

        // 4. 이제 맵(Map)을 AR 마커에서 떼어내고, 방금 만든 '고정된 닻'의 자식으로 넣습니다.
        map.transform.SetParent(anchorObject.transform, true);

        // 5. 완료. 이후부터는 슬라이더가 작동합니다.
        isAligned = true;
    }
}