using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public partial class ThumbnailEditorWindow : EditorWindow
{
    private static ThumbnailEditorWindow instance;
    private Camera renderCamera;

    [System.Serializable]
    public struct ThumbnailEditorSettings
    {
        public int thumbnailSize;
        public string path;
        [Range(-180, 180)]
        public float orbitYaw;
        [Range(-90, 90)]
        public float orbitPitch;
        [Range(-2, 2)]
        public float orbitHeight;
        [Range(.01f, 20)]
        public float orbitDistance;
        [Range(.1f, 130f)]
        public float cameraFOV;
        public int renderLayer;
    }
    private ThumbnailEditorSettings settings;

    private readonly List<PrefabObject> _prefabObjects = new List<PrefabObject>();

    private PrefabObject activeObject;

    private Vector2 scrollPos = Vector2.zero;

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        EditorGUIUtility.wideMode = true;
        EditorGUIUtility.labelWidth = 135;

        SerializedObject editorWindowObject = new SerializedObject(this);

        //! Draw path text field and browse button
        EditorGUILayout.BeginHorizontal();
        {
            settings.path = EditorGUILayout.TextField("SaveDirectory", settings.path);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                settings.path = EditorUtility.OpenFolderPanel("Tumbnail Save Path", settings.path, Application.dataPath);
            }
        }
        EditorGUILayout.EndHorizontal();

        var settingsProp = editorWindowObject.FindProperty("settings");

        var renderLayerProp = settingsProp.FindPropertyRelative("renderLayer");

        //! Render Mask
        EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("thumbnailSize"));
        renderLayerProp.intValue = EditorGUILayout.LayerField(renderLayerProp.intValue);
        EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("cameraFOV"));

        //! Orbit Properties
        EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("orbitYaw"));
        EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("orbitPitch"));
        EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("orbitHeight"));
        EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("orbitDistance"));

        //var settingsProp = editorWindowObject.FindProperty("settings");
        //EditorGUILayout.PropertyField(settingsProp, true);

        EditorGUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("Render All"))
            {
                RenderPreviews(_prefabObjects);
            }
            if (GUILayout.Button("Render Selected"))
            {
                List<PrefabObject> selected = 
                    _prefabObjects.Where(prefabObject => prefabObject.selected).ToList();
                RenderPreviews(selected);
            }
            if (GUILayout.Button("Save Json/Image Data"))
            {
                //saving images and a json with the settings used to create it
                foreach (PrefabObject meshObject in _prefabObjects)
                {
                    var jsonPath = Path.Combine(settings.path, meshObject.prefabObject.name + ".thumb.json");
                    var imagePath = Path.Combine(settings.path, meshObject.prefabObject.name + ".png");
                    File.WriteAllText(jsonPath, JsonUtility.ToJson(meshObject));
                    File.WriteAllBytes(imagePath, meshObject.thumbnail.EncodeToPNG());
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Mesh List", EditorStyles.boldLabel);

        //get reference to internal _prefabObjects list object
        SerializedProperty meshList = editorWindowObject.FindProperty("_prefabObjects");

        //! Draw the mesh list
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
        EditorGUILayout.PropertyField(meshList, true);
        EditorGUILayout.EndScrollView();

        HandleDragNDropEvents(editorWindowObject);

        editorWindowObject.ApplyModifiedProperties();

        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }

    }

    private void HandleDragNDropEvents(SerializedObject editorWindowObject)
    {
        //Define drag and drop rectangle
        var DragDropRect = GUILayoutUtility.GetLastRect();
        DragDropRect.height = EditorGUIUtility.singleLineHeight;

        //If the mouse is over the drag and drop area
        if (DragDropRect.Contains(Event.current.mousePosition))
        {
            //and the mouse is dragging something
            if (Event.current.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                Event.current.Use();
            }
            // else if mouse dropped
            else if (Event.current.type == EventType.DragPerform)
            {
                foreach (Object obj in DragAndDrop.objectReferences)
                {

                    Debug.Log(obj.GetType().ToString());

                    if (obj is GameObject go)
                    {
                        //look for the data object first in the current directory
                        var path = Path.Combine(settings.path, go.name + ".thumb.json");
                        if (File.Exists(path))
                        {
                            var newobj = JsonUtility.FromJson<PrefabObject>(File.ReadAllText(path));
                            newobj.focused = false;
                            _prefabObjects.Add(newobj);

                        }
                        else
                        {
                            _prefabObjects.Add(new PrefabObject
                            {
                                prefabObject = go,
                                hasCustomSettings = false,
                                orbitHeight = settings.orbitHeight,
                                orbitYaw = settings.orbitYaw,
                                orbitPitch = settings.orbitPitch,
                                orbitDistance = settings.orbitDistance,
                                thumbnail = Texture2D.whiteTexture
                            });
                        }
                    }
                }
            }
        }
    }

    private void RenderPreviews(List<PrefabObject> objects)
    {
        Camera cam = GetRenderCamera();
        cam.cullingMask = 1 << settings.renderLayer;
        cam.fieldOfView = settings.cameraFOV;

        if (cam.targetTexture.height != settings.thumbnailSize)
            cam.targetTexture = new RenderTexture(settings.thumbnailSize, settings.thumbnailSize, 32);


        var DefaultRenderData = GetCameraTransformTuple(settings.orbitYaw, settings.orbitPitch, settings.orbitDistance, settings.orbitHeight);
        (Vector3 pos, Quaternion rot, Vector3 forward, Vector3 target) activeData;

        for (var moIndex = 0; moIndex < objects.Count; moIndex++)
        {
            PrefabObject prefabObject = objects[moIndex];
            //if it has custom render settings, set them
            if (prefabObject.hasCustomSettings)
            {
                activeData = GetCameraTransformTuple(
                    prefabObject.orbitYaw, 
                    prefabObject.orbitPitch,
                    prefabObject.orbitDistance,
                    prefabObject.orbitHeight);
            }
            else
                activeData = DefaultRenderData;


            Matrix4x4 mtx = Matrix4x4.TRS(activeData.pos, activeData.rot, Vector3.one);
            //Matrix4x4 mtx = Matrix4x4.TRS(Vector3.zero , Quaternion.identity, Vector3.one);

            //find every mesh renderer in the prefab
            MeshRenderer[] mrs = prefabObject.prefabObject.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer mr in mrs)
            {
                //get the matching mesh filter
                MeshFilter mf = mr.gameObject.GetComponent<MeshFilter>();
                Mesh mesh = mf.sharedMesh;
                int subMeshCount = mf.sharedMesh.subMeshCount;
                for (int i = 0; i < subMeshCount; i++)
                {
                    Material submeshMaterial = mr.sharedMaterials[i];
                    if (submeshMaterial)
                    {
                        Graphics.DrawMesh(mesh, mtx.inverse, submeshMaterial, settings.renderLayer, cam);
                        Debug.Log("rendered to " + 0);
                    }
                    else
                        Debug.LogError("missing material for submesh index: " + i);
                }
            }

            var texture = new Texture2D(settings.thumbnailSize, settings.thumbnailSize);

            RenderTargetToTexture(cam, ref texture);

            prefabObject.thumbnail = texture;

            objects[moIndex] = prefabObject;


            //var pngData = texture.EncodeToPNG();

            //var imagePath = Path.Combine(settings.path, prefabObject.prefabObject.name + ".png");
            // For testing purposes, also write to a file in the project folder
            //File.WriteAllBytes(imagePath, pngData);

            //AssetDatabase.CreateAsset(t, $"Assets/{levelTile.name}.png");
        }
    }

    private Camera GetRenderCamera()
    {
        var returnCam = renderCamera;
        if (returnCam == null)
        {
            //first try and find it
            var cams = FindObjectsOfType<Camera>();
            foreach (var camera in cams)
            {
                if (camera.gameObject.name.Contains("thumbnail Camera"))
                {
                    returnCam = camera;
                    break;
                }
            }

            //if it didnt find one set one up
            if (returnCam == null)
            {
                var go = new GameObject("thumbnail Camera");
                returnCam = go.AddComponent<Camera>();
                returnCam.transform.position = Vector3.zero;
                returnCam.transform.rotation = Quaternion.identity;
                returnCam.transform.localScale = Vector3.one;
                returnCam.clearFlags = CameraClearFlags.Color;
                returnCam.backgroundColor = new Color(0f, 0f, 0f, 0);
                RenderTexture rt = new RenderTexture(settings.thumbnailSize, settings.thumbnailSize, 32);
                returnCam.targetTexture = rt;

                returnCam.cullingMask = settings.renderLayer;
                //returnCam.cullingMask = renderLayer;
            }
        }

        renderCamera = returnCam;

        return returnCam;
    }

    [MenuItem("Cookie Jar/thumbnail Renderer")]
    public static void ShowWindow()
    {
        instance = GetWindow<ThumbnailEditorWindow>();
        SceneView.onSceneGUIDelegate += instance.OnSceneGui;
        Camera.onPreCull += instance.DrawActivePreview;

        //load setting data
        if (EditorPrefs.HasKey("ThumbData"))
            instance.settings = JsonUtility.FromJson<ThumbnailEditorSettings>(EditorPrefs.GetString("ThumbData"));


        instance.Show();
    }

    private void DrawActivePreview(Camera camera)
    {
        if (activeObject != null)
        {
            Graphics.DrawMesh(activeObject.prefabObject.GetComponent<MeshFilter>().sharedMesh, Matrix4x4.identity,
                activeObject.prefabObject.GetComponent<MeshRenderer>().sharedMaterial, 0, camera);
        }

    }


    private static (Vector3 pos, Quaternion rot, Vector3 forward, Vector3 target) GetCameraTransformTuple(float yaw, float pitch, float distance, float height)
    {
        //where the camera will be pointing
        var targetPos = new Vector3(0, height, 0);

        //work out the camera position for the orbit rotations, done in reverse order
        //starting witha a vector pointing backwards at the orbit distance
        var camDelta = -Vector3.forward * distance;

        //rotate it up to the elevation
        //rotate it arond the up axis to the yaw angle
        camDelta = Quaternion.AngleAxis(pitch, Vector3.right) * camDelta;
        camDelta = Quaternion.AngleAxis(-yaw, Vector3.up) * camDelta;

        var lookRotation = Quaternion.LookRotation(-camDelta.normalized, Vector3.up);

        var camPosition = camDelta + targetPos;

        return (camPosition, lookRotation, -camDelta, targetPos);
    }

    private void OnPropClicked(SerializedProperty pObject)
    {
        PrefabObject focused = null;
        foreach (PrefabObject t in _prefabObjects)
        {
            bool equal = t.prefabObject == pObject.FindPropertyRelative("prefabObject").objectReferenceValue as GameObject;
            if (equal)
                focused = t;
            t.focused = equal;
        }
        activeObject = focused;

        Repaint();
        
    }

    private void OnSceneGui(SceneView sceneView)
    {
        var (camPos, camRot, camForward, targetPos) = 
            activeObject != null && activeObject.hasCustomSettings ? 
                GetCameraTransformTuple(
                    activeObject.orbitYaw,
                    activeObject.orbitPitch,
                    activeObject.orbitDistance,
                    activeObject.orbitHeight) :
                GetCameraTransformTuple(
                    settings.orbitYaw,
                    settings.orbitPitch,
                    settings.orbitDistance,
                    settings.orbitHeight);

        var binormal = Vector3.Cross(Vector3.up, camForward).normalized; //camera right
        var normal = Vector3.Cross(camForward, binormal).normalized; //camera up

        if (activeObject == null)
        {
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            Handles.CubeHandleCap(0, Vector3.zero, Quaternion.identity, 1, EventType.Repaint);
        }

        Handles.color = new Color(0f, 0f, 1f, 0.5f);
        Handles.DrawLine(camPos, targetPos);

        var upFOVVector = Quaternion.AngleAxis(settings.cameraFOV / 2, binormal) * camForward;
        var downFOVVector = Quaternion.AngleAxis(settings.cameraFOV / 2, -binormal) * camForward;

        Handles.color = new Color(0f, 1f, 0f, 0.5f);
        Handles.DrawLine(camPos, camPos + normal * .4f);
        Handles.color = new Color(1f, 0f, 0f, 0.25f);
        Handles.DrawLine(camPos, camPos + binormal * .4f);
        Handles.color = new Color(1f, 1f, 0f, 0.25f);
        Handles.DrawLine(camPos, camPos + upFOVVector * .3f);
        Handles.DrawLine(camPos, camPos + downFOVVector * .3f);
        // drawing the box to represent the frame of the thumbnail
        Handles.color = new Color(1f, 1f, 0f, 0.5f * .75f);
        Handles.RectangleHandleCap(
            0, targetPos,
            camRot,
            //quick and dirty field of view as a function of distance math
            (camPos - targetPos).magnitude * Mathf.Tan(settings.cameraFOV / 2 * Mathf.Deg2Rad),
            EventType.Repaint);

        Handles.color = new Color(0f, 0.82f, 1f, 0.5f);
        Handles.matrix = Matrix4x4.TRS(camPos, camRot, Vector3.one);
        Handles.DrawWireCube(Vector3.zero, new Vector3(.25f, .25f, .5f));

        //Handles.BeginGUI();
        //Handles.EndGUI();
        sceneView.Repaint();
    }

    private void OnDestroy()
    {
        SceneView.onSceneGUIDelegate -= OnSceneGui;

        if (!instance)
            instance = GetWindow<ThumbnailEditorWindow>();

        Debug.Log("OnDestroy");
        EditorPrefs.DeleteKey("ThumbPath");
        EditorPrefs.SetString("ThumbData", JsonUtility.ToJson(instance.settings));
        instance = null;

        if (renderCamera != null)
        {
            DestroyImmediate(renderCamera.gameObject);
        }

    }

    //using a camera, save it's Render Target to a Texture2d (passed by reference)
    private static void RenderTargetToTexture(Camera cam, ref Texture2D texture)
    {
        //store current render target
        RenderTexture currentRt = RenderTexture.active;
        //set the active target to the cameras render texture 
        RenderTexture.active = cam.targetTexture;
        //force the camera to render the scene into the target texture buffer
        cam.Render();
        //reads pixels from the active render buffer.
        texture.ReadPixels(new Rect(0, 0, cam.targetTexture.width, cam.targetTexture.height), 0, 0);
        //commit pixel changes
        texture.Apply();
        //restore the previous render target
        RenderTexture.active = currentRt;
    }

}
