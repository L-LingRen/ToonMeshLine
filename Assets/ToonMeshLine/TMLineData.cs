using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "TMLineData", menuName = "ToonMeshLine/TMLineData", order = 0)]
public class TMLineData : ScriptableObject {
    public List<TMLineDataStruct> Lines = new();

#if UNITY_EDITOR
    public void RefreshTMLineData(Mesh Mesh) {
        if (Mesh == null) {
            Debug.LogError("Mesh is null");
            return;
        }
    
        var Vertices = Mesh.vertices;
        var Triangles = Mesh.triangles;
        var TrianglesCount = Triangles.Length / 3;
    
        // 检测并收集所有边, 不包括掉重复的边
        var TMDataDic = new Dictionary<string, TMLineDataStruct>();
        for (int i = 0; i < TrianglesCount; i++) {
            var vertex1Index = Triangles[i * 3];
            var vertex2Index = Triangles[i * 3 + 1];
            var vertex3Index = Triangles[i * 3 + 2];
            AddLine(vertex1Index, vertex2Index, vertex3Index, Vertices, TMDataDic);
            AddLine(vertex2Index, vertex3Index, vertex1Index, Vertices, TMDataDic);
            AddLine(vertex3Index, vertex1Index, vertex2Index, Vertices, TMDataDic);
        }
        
        Lines.Clear();
        foreach (var Item in TMDataDic.Values) {
            Lines.Add(Item);
        }

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssetIfDirty(this);
        Debug.Log("TMLine Data Refresh Successfully");
    }
#endif

    static string GetLineId(Vector3 Point1, Vector3 Point2) {
        return $"({Point1.x:f4},{Point1.y:f4},{Point1.z:f4})-({Point2.x:f4},{Point2.y:f4},{Point2.z:f4})";
    }
    
    static void AddLine(int Vertex1Index, int Vertex2Index, int Vertex3Index, Vector3[] Vertices, Dictionary<string, TMLineDataStruct> TMDataDic) {
        var point1 = Vertices[Vertex1Index];
        var point2 = Vertices[Vertex2Index];
        TMLineDataStruct Data;
        if (TMDataDic.TryGetValue(GetLineId(point1, point2), out Data)) {
            if (Data.Triangle2Vertex3 == -1) {
                Data.Triangle2Vertex3 = Vertex3Index;
                TMDataDic[GetLineId(point1, point2)] = Data;
            }
        } else if (TMDataDic.TryGetValue(GetLineId(point2, point1), out Data)) {
            if (Data.Triangle2Vertex3 == -1) {
                Data.Triangle2Vertex3 = Vertex3Index;
                TMDataDic[GetLineId(point2, point1)] = Data;
            }
        } else {
            TMDataDic.Add(GetLineId(point1, point2), new TMLineDataStruct {
                Vertex1 = Vertex1Index, 
                Vertex2 = Vertex2Index, 
                Triangle1Vertex3 = Vertex3Index, 
                Triangle2Vertex3 = -1
            });
        }
    }
}

[Serializable]
public struct TMLineDataStruct {
    /// <summary>
    /// 构成边的顶点1的索引
    /// </summary>
    public int Vertex1;
    /// <summary>
    /// 构成边的顶点2的索引
    /// </summary>
    public int Vertex2;
    /// <summary>
    /// 边所在三角面1的顶点3索引
    /// </summary>
    public int Triangle1Vertex3;
    /// <summary>
    /// 边所在三角面2的顶点3索引
    /// </summary>
    public int Triangle2Vertex3;
}