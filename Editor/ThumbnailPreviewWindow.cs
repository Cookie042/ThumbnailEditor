using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ThumbnailPreviewWindow : EditorWindow
{
    public static ThumbnailEditorWindow instance;

    public Texture2D activeTexture;
    private void OnGUI()
    {
        var width = EditorGUIUtility.currentViewWidth;

        var rect = EditorGUILayout.GetControlRect(false, width);

        if (activeTexture != null)
        {
            

        }
        EditorGUI.DrawPreviewTexture(rect, activeTexture);
    }
}
