﻿using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public partial class ThumbnailEditorWindow
{

    [System.Serializable]
    public struct MeshObject
    {
        public GameObject prefabObject;
        public bool hasCustomSettings;
        [Range(-180,180)]
        public float orbitYaw;
        [Range(0, 90)]
        public float orbitPitch;
        [Range(.1f, 20)]
        public float orbitDistance;
        [Range(-2, 2)]
        public float orbitHeight;
        public Texture2D Thumbnail;

        public bool selected;
    }

    [CustomPropertyDrawer(typeof(MeshObject))]
    public class MeshObjectDrawer : PropertyDrawer
    {
        private static Color _selectedColor = new Color(0, 0, 1, .2f);
        private static Color _backColor = new Color(0, 0, 0, .2f);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            EditorGUI.indentLevel = 2;
            Rect indentedRect = EditorGUI.IndentedRect(position);
            EditorGUI.indentLevel = 0;

            EditorGUIUtility.wideMode = true;

            var height = EditorGUIUtility.singleLineHeight;
            var width = indentedRect.width;
            var spacing = EditorGUIUtility.standardVerticalSpacing;

            var ThumbnailHeight = height * 3 + spacing * 2;

            var isCustomSettings = property.FindPropertyRelative("hasCustomSettings");
            var orbitYaw = property.FindPropertyRelative("orbitYaw");
            var orbitPitch = property.FindPropertyRelative("orbitPitch");
            var orbitDistance = property.FindPropertyRelative("orbitDistance");
            var orbitHeight = property.FindPropertyRelative("orbitHeight");
            var thumbnail = property.FindPropertyRelative("Thumbnail");
            var isSelected = property.FindPropertyRelative("selected");
            var prefab = property.FindPropertyRelative("prefabObject").objectReferenceValue as GameObject;


            if (Event.current.type == EventType.MouseDown)
            {
                if (position.Contains(Event.current.mousePosition))
                {
                    isSelected.boolValue = true;
                }

            }


            var thumbRect = new Rect(indentedRect);
            thumbRect.width = thumbRect.height = ThumbnailHeight;
            EditorGUI.DrawRect(indentedRect, isSelected.boolValue ? _selectedColor : _backColor );

            //EditorGUI.DrawTextureTransparent(thumbRect, thumbnail.objectReferenceValue as Texture2D);
            //EditorGUI.DrawTextureAlpha(thumbRect, thumbnail.objectReferenceValue as Texture2D);
            EditorGUI.DrawPreviewTexture(thumbRect, thumbnail.objectReferenceValue as Texture2D);


            var LeftAreaRect = new Rect(indentedRect);
            LeftAreaRect.xMin = thumbRect.xMax + spacing;
            LeftAreaRect.xMax = position.width;
            LeftAreaRect.width = (LeftAreaRect.width - spacing * 2) / 3;

            LeftAreaRect.height = height;

            EditorGUIUtility.labelWidth = 80;
            if (prefab != null)
                EditorGUI.LabelField(LeftAreaRect, prefab.name);
            LeftAreaRect.x += LeftAreaRect.width + spacing;
            EditorGUI.PropertyField(LeftAreaRect, isCustomSettings, new GUIContent("Customize"));
            LeftAreaRect.x += LeftAreaRect.width + spacing;
            EditorGUI.PropertyField(LeftAreaRect, isSelected, new GUIContent("Select"));


            LeftAreaRect.xMin = thumbRect.xMax + spacing;
            LeftAreaRect.xMax = position.width;
            LeftAreaRect.y += height + spacing;

            if (isCustomSettings.boolValue)
            {
                var left13rd = new Rect(LeftAreaRect);
                left13rd.width = (LeftAreaRect.width - spacing * 3) / 2;
                EditorGUI.PropertyField(left13rd, orbitYaw, new GUIContent("Orbit Yaw"));
                left13rd.x += left13rd.width + spacing;
                EditorGUI.PropertyField(left13rd, orbitPitch, new GUIContent("Orbit Pitch"));
                left13rd.x -= left13rd.width + spacing;
                left13rd.y += height + spacing;
                EditorGUI.PropertyField(left13rd, orbitHeight, new GUIContent("Orbit Height"));
                left13rd.x += left13rd.width + spacing;
                EditorGUI.PropertyField(left13rd, orbitDistance, new GUIContent("Orbit Dist"));

            }

            property.serializedObject.ApplyModifiedProperties();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var prefab = property.FindPropertyRelative("prefabObject").objectReferenceValue as GameObject;
            
            var lheight = EditorGUIUtility.singleLineHeight;
            var lspace = EditorGUIUtility.standardVerticalSpacing;

            return lheight * 3 + lspace * 2;
        }

        
    }
}
