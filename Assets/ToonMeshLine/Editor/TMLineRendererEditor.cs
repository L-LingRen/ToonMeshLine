using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TMLineRenderer))]
public class TMLineRendererEditor : Editor {
    TMLineRenderer TMLineRenderer;
    MaterialEditor MaterialEditor;
    
    void OnEnable() {
        TMLineRenderer = (TMLineRenderer) target;
    }

    public override void OnInspectorGUI() {
        if (GUILayout.Button("Refresh TMLineData")) {
            TMLineRenderer.TryLoadTMData();
        }
        base.OnInspectorGUI();
        if (MaterialEditor == null && TMLineRenderer.Material != null) {
            MaterialEditor = (MaterialEditor) CreateEditor(TMLineRenderer.Material);
        }

        if (MaterialEditor != null) {
            MaterialEditor.PropertiesGUI();
        }
    }
}