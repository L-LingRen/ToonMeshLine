using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[ExecuteInEditMode]
[RequireComponent(typeof(Renderer))]
public class TMLineRenderer : MonoBehaviour {
    [HideInInspector]
    public bool IsInit;
    public List<TMDataStruct> TMData = new List<TMDataStruct>();
    public Material Material;
    public bool UseScale;
    public static Dictionary<TMLineRenderer, bool> Collection = new Dictionary<TMLineRenderer, bool>();
    Mesh Mesh;
    Renderer Renderer;
    ComputeBuffer UvsBuffer;
    ComputeBuffer TMDataBuffer;
    ComputeBuffer NormalsBuffer;
    ComputeBuffer VerticesBuffer;
    List<Vector2> Uvs = new List<Vector2>();
    List<Vector3> Normals = new List<Vector3>();
    List<Vector3> Vertices = new List<Vector3>();
    static readonly int VerticesID = Shader.PropertyToID("Vertices");
    static readonly int NormalsID = Shader.PropertyToID("Normals");
    static readonly int TMDataID = Shader.PropertyToID("TMData");
    static readonly int UvsID = Shader.PropertyToID("Uvs");

    //退化四边形
    [Serializable]
    public struct TMDataStruct {
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
    
    public void Draw(CommandBuffer CommandBuffer) {
        if (!IsInit) { return; }
        UpdateMaterialData(GetHotMesh(), Uvs, Normals, Vertices, TMData);
        var localToWorldMatrix = Renderer.localToWorldMatrix;
        if (UseScale) {
            var lossyScale = Renderer.transform.lossyScale;
            lossyScale.x = 1.0f / lossyScale.x;
            lossyScale.y = 1.0f / lossyScale.y;
            lossyScale.z = 1.0f / lossyScale.z;
            localToWorldMatrix *= Matrix4x4.Scale(lossyScale);
        }
        CommandBuffer.DrawProcedural(localToWorldMatrix, Material, 0, MeshTopology.Points, TMDataBuffer.count);
    }

    public Bounds GetBounds() {
        return Renderer.bounds;
    }

    void OnEnable() {
        Init();
        Collection[this] = true;
    }

    void OnDisable() {
        Release();
        Collection.Remove(this);
    }

    void OnValidate() {
        if (Material == null) {
            Release();
        }
        else if (!IsInit) {
            Init();
        }
    }
    
    public void Init() {
        Release();
        IsInit = false;
        Renderer = GetComponent<Renderer>();
        if (Material == null) {
            Debug.LogError("Material is null");
            return;
        }
        
        if (Material.shader.name != "TMLine/TMLine") {
            Debug.LogError($"{Material.shader.name} is not TMLine.shader");
            return;
        }

        if (Application.isPlaying) {
            Material = Instantiate(Material);
        }
        
        if (Renderer is MeshRenderer) {
            Mesh = GetComponent<MeshFilter>().sharedMesh;
        } 
        else if (Renderer is SkinnedMeshRenderer) {
            Mesh = new Mesh();
        } 
        else {
            Debug.LogError("TMLine only support 'MeshRenderer' and 'SkinnedMeshRenderer'");
            return;
        }

        var HotMesh = GetHotMesh();
        if (HotMesh == null) {
            Debug.LogError("Mesh is null");
            return;
        }

        if (TMData.Count <= 0) {
            RefreshTMLineData(HotMesh, TMData);
        }

        Vertices.AddRange(HotMesh.vertices);
        Normals.AddRange(HotMesh.normals);
        Uvs.AddRange(HotMesh.uv);
        VerticesBuffer = new ComputeBuffer(Vertices.Count, Marshal.SizeOf(typeof(Vector3)));
        NormalsBuffer = new ComputeBuffer(Normals.Count, Marshal.SizeOf(typeof(Vector3)));
        TMDataBuffer = new ComputeBuffer(TMData.Count, Marshal.SizeOf(typeof(TMDataStruct)));
        UvsBuffer = new ComputeBuffer(Uvs.Count, Marshal.SizeOf(typeof(Vector2)));
        UpdateMaterialData(HotMesh, Uvs, Normals, Vertices, TMData);
        IsInit = true;
    }
    
