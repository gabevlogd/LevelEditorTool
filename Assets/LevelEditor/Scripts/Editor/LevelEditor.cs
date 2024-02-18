# if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using Unity.EditorCoroutines.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class LevelEditor : EditorWindow
{
    [MenuItem("Tools/Level Editor")]
    public static void ShowWindow() => GetWindow<LevelEditor>("Level Editor");

    /// <summary>
    /// Prefabs to spawn to compose the scene
    /// </summary>
    [SerializeField] private GameObject[] _prefabs;
    
    /// <summary>
    /// The current selected prefab from the scene view UI (palette menu)
    /// </summary>
    [SerializeField] private GameObject _selectedObject;

    /// <summary>
    /// Material of the prefab preview visualized in the scene
    /// </summary>
    [SerializeField] private Material _previewMaterial;

    /// <summary>
    /// Mutex for enable/disable the render of the prefab preview
    /// </summary>
    [SerializeField] private bool _enablePrefabPreview;

    /// <summary>
    /// Mutex for enable/disable the preview snapping
    /// </summary>
    [SerializeField] private bool _enablePreviewSnapping;

    /// <summary>
    /// Rect of the palette menu (scene view UI)
    /// </summary>
    private Rect _paletteMenuRect;

    /// <summary>
    /// The prefabs icons visualized in the scene view UI (palette menu)
    /// </summary>
    private Texture[] _prefabIcons;

    /// <summary>
    /// Index of the current prefab selected in the palette menu
    /// </summary>
    private int _selectionGridIndex;

    /// <summary>
    /// Position of the palette menu scroll view
    /// </summary>
    private Vector2 _scrollPos;

    /// <summary>
    /// y component of the spawn position
    /// </summary>
    [SerializeField] private float _spawnPositionHeight;

    private Vector3 _previewPosition;
    private Quaternion _previewRotation;

    private SerializedObject _so;
    private SerializedProperty _previewMaterialP;
    private SerializedProperty _enablePrefabPreviewP;
    private SerializedProperty _enablePreviewSnappingP;
    private SerializedProperty _spawnPositionHeightP;

    private void OnEnable()
    {
        InitializeLevelEditor();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    private void OnGUI()
    {
        if (_so == null) return;
        _so.Update();
        EditorGUILayout.PropertyField(_enablePrefabPreviewP);
        if (_enablePrefabPreview)
        {
            EditorGUILayout.PropertyField(_enablePreviewSnappingP);
            EditorGUILayout.PropertyField(_previewMaterialP);
            EditorGUILayout.PropertyField(_spawnPositionHeightP);
        }

        if (GUILayout.Button("Save Current Level"))
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        if (GUILayout.Button("Make New Level"))
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
            


        if (_so.ApplyModifiedProperties())
        {
            Repaint();
            SceneView.RepaintAll();
        }
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        DrawPaletteMenu(sceneView);
        DrawPrefabPreview(sceneView);

        //Shift + Left-Click for spawn the selected prefab (only if prefab preview is enabled)
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.shift)
        {
            SpawnPrefab();
        }

        //Shift + F for enable/disable the preview of the current selected prefab
        if (Event.current.keyCode == KeyCode.F && Event.current.type == EventType.KeyDown && Event.current.shift)
        {
            _enablePrefabPreview = !_enablePrefabPreview;
            Repaint();
        }

        //Shift + E for enable/disable the preview of the current selected prefab
        if (Event.current.keyCode == KeyCode.E && Event.current.type == EventType.KeyDown && Event.current.shift)
        {
            _enablePreviewSnapping = !_enablePreviewSnapping;
            Repaint();
        }

        //Shift + Scroll Wheel for modifying the altitude where to spawn the selected prefab
        if (Event.current.type == EventType.ScrollWheel && Event.current.shift)
        {
            float scrollDirection = -Event.current.delta.normalized.x;
            _spawnPositionHeight += scrollDirection;
            Repaint();
        }
    }

    #region SCREEN VIEW PALETTE MENU:

    private void DrawPaletteMenu(SceneView sceneView)
    {
        _paletteMenuRect = GUI.Window(0, _paletteMenuRect, DrawPaletteMenuContent, "Rooms Palette");
        UpdatePaletteMenuPosition(sceneView);
    }

    /// <summary>
    /// Manages the dragging of the palette menu
    /// </summary>
    private void UpdatePaletteMenuPosition(SceneView sceneView)
    {
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
        //this vector is the pointer position from the palette menu reference frame
        Vector2 relativePointerPos = Event.current.mousePosition - _paletteMenuRect.position;
        if (relativePointerPos.x <= _paletteMenuRect.width && relativePointerPos.y <= _paletteMenuRect.height && relativePointerPos.x > 0f && relativePointerPos.y > 0f)
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

                //cache the selected index before it can be modified
                int index = _selectionGridIndex;
                int selectionGridColumnCount = 1;
                _selectionGridIndex = GUILayout.SelectionGrid(_selectionGridIndex, _prefabIcons, selectionGridColumnCount);
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

    /// <summary>
    /// If a new prefab was selected by the user in the palette menu (scene view UI) updates the selected prefab 
    /// </summary>
    private void PerformSelectionChange(int index)
    {
        if (index != _selectionGridIndex)
        {
            _selectedObject = _prefabs[_selectionGridIndex];
        }
    }

    private IEnumerator CreatePaletteMenuRect()
    {
        yield return null;
        float rectPosX = SceneView.lastActiveSceneView.camera.pixelWidth * 0.01f;
        float rectPosY = SceneView.lastActiveSceneView.camera.pixelHeight * 0.2f;
        _paletteMenuRect = new Rect(rectPosX, rectPosY, 170, 500);
    }

    #endregion
    #region PREFAB PREVIEW:

    private void DrawPrefabPreview(SceneView sceneView)
    {
        if (_selectedObject == null) return;
        if (!_enablePrefabPreview) return;

        Mesh mesh = _selectedObject.GetComponent<MeshFilter>().sharedMesh;
        if (_previewMaterial)
        {
            if (_previewMaterial.passCount > 1)
                _previewMaterial.SetPass(1);
            else _previewMaterial.SetPass(0);
        }
        else return;

        Graphics.DrawMeshNow(mesh, _previewPosition, _previewRotation);
        UpdatePreviewTransform(sceneView);
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
        if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.CompareTag("Door") && _enablePreviewSnapping)
        {
            TrySnapPreviewPosition(hit, ray);
        }
        else
        {
            _previewPosition = CalculatePreviewPosition(ray);
        }
    }

    private void UpdatePreviewRotation()
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
        {
            _previewRotation *= Quaternion.Euler(0f, 90f, 0f);
        }
    }

    private void TrySnapPreviewPosition(RaycastHit hit, Ray ray)
    {
        hit.collider.TryGetComponent(out BoxCollider doorCollider);
        _selectedObject.TryGetComponent(out Room selectedObject);

        //this vector is a direction that goes from the center of the room
        //present in the scene to its door currently inspected by the mouse pointer
        Vector3 direction = hit.collider.transform.parent.rotation * doorCollider.center.normalized;

        //if doors are aligned
        if (Vector3.Dot(direction, _previewRotation * selectedObject.North) < -0.9f ||
            Vector3.Dot(direction, _previewRotation * selectedObject.East) < -0.9f ||
            Vector3.Dot(direction, _previewRotation * selectedObject.South) < -0.9f ||
            Vector3.Dot(direction, _previewRotation * selectedObject.West) < -0.9f)
        {
            float snapOffset = 15f;
            _previewPosition = hit.collider.transform.parent.position + direction * snapOffset;
        }
        else
        {
            _previewPosition = CalculatePreviewPosition(ray);
        }
    }

    private Vector3 CalculatePreviewPosition(Ray ray)
    {
        // H = Y / D  where H = hypotenuse , Y = minor cathetus ,  D = Cos(angle between Y and H)
        float hypotenuse = ray.origin.y / Vector3.Dot(Vector3.down, ray.direction);
        return ray.origin + ray.direction * hypotenuse + Vector3.up * _spawnPositionHeight;
    }

    #endregion

    private void InitializeLevelEditor()
    {
        LoadPrefabs();

        this.StartCoroutine(CreatePaletteMenuRect());

        _enablePrefabPreview = true;
        _enablePreviewSnapping = true;
        _previewRotation = Quaternion.identity;

        _so = new SerializedObject(this);
        _previewMaterialP = _so.FindProperty("_previewMaterial");
        _enablePrefabPreviewP = _so.FindProperty("_enablePrefabPreview");
        _enablePreviewSnappingP = _so.FindProperty("_enablePreviewSnapping");
        _spawnPositionHeightP = _so.FindProperty("_spawnPositionHeight");

        SceneView.duringSceneGui += DuringSceneGUI;
    }

    private void SpawnPrefab()
    {
        if (!_enablePrefabPreview) return;
        GameObject obj = PrefabUtility.InstantiatePrefab(_selectedObject) as GameObject;
        Undo.RegisterCreatedObjectUndo(obj, "Object Spawn");
        obj.transform.SetPositionAndRotation(_previewPosition, _previewRotation);
    }

    /// <summary>
    /// Loads all prefabs from the project folder
    /// </summary>
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

    
}

#endif
