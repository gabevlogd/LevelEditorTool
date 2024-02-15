# if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using Unity.EditorCoroutines.Editor;
using UnityEngine;

public class LevelEditor : EditorWindow
{
    [MenuItem("Tools/Level Editor")]
    public static void ShowWindow() => GetWindow<LevelEditor>("Level Editor");

    [SerializeField]
    private GameObject[] _prefabs;
    private Texture[] _prefabIcons;
    [SerializeField]
    private GameObject _selectedObject;
    [SerializeField]
    private Material _previewMaterial;

    private int _selectionGridIndex;
    private Vector2 _scrollPos;
    private Vector3 _previewPosition;
    private Quaternion _previewRotation;
    [SerializeField]
    private bool _enablePrefabPreview;
    private Rect _paletteMenuRect;
    
    private SerializedObject _so;
    private SerializedProperty _previewMaterialP;
    private SerializedProperty _enablePrefabPreviewP;

    private GameObject _hitDetectionPlane;

    private void OnEnable()
    {
        this.StartCoroutine(InitializeLevelEditor());
    }

    private void OnDisable()
    {
        _so = null;
        DestroyImmediate(_hitDetectionPlane);
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    private void OnGUI()
    {
        if (_so == null) return;
        _so.Update();
        EditorGUILayout.PropertyField(_previewMaterialP);
        EditorGUILayout.PropertyField(_enablePrefabPreviewP);
        if (_so.ApplyModifiedProperties())
        {
            Repaint();
            SceneView.RepaintAll();
        }
    }

    private IEnumerator InitializeLevelEditor()
    {
        yield return null;
        GenerateHitDetectionPlane();
        LoadPrefabs();

        float sceneViewWidth = SceneView.lastActiveSceneView.camera.pixelWidth * 0.01f;
        float sceneViewHeight = SceneView.lastActiveSceneView.camera.pixelHeight * 0.2f;
        _paletteMenuRect = new Rect(sceneViewWidth, sceneViewHeight, 170, 500);

        _enablePrefabPreview = true;
        _previewRotation = Quaternion.identity;
        _so = new SerializedObject(this);
        _previewMaterialP = _so.FindProperty("_previewMaterial");
        _enablePrefabPreviewP = _so.FindProperty("_enablePrefabPreview");
        SceneView.duringSceneGui += DuringSceneGUI;
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        DrawPaletteMenu(sceneView);
        DrawPrefabPreview();
        UpdatePreviewTransform(sceneView);
        UpdateDetectionPlanePosition(sceneView);

        bool isHoldingShift = (Event.current.modifiers & EventModifiers.Shift) != 0;

        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && isHoldingShift)
        {
            SpawnPrefab();
        }

        if (Event.current.keyCode == KeyCode.F && Event.current.type == EventType.KeyDown && isHoldingShift)
        {
            _enablePrefabPreview = !_enablePrefabPreview;
            Repaint();
        }

        
    }

    private void SpawnPrefab()
    {
        GameObject obj = PrefabUtility.InstantiatePrefab(_selectedObject) as GameObject;
        Undo.RegisterCreatedObjectUndo(obj, "Object Spawn");
        obj.transform.SetPositionAndRotation(_previewPosition, _previewRotation);
    }

    private void DrawPaletteMenu(SceneView sceneView)
    {
        _paletteMenuRect = GUI.Window(0, _paletteMenuRect, DrawPaletteMenuContent, "Rooms Palette");
        if (Event.current.type == EventType.MouseDrag && Event.current.button == 0 && IsMouseOverPaletteMenu())
        {
            _paletteMenuRect.position += Event.current.delta;
            _paletteMenuRect.x = Mathf.Clamp(_paletteMenuRect.x, 0f, sceneView.camera.pixelWidth - _paletteMenuRect.width);
            _paletteMenuRect.y = Mathf.Clamp(_paletteMenuRect.y, 0f, sceneView.camera.pixelHeight - _paletteMenuRect.height);
            sceneView.Repaint();
        }
    }

    private bool IsMouseOverPaletteMenu()
    {
        Vector2 tmp = Event.current.mousePosition - _paletteMenuRect.position;
        if (tmp.x <= _paletteMenuRect.width && tmp.y <= _paletteMenuRect.height && tmp.x > 0f && tmp.y > 0f)
            return true;
        return false;
    }

