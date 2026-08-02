using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

[System.Serializable]
public class MapBlockData
{
    public string block_id;
    public string name;
    public string type;
    public float height;
    public List<float[]> coordinates;
}

public class MapGenerator : MonoBehaviour
{
    [Header("JSON 파일 이름 (Assets/StreamingAssets 폴더 내)")]
    public string jsonFileName = "nomadnest_map.json";

    [Header("생성될 맵의 부모 객체")]
    public Transform mapParent;

    [Header("사용할 머티리얼")]
    public Material polygonMaterial; // 인스펙터에서 드래그하여 할당
    public Material wallMaterial;    // 인스펙터에서 드래그하여 할당

    void Start()
    {
        GenerateMapFromJson();
    }

    public void GenerateMapFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, jsonFileName);
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[MapGenerator] 파일을 찾을 수 없습니다: {filePath}");
            return;
        }

        string jsonString = File.ReadAllText(filePath);
        List<MapBlockData> mapDataList = JsonConvert.DeserializeObject<List<MapBlockData>>(jsonString);

        foreach (var data in mapDataList)
        {
            if (data.type == "Polygon") CreatePolygonBlock(data);
            else if (data.type == "Line") CreateLineWall(data);
        }
    }

    private void CreatePolygonBlock(MapBlockData data)
    {
        if (data.coordinates == null || data.coordinates.Count < 3) return;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var point in data.coordinates)
        {
            if (point[0] < minX) minX = point[0];
            if (point[0] > maxX) maxX = point[0];
            if (point[1] < minZ) minZ = point[1];
            if (point[1] > maxZ) maxZ = data.coordinates[1][1];
        }

        Vector3 center = new Vector3((minX + maxX) / 2f, data.height / 2f, (minZ + maxZ) / 2f);
        Vector3 size = new Vector3(maxX - minX, data.height, maxZ - minZ);

        if (size.x < 0.01f || size.z < 0.01f) return;

        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = $"[Polygon] {data.block_id}";
        block.transform.SetParent(mapParent);
        block.transform.position = center;
        block.transform.localScale = size;

        // 인스펙터에서 할당한 머티리얼을 복제하여 사용
        if (polygonMaterial != null)
        {
            block.GetComponent<Renderer>().material = new Material(polygonMaterial);
        }
    }

    private void CreateLineWall(MapBlockData data)
    {
        if (data.coordinates == null || data.coordinates.Count < 2) return;

        Vector3 start = new Vector3(data.coordinates[0][0], 0, data.coordinates[0][1]);
        Vector3 end = new Vector3(data.coordinates[1][0], 0, data.coordinates[1][1]);

        float distance = Vector3.Distance(start, end);
        Vector3 center = (start + end) / 2f;
        center.y = data.height / 2f;

        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = $"[LineWall] {data.block_id}";
        wall.transform.SetParent(mapParent);
        wall.transform.position = center;
        wall.transform.localScale = new Vector3(0.2f, data.height, distance);
        wall.transform.rotation = Quaternion.LookRotation(end - start);

        // 인스펙터에서 할당한 머티리얼을 복제하여 사용
        if (wallMaterial != null)
        {
            wall.GetComponent<Renderer>().material = new Material(wallMaterial);
        }
    }
}