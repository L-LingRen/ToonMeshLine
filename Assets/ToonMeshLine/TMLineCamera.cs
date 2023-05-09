using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class TMLineCamera : MonoBehaviour {
    public CameraEvent CameraEvent = CameraEvent.AfterForwardOpaque;
    static Dictionary<Camera, CommandBuffer> Cache = new Dictionary<Camera, CommandBuffer>();
    const string CommandName = "TMLine";
    Plane[] Planes = new Plane[6];

    void OnEnable() {
        Camera.onPreCull += DrawWithCamera;
    }

    void OnDisable() {
        Camera.onPreCull -= DrawWithCamera;
        foreach (var Item in Cache) {
            if (Item.Key != null) {
                Item.Key.RemoveCommandBuffer(CameraEvent, Item.Value);
            }
        }
        Cache.Clear();
    }

    void DrawWithCamera(Camera Camera) {
        if (Camera.name == "Preview Scene Camera") {
            return;
        }
        
        CommandBuffer Command;
        if (!Cache.ContainsKey(Camera)) {
            Command = new CommandBuffer {
                name = CommandName
            };
            Camera.AddCommandBuffer(CameraEvent, Command);
            Cache[Camera] = Command;
        }

        Command = Cache[Camera];
        Command.Clear();
        GeometryUtility.CalculateFrustumPlanes(Camera, Planes);
        foreach (var Item in TMLineRenderer.Collection.Keys) {
            if (GeometryUtility.TestPlanesAABB(Planes, Item.GetBounds())) { // 是否在摄像机视野中。
                // Debug.Log($"在{Camera.name}视野中");
                Item.Draw(Command);
            }
        }
    }
}