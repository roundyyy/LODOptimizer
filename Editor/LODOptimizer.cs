using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;
using System.IO;

// Define a class to hold texture and color information
public class TextureData
{
    public Texture2D texture;
    public bool applyColor;
    public Color color;

    public TextureData(Texture2D tex, bool colorFlag, Color col)
    {
        texture = tex;
        applyColor = colorFlag;
        color = col;
    }
}

// Define a class to hold combined diffuse and normal texture information
public class CombinedTextureData
{
    public Texture2D diffuseTexture;
    public Texture2D normalTexture;
    public bool applyColor;
    public Color color;

    public CombinedTextureData(Texture2D diffuseTex, Texture2D normalTex, bool colorFlag, Color col)
    {
        diffuseTexture = diffuseTex;
        normalTexture = normalTex;
        applyColor = colorFlag;
        color = col;
    }
}

public class LODOptimizer : EditorWindow
{
    // List to store selected GameObjects
    private ReorderableList reorderableList;
    private List<GameObject> selectedObjects = new List<GameObject>();

    // Settings
    private int selectedAtlasSizeIndex = 3; // Default to 2048
    private readonly int[] atlasSizes = new int[] { 256, 512, 1024, 2048, 4096 };
    private string atlasMaterialName = "LODOptimized_Material";
    private Shader selectedShader;

    private bool applyMaterialColor = true; // Toggle to apply material colors
    private bool enableNormalMapPacking = false; // New toggle
    private string normalAtlasMaterialName = "LODOptimized_NormalMaterial"; // Name for normal atlas material

    private int padding = 1;

    // Folder Structure
    private string rootFolderName = "LODOptimizerFolder";
    private string meshesFolderName = "Meshes";
    private string texturesFolderName = "Textures";
    private string materialsFolderName = "Materials";

    // Progress Indicator
    private bool isProcessing = false;

    // Warnings
    private List<string> uvWarnings = new List<string>();

    // Shader List
    private List<Shader> availableShaders = new List<Shader>();
    private string[] shaderNames;
    private int selectedShaderIndex = -1;

    // Scroll position for the ReorderableList
    private Vector2 listScrollPos;

    [MenuItem("Tools/Roundy/LOD Optimizer")]
    public static void ShowWindow()
    {
        GetWindow<LODOptimizer>("LOD Optimizer v0.1");
    }

