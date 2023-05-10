using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class TMLineRenderer : MonoBehaviour {
    [NonSerialized]
    public bool IsInit;
    public TMLineData TMLineData;
    public Material Material;
    public bool UseScale;
    public static Dictionary<TMLineRenderer, bool> Collection = new Dictionary<TMLineRenderer, bool>();
    [NonSerialized]
    public Mesh Mesh;
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
    
    public void Draw(CommandBuffer CommandBuffer) {
        if (!IsInit) { return; }

        // Debug.Log($"绘制{TMLineData.Lines.Count}");
        TryUpdateMaterialHotData();
        var localToWorldMatrix = Renderer.localToWorldMatrix;
        if (UseScale) {
            var lossyScale = Renderer.transform.lossyScale;
            lossyScale.x = 1.0f / lossyScale.x;
            lossyScale.y = 1.0f / lossyScale.y;
            lossyScale.z = 1.0f / lossyScale.z;
            localToWorldMatrix *= Matrix4x4.Scale(lossyScale);
        }
        CommandBuffer.DrawProcedural(localToWorldMatrix, Material, 0, MeshTopology.Points, TMLineData.Lines.Count);
    }

    public Bounds GetBounds() {
        return Renderer.bounds;
    }

    void OnEnable() {
        TryInit();
        Collection[this] = true;
    }

    void OnDisable() {
        TryRelease();
        Collection.Remove(this);
    }

    void OnValidate() {
        TryInit();
    }

    public void TryInit() {
        if (IsInit) {
            return;
        }
        
        Renderer = GetComponent<Renderer>();
        if (Material == null) {
            Debug.LogError("Material is null");
            return;
        }

        if (TMLineData == null) {
            Debug.LogError("TMLineData is null");
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
        else if (Renderer is SkinnedMeshRenderer SkinnedMeshRenderer) {
            Mesh = new Mesh();
        } 
        else {
            Debug.LogError("TMLine only support 'MeshRenderer' and 'SkinnedMeshRenderer'");
            return;
        }
        
        Uvs.Clear();
        Normals.Clear();
        Vertices.Clear();
        
        if (Renderer is MeshRenderer) {
            Mesh = GetComponent<MeshFilter>().sharedMesh;
            Vertices.AddRange(Mesh.vertices);
            Normals.AddRange(Mesh.normals);
        } 
        else if (Renderer is SkinnedMeshRenderer SkinnedMeshRenderer) {
            Mesh = new Mesh();
            SkinnedMeshRenderer.BakeMesh(Mesh);
            Mesh.RecalculateNormals();
            Mesh.GetVertices(Vertices);
            Mesh.GetNormals(Normals);
        }
        Uvs.AddRange(Mesh.uv);
        
        VerticesBuffer = new ComputeBuffer(Vertices.Count, Marshal.SizeOf(typeof(Vector3)));
        NormalsBuffer = new ComputeBuffer(Normals.Count, Marshal.SizeOf(typeof(Vector3)));
        UvsBuffer = new ComputeBuffer(Uvs.Count, Marshal.SizeOf(typeof(Vector2)));
        
        UvsBuffer.SetData(Uvs);
        NormalsBuffer.SetData(Normals);
        VerticesBuffer.SetData(Vertices);
        
        Material.SetBuffer(UvsID, UvsBuffer);
        Material.SetBuffer(NormalsID, NormalsBuffer);
        Material.SetBuffer(VerticesID, VerticesBuffer);

        IsInit = true;
        if (TMLineData.Lines.Count > 0) {
            TryLoadTMData();
        }
        
        Debug.Log("初始化完成");
    }

    public bool TryLoadTMData() {
        if (!IsInit || TMLineData == null) { return false; }
        
        if (TMDataBuffer != null) {
            TMDataBuffer.Release();
        }
        
        TMLineData.RefreshTMLineData(Mesh);
        TMDataBuffer = new ComputeBuffer(TMLineData.Lines.Count, Marshal.SizeOf(typeof(TMLineDataStruct)));
        TMDataBuffer.SetData(TMLineData.Lines);
        Material.SetBuffer(TMDataID, TMDataBuffer);
        return true;
    }
    
    bool TryUpdateMaterialHotData() {
        if (Renderer is SkinnedMeshRenderer SkinnedMeshRenderer) {
            SkinnedMeshRenderer.BakeMesh(Mesh);
            Mesh.RecalculateNormals();
            
            Vertices.Clear();
            Mesh.GetVertices(Vertices);
            VerticesBuffer.SetData(Vertices);
            
            Normals.Clear();
            Mesh.GetNormals(Normals);
            NormalsBuffer.SetData(Normals);

#if UNITY_EDITOR
            Material.SetBuffer(UvsID, UvsBuffer);
            Material.SetBuffer(NormalsID, NormalsBuffer);
            Material.SetBuffer(VerticesID, VerticesBuffer);
            Material.SetBuffer(TMDataID, TMDataBuffer);
#endif
            return true;
        }
        
        if (Renderer is MeshRenderer) {
            
#if UNITY_EDITOR
            Material.SetBuffer(UvsID, UvsBuffer);
            Material.SetBuffer(NormalsID, NormalsBuffer);
            Material.SetBuffer(VerticesID, VerticesBuffer);
            Material.SetBuffer(TMDataID, TMDataBuffer);
#endif
        }

        return false;
    }

    void TryRelease() {
        if (!IsInit) { return; }
        
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
        Debug.Log("数据销毁成功");
    }
    
    void OnDestroy() {
        TryRelease();
    }
}
