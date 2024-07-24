using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Editor
{
    public class AssetUsageChecker : EditorWindow
    {
        #region Declaration
    
        private readonly List<Object> _assetsToCheck = new List<Object>();
        private string _scenesFolderPath = "Assets/Scenes"; // Default starting folder
        private string _resultText = "";
        private Vector2 _scrollPositionAssets = Vector2.zero;
        private Vector2 _scrollPositionResults = Vector2.zero;
    
        #endregion
        
        
        #region MenuController
    
        [MenuItem("Tools/Asset Usage Checker")]
        public static void ShowWindow()
        {
            GetWindow<AssetUsageChecker>("Asset Usage Checker").Show();
        }

        #endregion
    
        
        #region GUI Method

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Searches for usage of specified assets in scenes within a selected folder.", MessageType.Info);

            // Assets to Check field
            EditorGUILayout.LabelField("Assets to Check");

            // Allow Drag and Drop to add assets
            var evt = Event.current;
            var dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag & Drop Assets Here");
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        break;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        foreach (Object draggedObject in DragAndDrop.objectReferences)
                        {
                            _assetsToCheck.Add(draggedObject);
                        }
                    }
                    Event.current.Use();
                    break;
            }

            // Display the list of assets
            _scrollPositionAssets = EditorGUILayout.BeginScrollView(_scrollPositionAssets, GUILayout.Height(150));
            for (var i = 0; i < _assetsToCheck.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _assetsToCheck[i] = EditorGUILayout.ObjectField($"Asset {i + 1}", _assetsToCheck[i], typeof(Object), false);
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    _assetsToCheck.RemoveAt(i);
                    i--; // Adjust index due to removal
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            // Add/Remove buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Asset"))
            {
                _assetsToCheck.Add(null);
            }
            if (GUILayout.Button("Remove Asset") && _assetsToCheck.Count > 0)
            {
                _assetsToCheck.RemoveAt(_assetsToCheck.Count - 1);
            }
            if (GUILayout.Button("Remove All Assets") && _assetsToCheck.Count > 0)
            {
                _assetsToCheck.Clear();
            }
            EditorGUILayout.EndHorizontal();

            // Scenes Folder Path field with Select Folder button
            EditorGUILayout.BeginHorizontal();
            _scenesFolderPath = EditorGUILayout.TextField("Scenes Folder Path", _scenesFolderPath);
            if (GUILayout.Button("Select Folder", GUILayout.Width(100)))
            {
                _scenesFolderPath = EditorUtility.OpenFolderPanel("Select Folder with Scenes", _scenesFolderPath, "");
                if (!string.IsNullOrEmpty(_scenesFolderPath) && _scenesFolderPath.StartsWith(Application.dataPath))
                {
                    _scenesFolderPath = "Assets" + _scenesFolderPath.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Check Asset Usage"))
            {
                _resultText = _assetsToCheck.Count > 0 ? CheckAssetUsage() : "Please assign at least one asset to check.";
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Result");
            // Scroll area for results
            _scrollPositionResults = EditorGUILayout.BeginScrollView(_scrollPositionResults, GUILayout.Height(150));
            EditorGUILayout.LabelField(_resultText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        
            // Clear results button
            if (GUILayout.Button("Clear Results"))
            {
                _resultText = "";
            }
        }
    
        #endregion
        
        
        #region Helper Methods
        private string CheckAssetUsage()
        {
            var assetInScenes = new Dictionary<string, HashSet<string>>();
            var result = "";

            foreach (Object assetToCheck in _assetsToCheck)
            {
                if (assetToCheck == null) continue;
                var assetPath = AssetDatabase.GetAssetPath(assetToCheck);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    switch (assetToCheck)
                    {
                        case Texture2D when AssetDatabase.GetMainAssetTypeAtPath(assetPath) == typeof(Texture2D):
                        {
                            // Handle sprite sheets
                            var sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();
                            foreach (var sprite in sprites)
                            {
                                var spritePath = AssetDatabase.GetAssetPath(sprite);
                                assetInScenes[spritePath] = new HashSet<string>();
                            }

                            break;
                        }
                        case GameObject when PrefabUtility.GetPrefabAssetType(assetToCheck) == PrefabAssetType.Regular:
                            // Handle prefabs
                            assetInScenes[assetPath] = new HashSet<string>();
                            break;
                        default:
                            assetInScenes[assetPath] = new HashSet<string>();
                            break;
                    }
                }
            }

            if (string.IsNullOrEmpty(_scenesFolderPath) || !Directory.Exists(_scenesFolderPath))
            {
                return "Invalid scenes folder path.";
            }

            var sceneFiles = Directory.GetFiles(_scenesFolderPath, "*.unity", SearchOption.AllDirectories);
            if (sceneFiles.Length == 0)
            {
                return "No scenes found in the specified folder.";
            }

            var totalScenes = sceneFiles.Length;
            var currentSceneIndex = 0;

            foreach (var sceneFile in sceneFiles)
            {
                var scenePath = sceneFile.Replace(Application.dataPath, "Assets");

                // Load the scene
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                var rootObjects = scene.GetRootGameObjects();

                foreach (var rootObject in rootObjects)
                {
                    foreach (var assetPath in assetInScenes.Keys)
                    {
                        if (IsAssetInGameObject(rootObject, assetPath))
                        {
                            assetInScenes[assetPath].Add(scene.name);
                            result += $"Asset \"{Path.GetFileName(assetPath)}\" found in scene: \"{scene.name}\", in GameObject: \"{GetGameObjectHierarchy(rootObject)}\"\n";
                            break; // Found the asset in this GameObject, no need to keep looking in this GameObject
                        }
                    }
                }

                EditorSceneManager.CloseScene(scene, true);
                EditorUtility.DisplayProgressBar("Checking Asset Usage", $"Checking scene {currentSceneIndex + 1} of {totalScenes}", (float)currentSceneIndex / totalScenes);
                currentSceneIndex++;
            }

            EditorUtility.ClearProgressBar();

            foreach (var assetPath in assetInScenes.Keys)
            {
                if (assetInScenes[assetPath].Count > 0)
                {
                    result += $"Asset \"{Path.GetFileName(assetPath)}\" was found in the following scenes: {string.Join(", ", assetInScenes[assetPath])}\n";
                }
                else
                {
                    result += $"Asset \"{Path.GetFileName(assetPath)}\" was not found in any scenes.\n";
                }
            }

            return result;
        }

        private bool IsAssetInGameObject(GameObject go, string assetPath)
        {
            var components = go.GetComponentsInChildren<Component>(true);
            foreach (var component in components)
            {
                if (component == null) continue;

                var so = new SerializedObject(component);
                var sp = so.GetIterator();
                while (sp.NextVisible(true))
                {
                    if (sp.propertyType == SerializedPropertyType.ObjectReference && sp.objectReferenceValue != null)
                    {
                        var spAssetPath = AssetDatabase.GetAssetPath(sp.objectReferenceValue);
                        if (assetPath.Equals(spAssetPath))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private string GetGameObjectHierarchy(GameObject go)
        {
            var hierarchy = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                hierarchy = parent.name + "/" + hierarchy;
                parent = parent.parent;
            }
            return hierarchy;
        }
    
        #endregion
    }
}