    void UpdateMaterialData(Mesh Mesh, List<Vector2> Uvs, List<Vector3> Normals, List<Vector3> Vertices, List<TMDataStruct> TMData) {
        UvsBuffer.SetData(Uvs);
        TMDataBuffer.SetData(TMData);
        NormalsBuffer.SetData(Normals);
        VerticesBuffer.SetData(Vertices);
        
        Material.SetBuffer(VerticesID, VerticesBuffer);
        Material.SetBuffer(NormalsID, NormalsBuffer);
        Material.SetBuffer(TMDataID, TMDataBuffer);
        Material.SetBuffer(UvsID, UvsBuffer);
    }

    Mesh GetHotMesh() {
        if (Renderer is SkinnedMeshRenderer SkinnedMeshRenderer) {
            SkinnedMeshRenderer.BakeMesh(Mesh);
            Mesh.RecalculateNormals();
            Mesh.GetVertices(Vertices);
            Mesh.GetNormals(Normals);
        }
        return Mesh;
    }

    void Release() {
        IsInit = false;
        VerticesBuffer?.Release();
        NormalsBuffer?.Release();
        TMDataBuffer?.Release();
        UvsBuffer?.Release();
        if (Mesh != null && Renderer is SkinnedMeshRenderer) {
            DestroyImmediate(Mesh);
        }

        VerticesBuffer = null;
        NormalsBuffer = null;
        TMDataBuffer = null;
        UvsBuffer = null;
        Mesh = null;
        Vertices.Clear();
        Normals.Clear();
        Uvs.Clear();
    }
    
    void OnDestroy() {
        Release();
    }

#if UNITY_EDITOR
    public void RefreshTMLineData() {
        RefreshTMLineData(GetHotMesh(), TMData);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    static void RefreshTMLineData(Mesh Mesh, List<TMDataStruct> TMData) {
        if (Mesh == null) {
            Debug.LogError("Mesh is null");
            return;
        }
    
        var Vertices = Mesh.vertices;
        var Triangles = Mesh.triangles;
        var TrianglesCount = Triangles.Length / 3;
    
        // 检测并收集所有边, 不包括掉重复的边
        var TMDataDic = new Dictionary<string, TMDataStruct>();
        for (int i = 0; i < TrianglesCount; i++) {
            var vertex1Index = Triangles[i * 3];
            var vertex2Index = Triangles[i * 3 + 1];
            var vertex3Index = Triangles[i * 3 + 2];
            AddLine(vertex1Index, vertex2Index, vertex3Index, Vertices, TMDataDic);
            AddLine(vertex2Index, vertex3Index, vertex1Index, Vertices, TMDataDic);
            AddLine(vertex3Index, vertex1Index, vertex2Index, Vertices, TMDataDic);
        }
        
        TMData.Clear();
        foreach (var Item in TMDataDic.Values) {
            TMData.Add(Item);
        }
        
        Debug.Log("TMLine Data Refresh Successfully");
    }

    static string GetLineId(Vector3 Point1, Vector3 Point2) {
        return $"({Point1.x:f4},{Point1.y:f4},{Point1.z:f4})-({Point2.x:f4},{Point2.y:f4},{Point2.z:f4})";
    }
    
    static void AddLine(int Vertex1Index, int Vertex2Index, int Vertex3Index, Vector3[] Vertices, Dictionary<string, TMDataStruct> TMDataDic) {
        var point1 = Vertices[Vertex1Index];
        var point2 = Vertices[Vertex2Index];
        TMDataStruct Data;
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
            TMDataDic.Add(GetLineId(point1, point2), new TMDataStruct {
                Vertex1 = Vertex1Index, 
                Vertex2 = Vertex2Index, 
                Triangle1Vertex3 = Vertex3Index, 
                Triangle2Vertex3 = -1
            });
        }
    }
}