    private void DrawPaletteMenuContent(int id)
    {
        using (new GUILayout.VerticalScope())
        {
            using (new GUILayout.HorizontalScope())
            {
                _scrollPos = GUILayout.BeginScrollView(_scrollPos);

                int index = _selectionGridIndex;
                _selectionGridIndex = GUILayout.SelectionGrid(_selectionGridIndex, _prefabIcons, 1);
                PerformSelectionChange(index);

                GUILayout.EndScrollView();
            }

            GUILayout.Space(10f);

            if (GUILayout.Button("Refresh List"))
            {
                Debug.Log("Refresh");
                LoadPrefabs();
            }
        }

        GUI.DragWindow();

    }

    private void LoadPrefabs()
    {
        _prefabs = Resources.LoadAll<GameObject>("Rooms/");
        _selectionGridIndex = 0;
        _prefabIcons = new Texture[_prefabs.Length];
        for (int i = 0; i < _prefabs.Length; i++)
        {
            _prefabIcons[i] = AssetPreview.GetAssetPreview(_prefabs[i]);
        }
        _selectedObject = _prefabs.Length > 0 ? _prefabs[0] : null;
    }

    private void PerformSelectionChange(int index)
    {
        if (index != _selectionGridIndex)
        {
            _selectedObject = _prefabs[_selectionGridIndex];
        }
    }

    private void DrawPrefabPreview()
    {
        if (_selectedObject == null) return;
        if (!_enablePrefabPreview) return;

        
        MeshFilter[] filters = _selectedObject.GetComponentsInChildren<MeshFilter>();
        foreach(MeshFilter filter in filters)
        {
            Mesh mesh = filter.sharedMesh;
            Material mat = filter.GetComponent<MeshRenderer>().sharedMaterial;
            //MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
            //materialPropertyBlock.SetColor("_Color", Color.white);
            //filter.GetComponent<MeshRenderer>().SetPropertyBlock(materialPropertyBlock);
            mat.SetPass(1);
            //filter.GetComponent<MeshRenderer>().sharedMaterial.SetPass(1);
            Graphics.DrawMeshNow(mesh, _previewPosition, _previewRotation);
        }
    }

    private void UpdatePreviewTransform(SceneView sceneView)
    {
        UpdatePreviewPosition();
        UpdatePreviewRotation();
        if (Event.current.type == EventType.MouseMove)
        {
            sceneView.Repaint();
        }
    }

    private void UpdatePreviewPosition()
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        LayerMask snapLayer = 1 << 6;
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if(hit.collider.includeLayers == snapLayer)
            {
                //Debug.Log("Snap");
                TrySnapPreviewPosition(hit);
            }
            else
            {
                _previewPosition = hit.point;
            }
        }
        else
        {
            _previewPosition = ray.origin + (ray.direction * ray.origin.y);
        }
    }

    private void UpdatePreviewRotation()
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
        {
            _previewRotation *= Quaternion.Euler(0f, 90f, 0f);
            //Event.current.Use();
        }
    }

    private void TrySnapPreviewPosition(RaycastHit hit)
    {
        hit.collider.TryGetComponent(out BoxCollider collider);
        _selectedObject.TryGetComponent(out Room room);
        Vector3 tmp = hit.collider.transform.parent.rotation * collider.center.normalized;
        if (AreDoorsAligned(tmp, room))
        {
            _previewPosition = hit.collider.transform.parent.position + tmp * 15f;
        }
        else
        {
            _previewPosition = hit.point;
        }
    }

    private bool AreDoorsAligned(Vector3 a, Room room)
    {
        if (Vector3.Dot(a, _previewRotation * room.North) < -0.9f)
        {
            return true;
        }
        else if (Vector3.Dot(a, _previewRotation * room.East) < -0.9f)
        {
            return true;
        }
        else if (Vector3.Dot(a, _previewRotation * room.South) < -0.9f)
        {
            return true;
        }
        else if (Vector3.Dot(a, _previewRotation * room.West) < -0.9f)
        {
            return true;
        }

        return false;
    }

    private void GenerateHitDetectionPlane()
    {
        float x = SceneView.lastActiveSceneView.camera.transform.position.x;
        float z = SceneView.lastActiveSceneView.camera.transform.position.z;
        Vector3 pos = new Vector3(x, -0.05f, z);
        _hitDetectionPlane = new GameObject("HIT_DETECTOR");
        _hitDetectionPlane.transform.position = pos;
        BoxCollider collider = _hitDetectionPlane.AddComponent<BoxCollider>();
        collider.size = new Vector3(100000f, 0.1f, 100000f);
        _hitDetectionPlane.hideFlags = HideFlags.HideAndDontSave;
    }

    private void UpdateDetectionPlanePosition(SceneView sceneView)
    {
        _hitDetectionPlane.transform.position = new Vector3(sceneView.camera.transform.position.x, 0f, sceneView.camera.transform.position.z);
    }
}

#endif