    private void OnEnable()
    {
        // Initialize the ReorderableList
        reorderableList = new ReorderableList(selectedObjects, typeof(GameObject), true, true, true, true);

        reorderableList.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, "Selected Objects");
        };

        reorderableList.drawElementCallback = DrawReorderableListElement;

        // Define the element height callback
        reorderableList.elementHeightCallback = (int index) =>
        {
            return EditorGUIUtility.singleLineHeight + 4; // Single row height
        };

        // Custom onAddCallback to add a null slot
        reorderableList.onAddCallback = (ReorderableList list) =>
        {
            selectedObjects.Add(null);
            Debug.Log("LOD Optimizer: Added a new slot. Drag a GameObject into the slot.");
        };

        reorderableList.onRemoveCallback = (ReorderableList list) =>
        {
            if (EditorUtility.DisplayDialog("Confirm Removal", "Are you sure you want to remove the selected object?", "Yes", "No"))
            {
                selectedObjects.RemoveAt(list.index);
                Debug.Log("LOD Optimizer: Removed selected object from the list.");
                Repaint();
            }
        };

        // Populate availableShaders
        availableShaders = Resources.FindObjectsOfTypeAll<Shader>().ToList();
        shaderNames = availableShaders.Select(s => s.name).ToArray();

        // Set default shader index
        if (selectedShader != null)
        {
            selectedShaderIndex = availableShaders.IndexOf(selectedShader);
        }
        else
        {
            // Default to Standard shader
            selectedShaderIndex = availableShaders.FindIndex(s => s.name == "Standard");
            if (selectedShaderIndex == -1 && availableShaders.Count > 0)
                selectedShaderIndex = 0;
        }
    }

    private void DrawReorderableListElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        if (index < selectedObjects.Count)
        {
            GameObject obj = selectedObjects[index];
            rect.y += 2;

            // Define column widths
            float objectColumnWidth = rect.width * 0.5f;
            float statsColumnWidth = rect.width * 0.45f; // Adjust as needed
            float padding = rect.width * 0.05f;

            // Define positions
            Rect objectFieldRect = new Rect(rect.x, rect.y, objectColumnWidth, EditorGUIUtility.singleLineHeight);
            Rect pingButtonRect = new Rect(rect.x + objectColumnWidth + padding, rect.y, 18, EditorGUIUtility.singleLineHeight);
            Rect statsRect = new Rect(rect.x + objectColumnWidth + padding + 20, rect.y, statsColumnWidth - 20, EditorGUIUtility.singleLineHeight);

            // Display the GameObject field
            selectedObjects[index] = (GameObject)EditorGUI.ObjectField(
                objectFieldRect,
                obj,
                typeof(GameObject),
                true
            );

            // Add a button to ping the object in the hierarchy
            if (GUI.Button(pingButtonRect, "P"))
            {
                if (obj != null)
                {
                    Selection.activeGameObject = obj;
                    EditorGUIUtility.PingObject(obj);
                }
            }

            // Display stats beside the GameObject field
            if (obj != null)
            {
                LODGroup lodGroup = obj.GetComponent<LODGroup>();
                string lodInfo = lodGroup != null
                    ? $"LOD Levels: {lodGroup.GetLODs().Length}"
                    : "No LOD Group";

                string vertexInfo = "";
                MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    int vertexCount = meshFilter.sharedMesh.vertexCount;
                    vertexInfo = $"Vertices: {vertexCount}";
                }

                // Combine stats into a single line separated by a pipe or any delimiter
                string combinedStats = lodInfo;
                if (!string.IsNullOrEmpty(vertexInfo))
                {
                    combinedStats += " | " + vertexInfo;
                }

                EditorGUI.LabelField(
                    statsRect,
                    combinedStats
                );
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("LOD Optimizer Settings", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Selected Objects"))
        {
            AddSelectedObjects();
        }
        if (GUILayout.Button("Clear List"))
        {
            selectedObjects.Clear();
            Debug.Log("LOD Optimizer: Cleared selected objects list.");
            Repaint();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Limit the ReorderableList display to 10 items and add a scrollbar
        int maxVisibleItems = 10;
        float elementHeight = reorderableList.elementHeight;
        float headerHeight = reorderableList.headerHeight;
        float listHeight = headerHeight + elementHeight * maxVisibleItems;

        listScrollPos = EditorGUILayout.BeginScrollView(listScrollPos, GUILayout.Height(listHeight));
        reorderableList.DoLayoutList();
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);

        // Atlas Size Dropdown
        GUILayout.BeginHorizontal();
        GUILayout.Label("Max Atlas Size", GUILayout.Width(100));
        selectedAtlasSizeIndex = EditorGUILayout.Popup(selectedAtlasSizeIndex, atlasSizes.Select(size => size.ToString()).ToArray(), GUILayout.Width(100));
        GUILayout.EndHorizontal();

        // Material Name Input
        GUILayout.BeginHorizontal();
        GUILayout.Label("Material Name", GUILayout.Width(100));
        atlasMaterialName = EditorGUILayout.TextField(atlasMaterialName);
        GUILayout.EndHorizontal();

        // Shader Selection Dropdown
        GUILayout.BeginHorizontal();
        GUILayout.Label("Target Shader", GUILayout.Width(100));
        if (availableShaders.Count > 0)
        {
            selectedShaderIndex = EditorGUILayout.Popup(selectedShaderIndex, shaderNames, GUILayout.Width(200));
            selectedShader = availableShaders[selectedShaderIndex];
        }
        else
        {
            EditorGUILayout.LabelField("No shaders found.", GUILayout.Width(200));
        }
        GUILayout.EndHorizontal();

        // Refresh Shaders Button
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Shaders"))
        {
            availableShaders = Resources.FindObjectsOfTypeAll<Shader>().ToList();
            shaderNames = availableShaders.Select(s => s.name).ToArray();
            selectedShaderIndex = availableShaders.FindIndex(s => s.name == "Standard");
            if (selectedShaderIndex == -1 && availableShaders.Count > 0)
                selectedShaderIndex = 0;
            Repaint();
        }
        GUILayout.EndHorizontal();

        // Apply Material Color Checkbox
        GUILayout.BeginHorizontal();
        applyMaterialColor = EditorGUILayout.Toggle("Apply Material Color", applyMaterialColor, GUILayout.Width(200));
        GUILayout.EndHorizontal();

        // Padding Input
        GUILayout.BeginHorizontal();
        GUILayout.Label("Padding (px)", GUILayout.Width(100));
        padding = EditorGUILayout.IntField(padding, GUILayout.Width(50));
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Normal Map Packing Toggle
        GUILayout.BeginHorizontal();
        enableNormalMapPacking = EditorGUILayout.Toggle("Enable Normal Map Packing", enableNormalMapPacking, GUILayout.Width(200));
        GUILayout.EndHorizontal();

        // Optional: Input for Normal Atlas Material Name
        if (enableNormalMapPacking)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Normal Material Name", GUILayout.Width(150));
            normalAtlasMaterialName = EditorGUILayout.TextField(normalAtlasMaterialName);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Optimize LOD") && !isProcessing)
        {
            if (selectedObjects.Count == 0 || selectedObjects.All(obj => obj == null))
            {
                EditorUtility.DisplayDialog("LOD Optimizer", "No valid objects selected.", "OK");
                Debug.LogWarning("LOD Optimizer: No valid GameObjects selected.");
                return;
            }

            isProcessing = true;
            OptimizeLOD();
            isProcessing = false;
        }

        GUILayout.Space(10);

        // Display Warnings
        if (uvWarnings.Count > 0)
        {
            EditorGUILayout.HelpBox("Some objects have UVs outside the 0-1 range. They have been adjusted to prevent tiling issues.", MessageType.Warning);
            foreach (var warning in uvWarnings)
            {
                EditorGUILayout.LabelField(warning);
            }
        }

        if (isProcessing)
        {
            GUILayout.Label("Processing...", EditorStyles.boldLabel);
        }
    }

    private void AddSelectedObjects()
    {
        foreach (var obj in Selection.gameObjects)
        {
            if (!selectedObjects.Contains(obj))
            {
                selectedObjects.Add(obj);
                Debug.Log($"LOD Optimizer: Added '{obj.name}' to the optimization list.");
            }
        }
        Repaint();
    }

    /// <summary>
    /// Generates a unique asset path by appending a number if the asset already exists.
    /// </summary>
    /// <param name="basePath">The base path for the asset.</param>
    /// <param name="baseName">The base name for the asset.</param>
    /// <param name="extension">The file extension (e.g., ".mat").</param>
    /// <returns>A unique asset path.</returns>
    private string GetUniqueAssetPath(string basePath, string baseName, string extension)
    {
        string assetPath = Path.Combine(basePath, baseName + extension);
        int counter = 1;
        while (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
        {
            assetPath = Path.Combine(basePath, $"{baseName}_{counter}{extension}");
            counter++;
        }
        return assetPath;
    }

    private void OptimizeLOD()
    {
        Debug.Log("LOD Optimizer: OptimizeLOD method invoked.");

        try
        {
            // Filter out null GameObjects
            List<GameObject> validObjects = selectedObjects.Where(obj => obj != null).ToList();
            Debug.Log($"LOD Optimizer: {validObjects.Count} valid GameObjects to process.");

            if (validObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("LOD Optimizer", "No valid objects selected.", "OK");
                Debug.LogWarning("LOD Optimizer: No valid GameObjects to process.");
                return;
            }

            // Step 1: Setup folders
            string rootPath = Path.Combine("Assets", rootFolderName);
            string meshesPath = Path.Combine(rootPath, meshesFolderName);
            string texturesPath = Path.Combine(rootPath, texturesFolderName);
            string materialsPath = Path.Combine(rootPath, materialsFolderName);
            string normalTexturesPath = Path.Combine(texturesPath, "Normals"); // New folder for normal maps

            CreateFolderIfNotExists(rootPath);
            CreateFolderIfNotExists(meshesPath);
            CreateFolderIfNotExists(texturesPath);
            CreateFolderIfNotExists(materialsPath);
            if (enableNormalMapPacking)
            {
                CreateFolderIfNotExists(normalTexturesPath);
            }

            Debug.Log($"LOD Optimizer: Folder structure ensured at '{rootPath}'.");

            // Step 2: Collect highest LOD meshes and their textures
            List<MeshRenderer> meshRenderers = new List<MeshRenderer>();

            // Dictionary to store unique diffuse textures and colors
            Dictionary<(Texture2D, Color), CombinedTextureData> uniqueTexturesAndColors = new Dictionary<(Texture2D, Color), CombinedTextureData>();

            // Combined texture data list
            List<CombinedTextureData> combinedTextureDataList = new List<CombinedTextureData>();

            // Dictionary to map each MeshRenderer to its CombinedTextureData
            Dictionary<MeshRenderer, CombinedTextureData> rendererToTextureData = new Dictionary<MeshRenderer, CombinedTextureData>();


            foreach (var obj in validObjects)
            {
                LODGroup lodGroup = obj.GetComponent<LODGroup>();
                MeshRenderer[] renderersToProcess;

                if (lodGroup != null)
                {
                    LOD[] lods = lodGroup.GetLODs();
                    if (lods.Length > 0)
                    {
                        // Assuming the last LOD is the highest detail
                        renderersToProcess = lods[lods.Length - 1].renderers
                            .OfType<MeshRenderer>()
                            .ToArray();
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    // If no LODGroup, consider the MeshRenderer directly
                    MeshRenderer singleRenderer = obj.GetComponent<MeshRenderer>();
                    if (singleRenderer != null)
                    {
                        renderersToProcess = new MeshRenderer[] { singleRenderer };
                    }
                    else
                    {
                        continue;
                    }
                }

                foreach (var renderer in renderersToProcess)
                {
                    meshRenderers.Add(renderer);
                    Material mat = renderer.sharedMaterial;

                    if (mat != null)
                    {
                        Texture2D diffuseTex = mat.mainTexture as Texture2D;
                        if (diffuseTex == null)
                        {
                            diffuseTex = Texture2D.whiteTexture;
                            Debug.LogWarning($"LOD Optimizer: No _MainTex found for '{obj.name}'. Using white texture.");
                        }

                        Color color = Color.white;
                        bool applyColor = false;

                        if (applyMaterialColor && mat.HasProperty("_Color"))
                        {
                            color = mat.color;
                            applyColor = color != Color.white;
                        }

                        Texture2D normalTex = null;
                        if (enableNormalMapPacking)
                        {
                            if (mat.HasProperty("_BumpMap"))
                            {
                                normalTex = mat.GetTexture("_BumpMap") as Texture2D;
                            }
                            else if (mat.HasProperty("_NormalMap"))
                            {
                                normalTex = mat.GetTexture("_NormalMap") as Texture2D;
                            }

                            if (normalTex == null)
                            {
                                normalTex = GenerateFlatNormalMap(texSize: 128);
                                Debug.Log($"LOD Optimizer: Generated flat normal map for '{obj.name}'.");
                            }
                        }

                        EnsureTextureIsReadable(diffuseTex);
                        if (normalTex != null) EnsureTextureIsReadable(normalTex);

                        // Use both texture and color as the key
                        var key = (diffuseTex, color);

                        CombinedTextureData textureData;
                        if (!uniqueTexturesAndColors.TryGetValue(key, out textureData))
                        {
                            textureData = new CombinedTextureData(diffuseTex, normalTex, applyColor, color);
                            uniqueTexturesAndColors.Add(key, textureData);
                            combinedTextureDataList.Add(textureData);

                            if (applyColor)
                            {
                                Debug.Log($"LOD Optimizer: Added new texture-color combination for '{obj.name}'. Color: {color}");
                            }
                        }
                        else if (normalTex != textureData.normalTexture)
                        {
                            Debug.LogWarning($"LOD Optimizer: Different normal map found for same diffuse texture and color in '{obj.name}'. Using the first encountered normal map.");
                        }
                        // Map this renderer to its texture data
                        rendererToTextureData[renderer] = textureData;
                    }
                }
            }

            if (meshRenderers.Count == 0)
            {
                EditorUtility.DisplayDialog("LOD Optimizer", "No MeshRenderers found in selected objects.", "OK");
                Debug.LogWarning("LOD Optimizer: No MeshRenderers found in selected objects.");
                return;
            }

            Debug.Log($"LOD Optimizer: Collected {meshRenderers.Count} MeshRenderers and {combinedTextureDataList.Count} unique texture-color combinations.");

            // Step 3: Check for UV out-of-bounds and collect warnings
            foreach (var renderer in meshRenderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                Vector2[] uvs = filter.sharedMesh.uv;
                foreach (var uv in uvs)
                {
                    if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                    {
                        uvWarnings.Add($"Object '{renderer.gameObject.name}' has UVs outside the 0-1 range.");
                        break; // Only add one warning per object
                    }
                }
            }

            // Step 4: Calculate optimal grid based on combined list
            Debug.Log("LOD Optimizer: Calculating optimal grid for texture atlases.");
            (int rows, int columns, int texSize) = CalculateOptimalGrid(combinedTextureDataList.Count, atlasSizes[selectedAtlasSizeIndex], combinedTextureDataList.Select(ctd => new TextureData(ctd.diffuseTexture, ctd.applyColor, ctd.color)).ToList());
            if (rows == 0 || columns == 0 || texSize == 0)
            {
                EditorUtility.DisplayDialog("LOD Optimizer", "Failed to calculate optimal grid for atlas packing.", "OK");
                Debug.LogError("LOD Optimizer: Failed to calculate optimal grid for atlas packing.");
                return;
            }

            // Step 5: Create diffuse atlas
            Debug.Log("LOD Optimizer: Creating diffuse atlas texture.");
            Texture2D diffuseAtlas = CreateDiffuseAtlas(combinedTextureDataList, atlasSizes[selectedAtlasSizeIndex], texturesPath, atlasMaterialName, padding, rows, columns, texSize);

            if (diffuseAtlas == null)
            {
                EditorUtility.DisplayDialog("LOD Optimizer", "Failed to create diffuse atlas texture.", "OK");
                Debug.LogError("LOD Optimizer: Failed to create diffuse atlas texture.");
                return;
            }

            // Step 6: Create normal atlas
            Texture2D normalAtlas = null;



            if (enableNormalMapPacking && combinedTextureDataList.Count > 0)
            {
                Debug.Log("LOD Optimizer: Creating normal atlas texture using the same grid.");
                normalAtlas = CreateNormalAtlas(combinedTextureDataList, atlasSizes[selectedAtlasSizeIndex], normalTexturesPath, normalAtlasMaterialName, padding, rows, columns, texSize);

                if (normalAtlas == null)
                {
                    EditorUtility.DisplayDialog("LOD Optimizer", "Failed to create normal atlas texture.", "OK");
                    Debug.LogError("LOD Optimizer: Failed to create normal atlas texture.");
                    return;
                }
            }

            // Step 8: Create diffuse material
            Debug.Log("LOD Optimizer: Creating diffuse atlas material.");
            Material atlasMaterial = new Material(selectedShader != null ? selectedShader : Shader.Find("Standard"));
            atlasMaterial.name = atlasMaterialName;
            atlasMaterial.mainTexture = diffuseAtlas;
            if (atlasMaterial.HasProperty("_Glossiness"))
            {
                atlasMaterial.SetFloat("_Glossiness", 0f);
            }

            string materialPath = GetUniqueAssetPath(materialsPath, atlasMaterial.name, ".mat");
            AssetDatabase.CreateAsset(atlasMaterial, materialPath);
            Debug.Log($"LOD Optimizer: Created material asset at '{materialPath}'.");

            // Step 9: Assign atlas materials and remap UVs
            Debug.Log("LOD Optimizer: Assigning atlas materials and remapping UVs.");
            Undo.RegisterCompleteObjectUndo(meshRenderers.Select(r => r.gameObject).ToArray(), "LOD Optimizer");

            foreach (var renderer in meshRenderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    Debug.LogWarning($"LOD Optimizer: MeshFilter or Mesh is missing on '{renderer.gameObject.name}'. Skipping.");
                    continue;
                }

                // Duplicate mesh
                Mesh originalMesh = filter.sharedMesh;
                Mesh newMesh = Instantiate(originalMesh);
                newMesh.name = originalMesh.name + "_LODOptimized";

                // Save the new mesh asset
                string meshAssetName = newMesh.name;
                string meshPath = GetUniqueAssetPath(meshesPath, meshAssetName, ".asset");
                AssetDatabase.CreateAsset(newMesh, meshPath);
                Undo.RegisterCreatedObjectUndo(newMesh, "LOD Optimizer Create Mesh");
                Debug.Log($"LOD Optimizer: Created mesh asset at '{meshPath}'.");

                // Find the correct texture data for this renderer
                if (!rendererToTextureData.TryGetValue(renderer, out CombinedTextureData ctd))
                {
                    Debug.LogError($"LOD Optimizer: No texture data found for renderer on '{renderer.gameObject.name}'. Skipping UV remapping.");
                    continue;
                }

                // Find the index of this texture data in the combined list
                int texIndex = combinedTextureDataList.IndexOf(ctd);
                if (texIndex == -1)
                {
                    Debug.LogError($"LOD Optimizer: Texture data not found in combined list for '{renderer.gameObject.name}'. Skipping UV remapping.");
                    continue;
                }

                // Calculate UV offsets based on the grid
                int row = texIndex / columns;
                int col = texIndex % columns;

                float uvOffsetX = (float)col / columns;
                float uvOffsetY = (float)row / rows;
                float uvScaleX = 1f / columns;
                float uvScaleY = 1f / rows;

                Vector2[] originalUV = originalMesh.uv;
                Vector2[] newUV = new Vector2[originalUV.Length];

                bool hasTiling = false;
                foreach (var uv in originalUV)
                {
                    if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                    {
                        hasTiling = true;
                        break;
                    }
                }

                if (hasTiling)
                {
                    Debug.LogWarning($"LOD Optimizer: Mesh '{renderer.gameObject.name}' has UVs outside 0-1 range. Capping UVs to prevent tiling issues.");
                }

                for (int v = 0; v < originalUV.Length; v++)
                {
                    Vector2 uv = originalUV[v];
                    // Cap UVs to [0,1] if tiling is detected
                    if (hasTiling)
                    {
                        uv.x = Mathf.Clamp(uv.x, 0f, 1f);
                        uv.y = Mathf.Clamp(uv.y, 0f, 1f);
                    }

                    newUV[v] = new Vector2(uv.x * uvScaleX + uvOffsetX, uv.y * uvScaleY + uvOffsetY);
                }
                newMesh.uv = newUV;

                newMesh.RecalculateNormals();

                // Assign new mesh and material
                filter.sharedMesh = newMesh;
                renderer.sharedMaterial = atlasMaterial;

                Debug.Log($"LOD Optimizer: Updated '{renderer.gameObject.name}' with new mesh and atlas material. Texture index: {texIndex}");

                // Assign normal atlas material if enabled
                if (enableNormalMapPacking)
                {
                    // Assuming the shader uses _BumpMap for normals
                    renderer.sharedMaterial.SetTexture("_BumpMap", normalAtlas);
                    renderer.sharedMaterial.EnableKeyword("_NORMALMAP");
                    Debug.Log($"LOD Optimizer: Assigned normal atlas to '{renderer.gameObject.name}'.");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("LOD Optimizer: Optimization process completed successfully.");
            EditorUtility.DisplayDialog("LOD Optimizer", "LOD optimization completed successfully.", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"LOD Optimizer: An error occurred during optimization: {ex.Message}");
            EditorUtility.DisplayDialog("LOD Optimizer", "An error occurred during optimization. See the console for details.", "OK");
        }
    }


    private void CreateFolderIfNotExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path);
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                CreateFolderIfNotExists(parent);
            }
            string newFolderPath = AssetDatabase.CreateFolder(parent, folderName);
            if (!string.IsNullOrEmpty(newFolderPath))
            {
                Debug.Log($"LOD Optimizer: Created folder '{newFolderPath}'.");
            }
            else
            {
                Debug.LogWarning($"LOD Optimizer: Failed to create folder '{path}'.");
            }
        }
    }

    private void EnsureTextureIsReadable(Texture2D texture)
    {
        if (texture == null) return;

        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            Debug.Log($"LOD Optimizer: Made texture '{texture.name}' readable.");
        }
    }
    /// <summary>
    /// Calculates the optimal number of rows and columns for the atlas grid to minimize unused space.
    /// Returns the number of rows, columns, and the texture size.
    /// </summary>
    /// <param name="texCount">Total number of textures.</param>
    /// <param name="maxAtlasSize">Maximum size of the atlas.</param>
    /// <param name="textureDataList">List of TextureData objects.</param>
    /// <returns>A tuple containing the number of rows, columns, and texture size.</returns>
    private (int rows, int columns, int texSize) CalculateOptimalGrid(int texCount, int maxAtlasSize, List<TextureData> textureDataList)
    {
        int optimalRows = 0;
        int optimalColumns = 0;
        int minimalWaste = int.MaxValue;
        int optimalTexSize = 0;

        // Determine the maximum texture size
        int maxTexWidth = textureDataList.Max(td => td.texture.width);
        int maxTexHeight = textureDataList.Max(td => td.texture.height);
        int texSize = Mathf.NextPowerOfTwo(Mathf.Max(maxTexWidth, maxTexHeight));

        // Start with the largest possible texture size and reduce if necessary
        while (texSize >= 16) // Prevent textures from being too small
        {
            // Try different column counts to find the best fit
            for (int columns = 1; columns <= texCount; columns++)
            {
                int rows = Mathf.CeilToInt((float)texCount / columns);

                int atlasWidth = columns * (texSize + padding * 2);
                int atlasHeight = rows * (texSize + padding * 2);

                if (atlasWidth > maxAtlasSize || atlasHeight > maxAtlasSize)
                    continue;

                int waste = (columns * rows) - texCount;
                if (waste < minimalWaste)
                {
                    minimalWaste = waste;
                    optimalRows = rows;
                    optimalColumns = columns;
                    optimalTexSize = texSize;

                    if (waste == 0)
                        break; // Perfect fit
                }
            }

            if (optimalRows > 0 && optimalColumns > 0)
                break; // Found a suitable grid

            // Reduce texture size and try again
            texSize /= 2;
        }

        if (optimalRows == 0 || optimalColumns == 0 || optimalTexSize == 0)
        {
            Debug.LogError("LOD Optimizer: Unable to fit textures into atlas within the maximum atlas size.");
            return (0, 0, 0);
        }

        Debug.Log($"LOD Optimizer: Optimal grid calculated - Rows: {optimalRows}, Columns: {optimalColumns}, Texture Size: {optimalTexSize}");

        return (optimalRows, optimalColumns, optimalTexSize);
    }

    private Texture2D CreateDiffuseAtlas(List<CombinedTextureData> combinedTextureDataList, int maxSize, string texturesPath, string atlasName, int padding, int rows, int columns, int texSize)
    {
        if (combinedTextureDataList.Count == 0)
        {
            Debug.LogError("LOD Optimizer: No diffuse textures to atlas.");
            return null;
        }

        int atlasWidth = columns * (texSize + padding * 2);
        int atlasHeight = rows * (texSize + padding * 2);

        Debug.Log($"LOD Optimizer: Creating diffuse atlas with {columns} columns and {rows} rows. Atlas size: {atlasWidth}x{atlasHeight}.");

        Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
        atlas.name = atlasName + "_DiffuseAtlas";

        Color[] clearColors = new Color[atlasWidth * atlasHeight];
        for (int i = 0; i < clearColors.Length; i++)
        {
            clearColors[i] = Color.white;
        }
        atlas.SetPixels(clearColors);

        for (int i = 0; i < combinedTextureDataList.Count; i++)
        {
            CombinedTextureData ctd = combinedTextureDataList[i];
            Texture2D diffuseTex = ctd.diffuseTexture;

            EnsureTextureIsReadable(diffuseTex);

            Texture2D resizedTex = ResizeTexture(diffuseTex, texSize, texSize, false);

            int row = i / columns;
            int col = i % columns;

            int x = col * (texSize + padding * 2) + padding;
            int y = row * (texSize + padding * 2) + padding;

            Color[] diffusePixels = resizedTex.GetPixels();

            if (ctd.applyColor)
            {
                for (int p = 0; p < diffusePixels.Length; p++)
                {
                    diffusePixels[p] = diffusePixels[p] * ctd.color;
                }
            }

            atlas.SetPixels(x, y, texSize, texSize, diffusePixels);

            Debug.Log($"LOD Optimizer: Placed diffuse texture '{diffuseTex.name}' at ({x}, {y}) with padding.");
        }

        atlas.Apply();

        string atlasPath = GetUniqueAssetPath(texturesPath, atlas.name, ".png");
        byte[] atlasBytes = atlas.EncodeToPNG();
        File.WriteAllBytes(atlasPath, atlasBytes);
        AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
        Texture2D importedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

        TextureImporter atlasImporter = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
        if (atlasImporter != null)
        {
            atlasImporter.isReadable = true;
            atlasImporter.textureCompression = TextureImporterCompression.Uncompressed;
            atlasImporter.sRGBTexture = true; // Use sRGB for diffuse textures
            atlasImporter.mipmapEnabled = true;
            atlasImporter.filterMode = FilterMode.Bilinear;
            atlasImporter.SaveAndReimport();
        }

        return importedAtlas;
    }


    private Texture2D CreateNormalAtlas(List<CombinedTextureData> combinedTextureDataList, int maxSize, string normalTexturesPath, string atlasName, int padding, int rows, int columns, int texSize)
    {
        if (combinedTextureDataList.Count == 0)
        {
            Debug.LogError("LOD Optimizer: No normal textures to atlas.");
            return null;
        }

        int atlasWidth = columns * (texSize + padding * 2);
        int atlasHeight = rows * (texSize + padding * 2);

        Debug.Log($"LOD Optimizer: Creating normal atlas with {columns} columns and {rows} rows. Atlas size: {atlasWidth}x{atlasHeight}.");

        Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
        atlas.name = atlasName + "_NormalAtlas";

        Color[] clearColors = new Color[atlasWidth * atlasHeight];
        for (int i = 0; i < clearColors.Length; i++)
        {
            clearColors[i] = new Color(0.5f, 0.5f, 1f, 1f); // Default normal pointing up
        }
        atlas.SetPixels(clearColors);

        for (int i = 0; i < combinedTextureDataList.Count; i++)
        {
            CombinedTextureData ctd = combinedTextureDataList[i];
            Texture2D normalTex = ctd.normalTexture;

            EnsureTextureIsReadable(normalTex);

            Texture2D resizedTex = ResizeTexture(normalTex, texSize, texSize, true);

            int row = i / columns;
            int col = i % columns;

            int x = col * (texSize + padding * 2) + padding;
            int y = row * (texSize + padding * 2) + padding;

            Color[] normalPixels = resizedTex.GetPixels();
            atlas.SetPixels(x, y, texSize, texSize, normalPixels);

            Debug.Log($"LOD Optimizer: Placed normal texture '{normalTex.name}' at ({x}, {y}) with padding.");
        }

        atlas.Apply();

        string atlasPath = GetUniqueAssetPath(normalTexturesPath, atlas.name, ".png");
        byte[] atlasBytes = atlas.EncodeToPNG();
        File.WriteAllBytes(atlasPath, atlasBytes);
        AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
        Texture2D importedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

        TextureImporter atlasImporter = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
        if (atlasImporter != null)
        {
            atlasImporter.isReadable = true;
            atlasImporter.textureCompression = TextureImporterCompression.Uncompressed;
            atlasImporter.textureType = TextureImporterType.NormalMap;
            atlasImporter.wrapMode = TextureWrapMode.Clamp;
            atlasImporter.filterMode = FilterMode.Bilinear;
            atlasImporter.mipmapEnabled = false;
            atlasImporter.sRGBTexture = false; // Ensure linear color space for normal maps
            atlasImporter.SaveAndReimport();
        }

        return importedAtlas;
    }
    private Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight, bool isNormalMap)
    {
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(newWidth, newHeight, RenderTextureFormat.ARGB32, 0);
        descriptor.sRGB = !isNormalMap; // Use linear for normal maps, sRGB for others
        RenderTexture rt = RenderTexture.GetTemporary(descriptor);
        rt.filterMode = FilterMode.Bilinear;
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);
        Texture2D resized = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false, !isNormalMap);
        resized.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        resized.Apply(false);
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return resized;
    }

    /// <summary>
    /// Generates a flat normal map (points upwards).
    /// </summary>
    /// <param name="texSize">Size of the normal map.</param>
    /// <returns>A flat normal map texture.</returns>
    private Texture2D GenerateFlatNormalMap(int texSize)
    {
        Texture2D flatNormal = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        Color32 flatColor = new Color32(128, 128, 255, 255); // Represents a flat normal

        Color32[] pixels = new Color32[texSize * texSize];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = flatColor;
        }

        flatNormal.SetPixels32(pixels);
        flatNormal.Apply();
        flatNormal.name = "FlatNormal";

        // Save the flat normal texture
        string flatNormalPath = Path.Combine("Assets", rootFolderName, texturesFolderName, "Normals", "FlatNormal.png");
        byte[] bytes = flatNormal.EncodeToPNG();
        File.WriteAllBytes(flatNormalPath, bytes);
        AssetDatabase.ImportAsset(flatNormalPath, ImportAssetOptions.ForceUpdate);
        Texture2D importedFlatNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(flatNormalPath);

        // Ensure it's marked as a normal map
        TextureImporter importer = AssetImporter.GetAtPath(flatNormalPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
            Debug.Log($"LOD Optimizer: Saved flat normal map at '{flatNormalPath}'.");
        }

        return importedFlatNormal;
    }
}
