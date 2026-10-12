using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XRSimulation.Editor
{
    public static class ExteriorCityBuilder
    {
        private const string RootName = "Entorno_Ciudad_Exterior";

        [MenuItem("XR Tools/Construir Entorno Ciudad Exterior")]
        public static string BuildExteriorCity()
        {
            var scene = SceneManager.GetActiveScene();
            Undo.SetCurrentGroupName("Construir Entorno Ciudad Exterior URP");
            int undoGroup = Undo.GetCurrentGroup();

            // 1. Limpieza de versión previa de Entorno_Ciudad_Exterior si existía
            GameObject existingRoot = GameObject.Find(RootName);
            if (existingRoot != null)
            {
                Undo.DestroyObjectImmediate(existingRoot);
                Debug.Log($"[ExteriorCityBuilder] Versión anterior de '{RootName}' eliminada limpiamente.");
            }

            // 2. Crear GameObject raíz en (0, 0, 0)
            GameObject root = new GameObject(RootName);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(root, "Crear " + RootName);

            // 3. Crear los tres grupos organizativos obligatorios
            GameObject gViasAceras = CreateSubGroup(root, "01_Vias_y_Aceras");
            GameObject gEdificios = CreateSubGroup(root, "02_Edificios_Perimetro");
            GameObject gMobiliario = CreateSubGroup(root, "03_Mobiliario_Urbano_Props");

            int totalFloors = 0;
            int totalBuildings = 0;
            int totalProps = 0;

            // =========================================================================
            // SECCIÓN 1: 01_Vias_y_Aceras (Acera Oficina, Calzada y Acera Opuesta)
            // =========================================================================
            string pSideway1 = "Assets/POLYGON city pack/Prefabs/Floor/Sideway 1 prefab.prefab";
            string pSideway4 = "Assets/POLYGON city pack/Prefabs/Floor/Sideway 4 prefab.prefab";
            string pSideway5 = "Assets/POLYGON city pack/Prefabs/Floor/Sideway 5 prefab.prefab";
            string pStreet4 = "Assets/POLYGON city pack/Prefabs/Floor/Street 4 Prefab.prefab";
            string pStreetCrosswalk = "Assets/POLYGON city pack/Prefabs/Floor/Street 8 Prefab.prefab";

            // 1.1 Acera Frontal de la Oficina (Z: 4.0 a 8.0, X: -20 a +20)
            // Conectada directamente a la puerta de salida en Z=4.0, X=-3.5
            for (float x = -20f; x <= 20f; x += 2f)
            {
                // Módulo pegado a la pared exterior (Z = 5.0m, cubre 4.0 a 6.0)
                var swInner = SpawnPrefab(pSideway5, gViasAceras.transform, new Vector3(x, 0f, 5f), Quaternion.identity);
                ConfigureFloorModule(swInner, 2f, 2f);
                totalFloors++;

                // Módulo exterior con bordillo hacia la calle (Z = 7.0m, cubre 6.0 a 8.0)
                var swOuter = SpawnPrefab(pSideway1, gViasAceras.transform, new Vector3(x, 0f, 7f), Quaternion.identity);
                ConfigureFloorModule(swOuter, 2f, 2f);
                totalFloors++;
            }

            // 1.2 Aceras Laterales que rodean la oficina (Este y Oeste)
            for (float z = 3f; z >= -6f; z -= 2f)
            {
                // Acera Oeste (X = -7m y -9m)
                var swW1 = SpawnPrefab(pSideway4, gViasAceras.transform, new Vector3(-7f, 0f, z), Quaternion.identity);
                ConfigureFloorModule(swW1, 2f, 2f);
                var swW2 = SpawnPrefab(pSideway4, gViasAceras.transform, new Vector3(-9f, 0f, z), Quaternion.identity);
                ConfigureFloorModule(swW2, 2f, 2f);
                totalFloors += 2;

                // Acera Este (X = +7m y +9m)
                var swE1 = SpawnPrefab(pSideway4, gViasAceras.transform, new Vector3(7f, 0f, z), Quaternion.identity);
                ConfigureFloorModule(swE1, 2f, 2f);
                var swE2 = SpawnPrefab(pSideway4, gViasAceras.transform, new Vector3(9f, 0f, z), Quaternion.identity);
                ConfigureFloorModule(swE2, 2f, 2f);
                totalFloors += 2;
            }

            // 1.3 Calzada de Asfalto (Z: 8.0 a 16.0, ancho 8m = 2-3 carriles)
            // Módulos Street 4 (4x4m) y Cruce Peatonal Street 8 a lo largo de X: -18 a +18
            for (float x = -18f; x <= 18f; x += 4f)
            {
                // Cruce peatonal (paso de cebra) frente a la puerta de salida exterior (X=-2, abarca de X=-4 a 0)
                bool isCrosswalk = Mathf.Approximately(x, -2f);
                string prefabToSpawn = isCrosswalk ? pStreetCrosswalk : pStreet4;

                // Carril Sur (Z = 10.0m, cubre Z: 8.0 a 12.0)
                var stSouth = SpawnPrefab(prefabToSpawn, gViasAceras.transform, new Vector3(x, 0f, 10f), Quaternion.identity);
                ConfigureFloorModule(stSouth, 4f, 4f);
                totalFloors++;

                // Carril Norte (Z = 14.0m, cubre Z: 12.0 a 16.0)
                var stNorth = SpawnPrefab(prefabToSpawn, gViasAceras.transform, new Vector3(x, 0f, 14f), Quaternion.identity);
                ConfigureFloorModule(stNorth, 4f, 4f);
                totalFloors++;
            }

            // 1.4 Acera Opuesta / Frente (Z: 16.0 a 20.0, X: -20 a +20)
            for (float x = -20f; x <= 20f; x += 2f)
            {
                // Módulo con bordillo hacia la calle (Z = 17.0m, cubre 16.0 a 18.0)
                var swOppCurb = SpawnPrefab(pSideway1, gViasAceras.transform, new Vector3(x, 0f, 17f), Quaternion.Euler(0f, 180f, 0f));
                ConfigureFloorModule(swOppCurb, 2f, 2f);
                totalFloors++;

                // Módulo interior pegado a los edificios (Z = 19.0m, cubre 18.0 a 20.0)
                var swOppInner = SpawnPrefab(pSideway5, gViasAceras.transform, new Vector3(x, 0f, 19f), Quaternion.Euler(0f, 180f, 0f));
                ConfigureFloorModule(swOppInner, 2f, 2f);
                totalFloors++;
            }

            // =========================================================================
            // SECCIÓN 2: 02_Edificios_Perimetro (Bloqueo Visual 360° y Optimización)
            // =========================================================================
            string bTowerA = "Assets/POLYGON city pack/Prefabs/Buildings/Building_A_prefab.prefab";       // 40m altura
            string bTowerC1 = "Assets/POLYGON city pack/Prefabs/Buildings/Building_C1_prefab.prefab";     // 30m altura
            string bTowerJ = "Assets/POLYGON city pack/Prefabs/Buildings/Building_J_prefab.prefab";       // 32m altura
            string bTowerH = "Assets/POLYGON city pack/Prefabs/Buildings/Building_H_prefab.prefab";       // 20m altura
            string bPolice = "Assets/POLYGON city pack/Prefabs/Buildings/Police_station_prefab.prefab";   // 15m civic
            string bBlockD = "Assets/POLYGON city pack/Prefabs/Buildings/Building_D_prefab.prefab";       // 15m altura
            string bShopA = "Assets/POLYGON city pack/Prefabs/Buildings/Shop_A_prefab.prefab";            // 10m comercial
            string bShopB = "Assets/POLYGON city pack/Prefabs/Buildings/Shop_B_prefab.prefab";            // 3.6m comercial
            string bSupermarket = "Assets/POLYGON city pack/Prefabs/Buildings/Supermaket_prefab.prefab"; // 9.4m comercial
            string bBuildingT = "Assets/POLYGON city pack/Prefabs/Buildings/Building_T_prefab.prefab";    // 8.5m residencial
            string bBuildingE = "Assets/POLYGON city pack/Prefabs/Buildings/Building_E_prefab.prefab";    // 10m residencial

            // Distribución de Edificios estratégicos perimetrales (360° de cobertura)
            var buildingDefinitions = new (string prefab, Vector3 pos, Quaternion rot, bool keepCollider)[]
            {
                // Frente Norte (Cruzando la calle, mirando hacia el sur)
                (bShopA, new Vector3(-3.5f, 0f, 20.5f), Quaternion.Euler(0f, 180f, 0f), false),
                (bShopB, new Vector3(6f, 0f, 20.5f), Quaternion.Euler(0f, 180f, 0f), false),
                (bTowerC1, new Vector3(-3.5f, 0f, 28f), Quaternion.Euler(0f, 180f, 0f), false),
                (bTowerA, new Vector3(-16f, 0f, 27f), Quaternion.Euler(0f, 180f, 0f), false),
                (bTowerJ, new Vector3(14f, 0f, 27f), Quaternion.Euler(0f, 180f, 0f), false),

                // Flanco Oeste (Izquierda de la oficina, mirando al Este)
                (bPolice, new Vector3(-19f, 0f, 3f), Quaternion.Euler(0f, 90f, 0f), false),
                (bBlockD, new Vector3(-19f, 0f, -8f), Quaternion.Euler(0f, 90f, 0f), false),

                // Flanco Este (Derecha de la oficina, mirando al Oeste)
                (bSupermarket, new Vector3(20f, 0f, 3f), Quaternion.Euler(0f, -90f, 0f), false),
                (bTowerH, new Vector3(21f, 0f, -8f), Quaternion.Euler(0f, -90f, 0f), false),

                // Parte Trasera Sur (Detrás de la oficina, mirando al Norte)
                (bBuildingE, new Vector3(0f, 0f, -10f), Quaternion.identity, false),
                (bBuildingT, new Vector3(-10f, 0f, -11f), Quaternion.identity, false),
                (bTowerH, new Vector3(10f, 0f, -11f), Quaternion.identity, false)
            };

            foreach (var bDef in buildingDefinitions)
            {
                var bObj = SpawnPrefab(bDef.prefab, gEdificios.transform, bDef.pos, bDef.rot);
                if (bObj != null)
                {
                    // Optimización física Quest: remover colliders de edificios lejanos decorativos
                    if (!bDef.keepCollider)
                    {
                        var cols = bObj.GetComponentsInChildren<Collider>(true);
                        foreach (var c in cols)
                        {
                            Object.DestroyImmediate(c);
                        }
                    }
                    totalBuildings++;
                }
            }

            // =========================================================================
            // SECCIÓN 3: 03_Mobiliario_Urbano_Props (Alumbrado, Señalética y Zona EVAK)
            // =========================================================================
            string pStreetLamp = "Assets/POLYGON city pack/Prefabs/Lamps/street lamp 2 prefab.prefab";
            string pTrafficLight = "Assets/POLYGON city pack/Prefabs/Props/Traffic light 1 Prefab.prefab";
            string pPowerPole = "Assets/POLYGON city pack/Prefabs/Props/Power_poles prefab.prefab";
            string pPowerCable = "Assets/POLYGON city pack/Prefabs/Props/Dynamic_power_pole_cable prefab.prefab";
            string pTrashcan = "Assets/POLYGON city pack/Prefabs/Props/trashcan prefab.prefab";
            string pBigTrash = "Assets/POLYGON city pack/Prefabs/Props/Big_trash_bin prefab.prefab";
            string pHydrant = "Assets/POLYGON city pack/Prefabs/Props/Hydrant prefab.prefab";
            string pBench = "Assets/POLYGON city pack/Prefabs/Props/bench prefab.prefab";
            string pPotTree = "Assets/POLYGON city pack/Prefabs/Props/Pot_tree prefab.prefab";
            string pTree = "Assets/POLYGON city pack/Prefabs/Props/Tree prefab.prefab";
            string pWarningStand = "Assets/POLYGON city pack/Prefabs/Props/traffic_warning_stand prefab.prefab";
            string pTrafficCone = "Assets/POLYGON city pack/Prefabs/Props/traffic_obj prefab.prefab";
            string pSignStop = "Assets/POLYGON city pack/Prefabs/traffic signs/stop sign.prefab";
            string pSignSlow = "Assets/POLYGON city pack/Prefabs/traffic signs/slow sign.prefab";
            string pSignParking = "Assets/POLYGON city pack/Prefabs/traffic signs/parking sign.prefab";

            // 3.1 Postes de Iluminación Pública (Lamps)
            Vector3[] lampPositions = new Vector3[]
            {
                new Vector3(-12f, 0f, 7.8f),
                new Vector3(-6.5f, 0f, 7.8f),
                new Vector3(2.5f, 0f, 7.8f),
                new Vector3(10f, 0f, 7.8f),
                new Vector3(-10f, 0f, 16.2f),
                new Vector3(4f, 0f, 16.2f)
            };
            foreach (var lpos in lampPositions)
            {
                float rotY = lpos.z > 12f ? 180f : 0f;
                var lamp = SpawnPrefab(pStreetLamp, gMobiliario.transform, lpos, Quaternion.Euler(0f, rotY, 0f));
                if (lamp != null) totalProps++;
            }

            // 3.2 Semáforos en esquinas de cruce peatonal
            var tl1 = SpawnPrefab(pTrafficLight, gMobiliario.transform, new Vector3(-5.5f, 0f, 7.8f), Quaternion.identity);
            if (tl1 != null) totalProps++;
            var tl2 = SpawnPrefab(pTrafficLight, gMobiliario.transform, new Vector3(0.5f, 0f, 16.2f), Quaternion.Euler(0f, 180f, 0f));
            if (tl2 != null) totalProps++;

            // 3.3 Postes eléctricos y cableado aéreo
            var pole1 = SpawnPrefab(pPowerPole, gMobiliario.transform, new Vector3(-18f, 0f, 7.7f), Quaternion.identity);
            var pole2 = SpawnPrefab(pPowerPole, gMobiliario.transform, new Vector3(18f, 0f, 7.7f), Quaternion.identity);
            var cable = SpawnPrefab(pPowerCable, gMobiliario.transform, new Vector3(0f, 5.5f, 7.7f), Quaternion.identity);
            if (pole1 != null) totalProps++;
            if (pole2 != null) totalProps++;
            if (cable != null) totalProps++;

            // 3.4 Mobiliario urbano: Papeleras, bancos, hidrante y vegetación
            SpawnPrefab(pTrashcan, gMobiliario.transform, new Vector3(-6f, 0f, 7.4f), Quaternion.identity);
            SpawnPrefab(pBigTrash, gMobiliario.transform, new Vector3(12f, 0f, 7.4f), Quaternion.identity);
            SpawnPrefab(pHydrant, gMobiliario.transform, new Vector3(7.5f, 0f, 7.8f), Quaternion.identity);
            SpawnPrefab(pBench, gMobiliario.transform, new Vector3(3f, 0f, 4.4f), Quaternion.identity);
            SpawnPrefab(pPotTree, gMobiliario.transform, new Vector3(-1.2f, 0f, 4.3f), Quaternion.identity);
            SpawnPrefab(pPotTree, gMobiliario.transform, new Vector3(5.5f, 0f, 4.3f), Quaternion.identity);
            SpawnPrefab(pTree, gMobiliario.transform, new Vector3(-15f, 0f, 5.5f), Quaternion.identity);
            SpawnPrefab(pTree, gMobiliario.transform, new Vector3(15f, 0f, 5.5f), Quaternion.identity);
            totalProps += 8;

            // 3.5 Señalética de tráfico
            SpawnPrefab(pSignStop, gMobiliario.transform, new Vector3(-15f, 0f, 7.8f), Quaternion.identity);
            SpawnPrefab(pSignSlow, gMobiliario.transform, new Vector3(-8f, 0f, 7.8f), Quaternion.identity);
            SpawnPrefab(pSignParking, gMobiliario.transform, new Vector3(14f, 0f, 7.8f), Quaternion.identity);
            totalProps += 3;

            // 3.6 Zona de Evacuación y Punto de Encuentro (Narrativa EVAK VR)
            // Delimitada en la acera frente a la salida exterior (X: -4.5 a -1.5, Z: 5.5 a 7.5)
            GameObject zonaEvac = new GameObject("Punto_De_Encuentro_Evacuacion_EVAK");
            zonaEvac.transform.SetParent(gMobiliario.transform);
            zonaEvac.transform.position = new Vector3(-3.0f, 0f, 6.5f);
            Undo.RegisterCreatedObjectUndo(zonaEvac, "Crear " + zonaEvac.name);

            // Marcador visual de suelo (Cuadrante de seguridad verde translúcido)
            GameObject marcadorSuelo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marcadorSuelo.name = "Marcador_Suelo_Zona_Segura";
            marcadorSuelo.transform.SetParent(zonaEvac.transform);
            marcadorSuelo.transform.position = new Vector3(-3.0f, 0.005f, 6.5f);
            marcadorSuelo.transform.localScale = new Vector3(2.8f, 0.01f, 1.8f);
            Undo.RegisterCreatedObjectUndo(marcadorSuelo, "Crear " + marcadorSuelo.name);

            // Material URP verde de emergencia guardado como asset permanente
            string matPath = "Assets/POLYGON city pack/Materials/Mat_Evac_SafeZone.mat";
            Material matEvac = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (matEvac == null)
            {
                matEvac = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                matEvac.name = "Mat_Evac_SafeZone";
                matEvac.SetColor("_BaseColor", new Color(0.1f, 0.75f, 0.25f, 0.9f));
                AssetDatabase.CreateAsset(matEvac, matPath);
            }
            marcadorSuelo.GetComponent<Renderer>().sharedMaterial = matEvac;

            // Collider trigger informativo para la lógica de escape
            var boxTrigger = marcadorSuelo.GetComponent<BoxCollider>();
            if (boxTrigger != null)
            {
                boxTrigger.isTrigger = true;
                boxTrigger.size = new Vector3(1f, 200f, 1f); // Altura de detección
                boxTrigger.center = new Vector3(0f, 100f, 0f);
            }

            // Vallas y conos delimitadores de seguridad en las esquinas
            SpawnPrefab(pWarningStand, zonaEvac.transform, new Vector3(-4.3f, 0f, 5.7f), Quaternion.identity);
            SpawnPrefab(pWarningStand, zonaEvac.transform, new Vector3(-1.7f, 0f, 5.7f), Quaternion.identity);
            SpawnPrefab(pTrafficCone, zonaEvac.transform, new Vector3(-4.3f, 0f, 7.3f), Quaternion.identity);
            SpawnPrefab(pTrafficCone, zonaEvac.transform, new Vector3(-1.7f, 0f, 7.3f), Quaternion.identity);
            totalProps += 5;

            // =========================================================================
            // SECCIÓN 4: Optimización para Meta Quest (Android Standalone)
            // =========================================================================
            // Activar Static Flags (Static Batching, Occluder/Occludee) en toda la jerarquía
            SetHierarchyStatic(root);

            // 5. Guardar escena y registrar cambios
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string report = $"[ExteriorCityBuilder] ENTORNO EXTERIOR CONSTRUIDO CON ÉXITO:\n" +
                            $"- Vías y Módulos de Suelo: {totalFloors} (con BoxColliders alineados a Y=0.0)\n" +
                            $"- Edificios Perimetrales (360°): {totalBuildings} (física optimizada sin colliders lejanos)\n" +
                            $"- Mobiliario, Luces y Props: {totalProps}\n" +
                            $"- Punto de Encuentro EVAK VR configurado en (-3.0, 0, 6.5)\n" +
                            $"- Todos los objetos marcados como Static para Meta Quest.\n" +
                            $"Escena guardada: {scene.name}";

            Debug.Log(report);
            return report;
        }

        private static GameObject CreateSubGroup(GameObject parent, string name)
        {
            GameObject sub = new GameObject(name);
            sub.transform.SetParent(parent.transform);
            sub.transform.localPosition = Vector3.zero;
            sub.transform.localRotation = Quaternion.identity;
            sub.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(sub, "Crear " + name);
            return sub;
        }

        private static GameObject SpawnPrefab(string path, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[ExteriorCityBuilder] No se encontró el prefab en: {path}");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.SetParent(parent);
            instance.transform.localPosition = localPos;
            instance.transform.localRotation = localRot;
            instance.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(instance, "Spawn " + instance.name);
            return instance;
        }

        private static void ConfigureFloorModule(GameObject floorModule, float sizeX, float sizeZ)
        {
            if (floorModule == null) return;

            // Remover MeshColliders complejos para optimización de físicas y raycasts en Quest
            var meshCols = floorModule.GetComponentsInChildren<MeshCollider>(true);
            foreach (var mc in meshCols)
            {
                Object.DestroyImmediate(mc);
            }

            // Asegurar un BoxCollider limpio alineado en cota Y=0.0
            BoxCollider bc = floorModule.GetComponent<BoxCollider>();
            if (bc == null)
            {
                bc = floorModule.AddComponent<BoxCollider>();
            }

            // Grosor de 0.2m bajo el suelo con superficie superior exactamente en Y=0.0
            bc.size = new Vector3(sizeX, 0.2f, sizeZ);
            bc.center = new Vector3(0f, -0.1f, 0f);
        }

        private static void SetHierarchyStatic(GameObject root)
        {
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic |
                                      StaticEditorFlags.OccludeeStatic |
                                      StaticEditorFlags.OccluderStatic |
                                      StaticEditorFlags.ReflectionProbeStatic;

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(transform.gameObject, flags);
            }
        }
    }
}
