using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TMLineRenderer))]
public class TMLineRendererEditor : Editor {
    TMLineRenderer TMLineRenderer;
    
    void OnEnable() {
        TMLineRenderer = (TMLineRenderer) target;
    }

    public override void OnInspectorGUI() {
        if (!TMLineRenderer.IsInit) {
            if (GUILayout.Button("Init")) {
                TMLineRenderer.Init();
            }
            base.OnInspectorGUI();
        }
        else {
            if (GUILayout.Button("Refresh TMLineData")) {
                TMLineRenderer.RefreshTMLineData();
            }
            base.OnInspectorGUI();
            var MatEditor = (MaterialEditor) CreateEditor(TMLineRenderer.Material);
            MatEditor.PropertiesGUI();
        }
    }
}