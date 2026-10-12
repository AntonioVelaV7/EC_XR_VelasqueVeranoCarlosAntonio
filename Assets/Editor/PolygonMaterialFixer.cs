using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PolygonCityPack.Editor
{
    [InitializeOnLoad]
    public static class PolygonMaterialFixer
    {
        private const string PREF_KEY = "PolygonCityMaterialsFixed_v1";

        static PolygonMaterialFixer()
        {
            // Auto-run once on load if not already executed
            EditorApplication.delayCall += () =>
            {
                if (!EditorPrefs.GetBool(PREF_KEY, false))
                {
                    Debug.Log("[PolygonMaterialFixer] Ejecutando reparación automática inicial...");
                    FixAllMaterials();
                    EditorPrefs.SetBool(PREF_KEY, true);
                }
            };
        }

        [MenuItem("Tools/POLYGON City/Reparar Materiales (URP Lit)")]
        [MenuItem("Assets/POLYGON City/Reparar Materiales (URP Lit)")]
        public static void ForceFixAllMaterials()
        {
            FixAllMaterials();
            EditorPrefs.SetBool(PREF_KEY, true);
        }

        public static void FixAllMaterials()
        {
            // 1. Localizar Shader URP Lit
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                // Fallback por GUID del shader URP Lit estándar
                string shaderPath = AssetDatabase.GUIDToAssetPath("933532a4fcc9baf4fa0491de14d08ed7");
                if (!string.IsNullOrEmpty(shaderPath))
                {
                    urpLit = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                }
            }

            if (urpLit == null)
            {
                Debug.LogError("[PolygonMaterialFixer] ERROR CRÍTICO: No se encontró el shader 'Universal Render Pipeline/Lit'. ¿Está URP instalado y activo?");
                return;
            }

            Debug.Log($"[PolygonMaterialFixer] Shader encontrado: {urpLit.name}. Iniciando reparación...");

            // 2. Indexar todas las texturas de Assets/POLYGON city pack/Meshes,textures/
            string texturesRoot = "Assets/POLYGON city pack/Meshes,textures";
            Dictionary<string, Texture2D> textureDict = BuildTextureIndex(texturesRoot);
            Debug.Log($"[PolygonMaterialFixer] {textureDict.Count} texturas indexadas en '{texturesRoot}'.");

            // 3. Buscar todos los materiales en Assets/POLYGON city pack
            string[] searchFolders = new string[]
            {
                "Assets/POLYGON city pack/Materials",
                "Assets/POLYGON city pack/Meshes,textures"
            };

            string[] matGuids = AssetDatabase.FindAssets("t:Material", searchFolders);
            int totalProcessed = 0;
            int shaderChangedCount = 0;
            int textureAssignedCount = 0;
            int whiteColorCount = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < matGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(matGuids[i]);
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null) continue;

                    // Omitir skyboxes procedurales como Day.mat y Night.mat
                    if (mat.shader != null && mat.shader.name.StartsWith("Skybox", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    totalProcessed++;
                    bool isModified = false;

                    // Leer propiedades previas antes de cambiar de shader
                    Texture originalTex = mat.GetTexture("_MainTex");
                    Vector2 texScale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
                    Vector2 texOffset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
                    Color originalColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

                    // Cambiar shader a URP Lit si es necesario
                    if (mat.shader != urpLit)
                    {
                        mat.shader = urpLit;
                        shaderChangedCount++;
                        isModified = true;
                    }

                    // Determinar textura
                    Texture targetTexture = originalTex;
                    if (targetTexture == null && mat.HasProperty("_BaseMap"))
                    {
                        targetTexture = mat.GetTexture("_BaseMap");
                    }

                    string lowerPath = path.ToLowerInvariant();
                    bool isColorPalette = lowerPath.Contains("colors_buildings") ||
                                          lowerPath.Contains("traffic light") ||
                                          lowerPath.Contains("color_windows");

                    // Si no tiene textura asignada, buscar en el índice de texturas por nombre del material (solo si no es paleta de color)
                    if (targetTexture == null && !isColorPalette)
                    {
                        targetTexture = FindMatchingTexture(mat.name, textureDict);
                        if (targetTexture != null)
                        {
                            textureAssignedCount++;
                            isModified = true;
                        }
                    }

                    // Asignar textura y escala/offset
                    if (targetTexture != null)
                    {
                        if (mat.HasProperty("_BaseMap"))
                        {
                            mat.SetTexture("_BaseMap", targetTexture);
                            mat.SetTextureScale("_BaseMap", texScale);
                            mat.SetTextureOffset("_BaseMap", texOffset);
                        }
                        if (mat.HasProperty("_BaseColorMap"))
                        {
                            mat.SetTexture("_BaseColorMap", targetTexture);
                            mat.SetTextureScale("_BaseColorMap", texScale);
                            mat.SetTextureOffset("_BaseColorMap", texOffset);
                        }
                        if (mat.HasProperty("_MainTex"))
                        {
                            mat.SetTexture("_MainTex", targetTexture);
                        }

                        // Asegurar color base en blanco puro (1, 1, 1, 1) para texturas
                        if (mat.HasProperty("_BaseColor"))
                        {
                            mat.SetColor("_BaseColor", Color.white);
                        }
                        if (mat.HasProperty("_Color"))
                        {
                            mat.SetColor("_Color", Color.white);
                        }
                        whiteColorCount++;
                        isModified = true;
                    }
                    else
                    {
                        // Material sin textura (colores sólidos como Colors_buildings, traffic light, windows)
                        // Si la carpeta es Colors_buildings / traffic light / color_windows, preservar su color original
                        if (isColorPalette && originalColor != Color.white)
                        {
                            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", originalColor);
                            if (mat.HasProperty("_Color")) mat.SetColor("_Color", originalColor);
                        }
                        else
                        {
                            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                            whiteColorCount++;
                        }
                        isModified = true;
                    }

                    if (isModified)
                    {
                        EditorUtility.SetDirty(mat);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // 4. Guardar y refrescar assets
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            Debug.Log($"[PolygonMaterialFixer] PROCESO COMPLETADO:" +
                      $"\n- Total materiales procesados: {totalProcessed}" +
                      $"\n- Shaders cambiados a URP/Lit: {shaderChangedCount}" +
                      $"\n- Texturas vinculadas: {textureAssignedCount}" +
                      $"\n- Materiales con _BaseColor blanco: {whiteColorCount}" +
                      $"\nAssets guardados con éxito.");
        }

        private static Dictionary<string, Texture2D> BuildTextureIndex(string folder)
        {
            var dict = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            if (!AssetDatabase.IsValidFolder(folder)) return dict;

            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new string[] { folder });
            foreach (string guid in texGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;

                string rawName = Path.GetFileNameWithoutExtension(path);
                string cleanName = NormalizeString(rawName);

                if (!dict.ContainsKey(cleanName))
                {
                    dict[cleanName] = tex;
                }

                // También indexar sin la palabra "texture" si existe
                string noTex = cleanName.Replace("texture", "");
                if (!string.IsNullOrEmpty(noTex) && !dict.ContainsKey(noTex))
                {
                    dict[noTex] = tex;
                }
            }

            return dict;
        }

        private static Texture2D FindMatchingTexture(string matName, Dictionary<string, Texture2D> textureDict)
        {
            string cleanMat = NormalizeString(matName);
            if (string.IsNullOrEmpty(cleanMat)) return null;

            // 1. Coincidencia exacta
            if (textureDict.TryGetValue(cleanMat, out Texture2D tex)) return tex;

            // 2. Coincidencia sin "texture"
            string noTex = cleanMat.Replace("texture", "");
            if (!string.IsNullOrEmpty(noTex) && textureDict.TryGetValue(noTex, out tex)) return tex;

            // 3. Coincidencia con "texture" al final
            string withTex = cleanMat + "texture";
            if (textureDict.TryGetValue(withTex, out tex)) return tex;

            // 4. Búsqueda por contención
            foreach (var kvp in textureDict)
            {
                if (kvp.Key.Length > 3 && (cleanMat.Contains(kvp.Key) || kvp.Key.Contains(cleanMat)))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        private static string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in input.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
