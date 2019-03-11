using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.PostProcessing;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public partial class ThumbnailEditorWindow : EditorWindow
{
    private static ThumbnailEditorWindow instance;
    public Camera renderCamera;

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

        public PostProcessProfile postProfile;
        public PostProcessLayer.Antialiasing AAMode;

    }
    public ThumbnailEditorSettings settings;
    
    public List<PrefabObject> _prefabObjects = new List<PrefabObject>();

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

        EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("postProfile"));
        EditorGUILayout.PropertyField(settingsProp.FindPropertyRelative("AAMode"));

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
                    var jsonPath = Path.Combine(settings.path, meshObject.prefab.name + ".thumb.json");
                    var imagePath = Path.Combine(settings.path, meshObject.prefab.name + ".png");
                    File.WriteAllText(jsonPath, JsonUtility.ToJson(meshObject,true));
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
                                prefab = go,
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
            cam.targetTexture = new RenderTexture(settings.thumbnailSize, settings.thumbnailSize, 32) {antiAliasing = 8, filterMode = FilterMode.Trilinear, anisoLevel = 16};


        var DefaultRenderData = GetCameraTransformTuple(settings.orbitYaw, settings.orbitPitch, settings.orbitDistance, settings.orbitHeight);
        (Vector3 pos, Quaternion rot, Vector3 forward, Vector3 target) activeData;

        for (var moIndex = 0; moIndex < objects.Count; moIndex++)
        {
            PrefabObject po = objects[moIndex];
            //if it has custom render settings, set them
            if (po.hasCustomSettings)
            {
                activeData = GetCameraTransformTuple(
                    po.orbitYaw, 
                    po.orbitPitch,
                    po.orbitDistance,
                    po.orbitHeight);
            }
            else
                activeData = DefaultRenderData;


            Matrix4x4 mtx = Matrix4x4.TRS(activeData.pos, activeData.rot, Vector3.one).inverse;

            RecursiveDrawGameObject(mtx, po.prefab, settings.renderLayer, cam);

            var texture = new Texture2D(settings.thumbnailSize, settings.thumbnailSize);

            RenderTargetToTexture(cam, ref texture);

            po.thumbnail = texture;

            objects[moIndex] = po;
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
                var rt = new RenderTexture(settings.thumbnailSize, settings.thumbnailSize, 32) {antiAliasing = 8};

                var go = new GameObject("thumbnail Camera");
                go.layer = settings.renderLayer;
                returnCam = go.AddComponent<Camera>();

                var postLayer = go.AddComponent<PostProcessLayer>();
                postLayer.antialiasingMode = settings.AAMode;
                postLayer.volumeLayer = 1 << settings.renderLayer;
                var postVolume = go.AddComponent<PostProcessVolume>();
                postVolume.isGlobal = true;
                postVolume.profile = settings.postProfile;

                returnCam.transform.position = Vector3.zero;
                returnCam.transform.rotation = Quaternion.identity;
                returnCam.transform.localScale = Vector3.one;
                returnCam.clearFlags = CameraClearFlags.Color;
                returnCam.backgroundColor = Color.clear;
                returnCam.targetTexture = rt;

                returnCam.cullingMask = settings.renderLayer;
            }
        }

        renderCamera = returnCam;

        //surely we have one by now
        return returnCam;
    }

    [MenuItem("Cookie Jar/thumbnail Renderer")]
    public static void ShowWindow()
    {
        //get one if it already exists
        instance = GetWindow<ThumbnailEditorWindow>();
        //delegate to the scenview render method, so we can draw gizmos in the scene using Editor GUI Handles
        SceneView.onSceneGUIDelegate -= instance.OnSceneGui; //yuck...why unity whyyyy!?!
        SceneView.onSceneGUIDelegate += instance.OnSceneGui;
        //callback so that we can draw a preview of the active mesh in the sceneview camera
        Camera.onPreCull -= instance.DrawActiveObjectScenePreview;
        Camera.onPreCull += instance.DrawActiveObjectScenePreview;
        instance.activeObject = null;

        //load setting data, it's stashed as a really long string encodeded using json
        //easier than making individual prefs for each settings property. much easier to save also.
        if (EditorPrefs.HasKey("ThumbData"))
            instance.settings = JsonUtility.FromJson<ThumbnailEditorSettings>(EditorPrefs.GetString("ThumbData"));
        instance.Show();
    }

    private void DrawActiveObjectScenePreview(Camera cam)
    {
        if (activeObject != null)
        {
            RecursiveDrawGameObject(activeObject.prefab.transform.worldToLocalMatrix, activeObject.prefab,0, cam);
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

    //really hacky way of managing the active focused object. TODO: do better
    private void OnPropClicked(SerializedProperty pObject)
    {
        PrefabObject focused = null;
        foreach (PrefabObject t in _prefabObjects)
        {
            //matching based on prefab gameobject reference
            bool equal = t.prefab == pObject.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
            if (equal)
                focused = t;
            t.focused = equal; //sets matching to true, non to false
        }
        activeObject = focused;

        Repaint(); //because we're changing the state of 'focused' on all the objects, used by the propertyDrawer
    }

    private void OnSceneGui(SceneView sceneView)
    {
        //geting camera data based on if the active object exists and has custom settings
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

        //if there is no active object, just draw a 1x1x1m ghost cube at the center
        if (activeObject == null)
        {
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            Handles.CubeHandleCap(0, Vector3.zero, Quaternion.identity, 1, EventType.Repaint);
        }

        //drawing the forward vector of the camera
        Handles.color = new Color(0f, 0f, 1f, 0.5f);
        Handles.DrawLine(camPos, targetPos);

        //showing the verticle angle of view
        var upFOVVector = Quaternion.AngleAxis(settings.cameraFOV / 2, binormal) * camForward;
        var downFOVVector = Quaternion.AngleAxis(settings.cameraFOV / 2, -binormal) * camForward;

        //camera local y
        Handles.color = new Color(0f, 1f, 0f, 0.5f);
        Handles.DrawLine(camPos, camPos + normal * .4f);
        //camera local x
        Handles.color = new Color(1f, 0f, 0f, 0.25f);
        Handles.DrawLine(camPos, camPos + binormal * .4f);
        //camera FOV
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

        //drawing a little cube to represent the camera
        Handles.color = new Color(0f, 0.82f, 1f, 0.5f);
        Handles.matrix = Matrix4x4.TRS(camPos, camRot, Vector3.one);
        Handles.DrawWireCube(Vector3.zero, new Vector3(.25f, .25f, .5f));

        sceneView.Repaint();
    }

    private void OnDestroy()
    {
        SceneView.onSceneGUIDelegate -= OnSceneGui;
        Camera.onPreCull -= instance.DrawActiveObjectScenePreview;

        activeObject = null;

        if (!instance)
            instance = GetWindow<ThumbnailEditorWindow>();

        Debug.Log("OnDestroy");
        EditorPrefs.DeleteKey("ThumbPath");
        EditorPrefs.SetString("ThumbData", JsonUtility.ToJson(instance.settings, true));
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


    public void RecursiveDrawGameObject(Matrix4x4 rootWorldToLocalTransform, GameObject target, int renderLayer, Camera cam)
    {

        foreach (Transform child in target.transform)
        {
            RecursiveDrawGameObject(rootWorldToLocalTransform, child.gameObject, renderLayer, cam);
        }
        var mf = target.GetComponent<MeshFilter>();
        var mr = target.GetComponent<MeshRenderer>();

        if (mf == null || mr == null)
            return;

        var mesh = mf.sharedMesh;
        var mats = mr.sharedMaterials;

        var relativeXform = rootWorldToLocalTransform * target.transform.localToWorldMatrix;

        for (var i = 0; i < mats.Length && i < mesh.subMeshCount; i++)
        {
            var material = mats[i];
            if (material != null && i < mesh.subMeshCount)
                Graphics.DrawMesh(mesh, relativeXform, material, renderLayer, cam);
        }

    }

}
