# Task 02. ARScene 및 마커 라이브러리 구성

## 목표

6개 실제 마커 이미지를 AR Foundation이 인식할 수 있도록 등록하고, 건물 좌표계를 담을 Scene 구조를 만듭니다.

## 작업

1. `Assets/Data` 아래에 prefab 보관용 폴더를 만들고 경로 prefab 6개를 준비합니다.
2. Reference Image Library를 만들고 마커 이미지 6개를 추가합니다.
3. 이미지 이름을 `Marker_01`~`Marker_06`으로 통일하고, 실제 인쇄 폭을 각각 15cm로 입력합니다.
4. `XR Origin`의 `AR Tracked Image Manager`에 Image Library를 할당합니다.
5. `ARScene`에 `BuildingContentRoot`를 만듭니다.
6. 그 아래에 `VirtualMarkers`, `RouteContentRoot`를 만듭니다.
7. `VirtualMarkers` 아래에 빈 오브젝트 `Marker_01`~`Marker_06`을 만듭니다.
8. 도면과 실제 측정값을 기준으로 가상 마커 위치·회전을 배치합니다. Unity 1단위는 실제 1m입니다.

## 완료 조건

Inspector에서 이미지 라이브러리 6개, 가상 마커 Transform 6개, 경로 prefab 6개가 준비되어 있습니다.

## 다음 Task 전제

`CheckList/02_ARScene_및_마커_라이브러리_체크리스트.md`를 모두 통과해야 합니다.
