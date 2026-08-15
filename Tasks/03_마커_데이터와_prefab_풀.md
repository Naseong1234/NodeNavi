# Task 03. 마커 데이터와 prefab 풀

## 목표

마커 이름·경로 순서·가상 마커·prefab을 한 데이터 항목으로 묶고, prefab 6개를 앱 시작 시 한 번만 생성해 관리합니다.

## 구현 파일

- `NodeNavi_Unity/Assets/Data/Scripts/sh_MarkerRouteController.cs`
- 필요 시 `NodeNavi_Unity/Assets/Data/Scripts/sh_MarkerRouteData.cs`

## 작업

1. `markerName`, `routeOrder`, `knownMarkerTransform`, `routePrefab`, `runtimeInstance`를 가진 마커 데이터를 정의합니다.
2. `MarkerRouteController`가 Inspector의 마커 데이터 6개를 검사하도록 만듭니다.
3. 이름 중복, 빈 prefab, 빈 Transform, 중복된 `routeOrder`는 Console 오류로 알립니다.
4. 각 prefab을 `RouteContentRoot` 아래에 한 번만 생성합니다.
5. 생성 즉시 모든 인스턴스를 비활성화하고 `runtimeInstance`에 보관합니다.
6. `ARTrackedImageManager.trackedImagesChanged` 이벤트 구독·해제를 구현합니다. 이 Task에서는 이벤트 수신 로그까지만 확인합니다.

## 완료 조건

플레이 중 인스턴스 6개가 단 한 번 생성되며 모두 비활성화되고, 인식 마커 이름이 Console에 출력됩니다.

## 다음 Task 전제

`CheckList/03_마커_데이터와_prefab_풀_체크리스트.md`를 모두 통과해야 합니다.
