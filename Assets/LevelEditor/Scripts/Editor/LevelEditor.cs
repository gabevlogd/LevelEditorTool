# if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using Unity.EditorCoroutines.Editor;
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
    /// Index of the current prefab selected in the palette menu
    /// </summary>
    private int _selectionGridIndex;

    /// <summary>
    /// Position of the palette menu scroll view
    /// </summary>
    private Vector2 _scrollPos;

    private bool _showSaveSection;
    private bool _showLoadSection;

    /// <summary>
    /// y component of the spawn position
    /// </summary>
    [SerializeField] private float _spawnPositionHeight;

    [SerializeField] private string _saveFolderName;
    [SerializeField] private string _saveFileName;
    [SerializeField] private string _loadFileName;

    private Vector3 _previewPosition;
    private Quaternion _previewRotation;

    private SerializedObject _so;
    private SerializedProperty _previewMaterialP;
    private SerializedProperty _enablePrefabPreviewP;
    private SerializedProperty _enablePreviewSnappingP;
    private SerializedProperty _spawnPositionHeightP;
    private SerializedProperty _saveFolderNameP;
    private SerializedProperty _saveFileNameP;
    private SerializedProperty _loadFileNameP;

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
        //preview settings
        EditorGUILayout.PropertyField(_enablePrefabPreviewP);
        if (_enablePrefabPreview)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_enablePreviewSnappingP);
            EditorGUILayout.PropertyField(_previewMaterialP);
            EditorGUILayout.PropertyField(_spawnPositionHeightP);
            EditorGUI.indentLevel--;
        }
        //end preview settings

        GUILayout.Space(10f);

        //save settings
        _showSaveSection = EditorGUILayout.Foldout(_showSaveSection, "Save");
        if (_showSaveSection)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_saveFolderNameP);
            EditorGUILayout.PropertyField(_saveFileNameP);
            if (GUILayout.Button("Save Current Level"))
                SaveCurrentLevel();
            EditorGUI.indentLevel--;
        }
        //end save settings

        //load settings
        _showLoadSection = EditorGUILayout.Foldout(_showLoadSection, "Load");
        if (_showLoadSection)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_loadFileNameP);
            if (GUILayout.Button("Load Level"))
                LoadLevel(_loadFileName);
            EditorGUI.indentLevel--;
        }
        //end load settings

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
                _selectionGridIndex = GUILayout.SelectionGrid(_selectionGridIndex, GetPrefabIcons(), selectionGridColumnCount); ;
                PerformSelectionChange(index);

                GUILayout.EndScrollView();
            }

            GUILayout.Space(10f);

            if (GUILayout.Button("Refresh List"))
            {
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
        if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.CompareTag("SnapPoint") && _enablePreviewSnapping)
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

    private void TrySnapPreviewPosition(RaycastHit snapPoint, Ray ray)
    {
        _selectedObject.TryGetComponent(out SnappableObject snappableObject);

        //this vector is a direction that goes from the center of the snappableObject
        //present in the scene to its snap point (door) currently inspected by the mouse pointer
        Vector3 direction = snapPoint.transform.parent.rotation * snapPoint.transform.localPosition.normalized;

        //(if doors are aligned)
        if (CanSnap(direction, snappableObject))
        {
            _previewPosition = snapPoint.transform.position;
        }
        else
        {
            _previewPosition = CalculatePreviewPosition(ray);
        }
    }

    private bool CanSnap(Vector3 snapDirection, SnappableObject snappableObj)
    {
        foreach(Transform trs in snappableObj.SnapPoint)
        {
            if (Vector3.Dot(snapDirection, _previewRotation * trs.localPosition.normalized) < -0.9f)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Calculate where to locate the preview in the 3D world space based on the camera and mouse pointer position
    /// </summary>
    /// <param name="ray">ray that starts from the camera position and go through the mouse pointer</param>
    /// <returns>Calculated position</returns>
    private Vector3 CalculatePreviewPosition(Ray ray)
    {
        // H = Y / D  where H = hypotenuse , Y = cathetus ,  D = Cos(angle between Y and H)
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
        _saveFolderNameP = _so.FindProperty("_saveFolderName");
        _saveFileNameP = _so.FindProperty("_saveFileName");
        _loadFileNameP = _so.FindProperty("_loadFileName");

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
        _prefabs = Resources.LoadAll<GameObject>("SnappableObject/");
        _selectionGridIndex = 0;
        _selectedObject = _prefabs.Length > 0 ? _prefabs[0] : null;
    }

    private Texture[] GetPrefabIcons()
    {
        if (_prefabs == null || _prefabs.Length == 0) return null;
        Texture[] icons = new Texture[_prefabs.Length];
        for (int i = 0; i < _prefabs.Length; i++)
        {
            icons[i] = AssetPreview.GetAssetPreview(_prefabs[i]);
        }
        return icons;
    }

    /// <summary>
    /// Save the current level into a json file
    /// </summary>
    private void SaveCurrentLevel()
    {
        LevelSaveData saveData = new LevelSaveData();
        foreach (SnappableObject snappableObj in FindObjectsOfType<SnappableObject>())
        {
            saveData.IDs.Add(snappableObj.ObjID);
            saveData.Positions.Add(snappableObj.transform.position);
            saveData.Rotations.Add(snappableObj.transform.rotation);
        }
        JsonSaveHandler.Save(_saveFolderName, _saveFileName, saveData);
    }


    /// <summary>
    /// Load a level from the passed levelFileName (json file)
    /// </summary>
    private void LoadLevel(string levelFileName)
    {
        LevelSaveData loadData;
        loadData = JsonSaveHandler.Load<LevelSaveData>(_loadFileName);
        if (loadData == null)
        {
            Debug.LogError($"Level {levelFileName} loading failed");
            return;
        }

        int index = 0;
        foreach(int id in loadData.IDs)
        {
            GameObject obj = PrefabUtility.InstantiatePrefab(GetPrefabByID(id)) as GameObject;
            obj.transform.SetPositionAndRotation(loadData.Positions[index], loadData.Rotations[index]);
            index++;
        }
    }

    private GameObject GetPrefabByID(int id)
    {
        foreach(GameObject prefab in _prefabs)
        {
            if (prefab.GetComponent<SnappableObject>().ObjID == id)
                return prefab;
        }
        Debug.LogError($"Prefab {id} not found");
        return null;
    }

    
}

#endif
