using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XRSimulation.Editor
{
    public static class OfficeEnvironmentBuilder
    {
        [MenuItem("XR Tools/Construir Entorno Oficina")]
        public static string BuildOffice()
        {
            var scene = SceneManager.GetActiveScene();
            Undo.SetCurrentGroupName("Construir Entorno Oficina XR");
            int undoGroup = Undo.GetCurrentGroup();

            // 1. Manejo seguro de FireHazard existente antes de limpiar placeholders
            GameObject fireHazard = GameObject.Find("FireHazard");
            if (fireHazard != null)
            {
                Undo.SetTransformParent(fireHazard.transform, null, "Desvincular FireHazard");
            }

            // 2. Limpieza de placeholder residual 'Cuarto' y objetos antiguos
            GameObject cuartoOld = GameObject.Find("Cuarto");
            if (cuartoOld != null)
            {
                Undo.DestroyObjectImmediate(cuartoOld);
            }

            // Si ya exista una versin anterior de Entorno_Oficina, eliminarla limpiamente
            GameObject entornoOld = GameObject.Find("Entorno_Oficina");
            if (entornoOld != null)
            {
                Undo.DestroyObjectImmediate(entornoOld);
            }

            // 3. Crear raz Entorno_Oficina
            GameObject root = new GameObject("Entorno_Oficina");
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(root, "Crear Entorno_Oficina");

            // Crear sub-jerarquas requeridas
            GameObject gEstructura = CreateSubGroup(root, "01_Estructura");
            GameObject gMobiliario = CreateSubGroup(root, "02_Mobiliario_Trabajo");
            GameObject gReunionCoffee = CreateSubGroup(root, "03_Sala_Reunion_Coffee");
            GameObject gProps = CreateSubGroup(root, "04_Props_Decoracion");

            // Materiales del proyecto
            Material matSuelo = AssetDatabase.LoadAssetAtPath<Material>("Assets/nappin/OfficeEssentialsPack/Materials/(Mat)Floor.mat")
                                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Cuarto_Suelo.mat");
            Material matPared = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Cuarto_Pared.mat");
            Material matTecho = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Cuarto_Techo.mat");

            // Prefabs de OfficeEssentialsPack
            string pDesk1 = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)Desk1.prefab";
            string pDesk2 = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)Desk2.prefab";
            string pChair = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)OfficeChair.prefab";
            string pPC = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)PC.prefab";
            string pPCCase = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)PCCase.prefab";
            string pTrashCan = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)TrashCan.prefab";
            string pDeskLight = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)DeskLight.prefab";
            string pConfTable = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)ConferenceTable.prefab";
            string pWater = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)WaterDispenser.prefab";
            string pCoffeePot = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)CoffePot.prefab";
            string pBigDrawer = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)BigDrawer.prefab";
            string pPlant1 = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)Plant1.prefab";
            string pPlantBox = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)PlantBox.prefab";
            string pDoor = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)SeparatorDoor.prefab";
            string pSeparator = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)Separator.prefab";
            string pBookPile1 = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)BookPile1.prefab";
            string pBookPile2 = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)BookPile2.prefab";
            string pCeilingLight = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)CeilingLight1.prefab";

            // -----------------------------------------------------------------------------------------
            // 01_Estructura: Suelo (12x8m) y 4 Paredes (3m altura) con vano norte para SeparatorDoor
            // Coordenadas: X [-6 a +6], Z [-4 a +4], Y [0 a 3]
            // -----------------------------------------------------------------------------------------

            // Suelo (12m x 8m, BoxCollider)
            GameObject suelo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            suelo.name = "Suelo_12x8m";
            suelo.transform.SetParent(gEstructura.transform);
            suelo.transform.position = new Vector3(0f, -0.05f, 0f);
            suelo.transform.localScale = new Vector3(12f, 0.1f, 8f);
            if (matSuelo != null) suelo.GetComponent<Renderer>().sharedMaterial = matSuelo;
            EnsureBoxCollider(suelo);
            SetStatic(suelo, true);

            // Pared Sur (Z = -4.0, 12m largo, 3m alto)
            GameObject paredSur = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paredSur.name = "Pared_Sur";
            paredSur.transform.SetParent(gEstructura.transform);
            paredSur.transform.position = new Vector3(0f, 1.5f, -4.0f);
            paredSur.transform.localScale = new Vector3(12f, 3f, 0.2f);
            if (matPared != null) paredSur.GetComponent<Renderer>().sharedMaterial = matPared;
            EnsureBoxCollider(paredSur);
            SetStatic(paredSur, true);

            // Pared Oeste (X = -6.0, 8m largo, 3m alto)
            GameObject paredOeste = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paredOeste.name = "Pared_Oeste";
            paredOeste.transform.SetParent(gEstructura.transform);
            paredOeste.transform.position = new Vector3(-6.0f, 1.5f, 0f);
            paredOeste.transform.localScale = new Vector3(0.2f, 3f, 8f);
            if (matPared != null) paredOeste.GetComponent<Renderer>().sharedMaterial = matPared;
            EnsureBoxCollider(paredOeste);
            SetStatic(paredOeste, true);

            // Pared Este (X = +6.0, 8m largo, 3m alto)
            GameObject paredEste = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paredEste.name = "Pared_Este";
            paredEste.transform.SetParent(gEstructura.transform);
            paredEste.transform.position = new Vector3(6.0f, 1.5f, 0f);
            paredEste.transform.localScale = new Vector3(0.2f, 3f, 8f);
            if (matPared != null) paredEste.GetComponent<Renderer>().sharedMaterial = matPared;
            EnsureBoxCollider(paredEste);
            SetStatic(paredEste, true);

            // Pared Norte / Frontal con vano para [Prb]SeparatorDoor
            // Vano de puerta ubicado en el pasillo de entrada: X = [-4.21 a -2.79] (ancho ~1.42m)
            // Tramo Izquierdo: X = [-6.0 a -4.21] -> ancho 1.79m, centro X = -5.105
            GameObject paredNorteIzq = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paredNorteIzq.name = "Pared_Norte_Izq";
            paredNorteIzq.transform.SetParent(gEstructura.transform);
            paredNorteIzq.transform.position = new Vector3(-5.105f, 1.5f, 4.0f);
            paredNorteIzq.transform.localScale = new Vector3(1.79f, 3f, 0.2f);
            if (matPared != null) paredNorteIzq.GetComponent<Renderer>().sharedMaterial = matPared;
            EnsureBoxCollider(paredNorteIzq);
            SetStatic(paredNorteIzq, true);

            // Tramo Derecho: X = [-2.79 a +6.0] -> ancho 8.79m, centro X = 1.605
            GameObject paredNorteDer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paredNorteDer.name = "Pared_Norte_Der";
            paredNorteDer.transform.SetParent(gEstructura.transform);
            paredNorteDer.transform.position = new Vector3(1.605f, 1.5f, 4.0f);
            paredNorteDer.transform.localScale = new Vector3(8.79f, 3f, 0.2f);
            if (matPared != null) paredNorteDer.GetComponent<Renderer>().sharedMaterial = matPared;
            EnsureBoxCollider(paredNorteDer);
            SetStatic(paredNorteDer, true);

            // Instancia de Puerta [Prb]SeparatorDoor en el vano
            GameObject puerta = SpawnPrefab(pDoor, gEstructura.transform, new Vector3(-2.8f, 0f, 4.0f), Quaternion.identity);
            if (puerta != null)
            {
                puerta.name = "Puerta_Entrada_SeparatorDoor";
                EnsureBoxCollider(puerta);
                SetStatic(puerta, true);
            }

            // Techo perimetral
            GameObject techo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            techo.name = "Techo";
            techo.transform.SetParent(gEstructura.transform);
            techo.transform.position = new Vector3(0f, 3.05f, 0f);
            techo.transform.localScale = new Vector3(12f, 0.1f, 8f);
            if (matTecho != null) techo.GetComponent<Renderer>().sharedMaterial = matTecho;
            SetStatic(techo, true);

            // Luminarias de techo (CeilingLight1)
            Vector3[] lightPositions = new Vector3[] {
                new Vector3(-3.5f, 2.95f, 2.0f),
                new Vector3(-0.6f, 2.95f, 1.3f),
                new Vector3(-0.6f, 2.95f, -2.4f),
                new Vector3(4.0f, 2.95f, 1.5f),
                new Vector3(4.0f, 2.95f, -1.5f),
                new Vector3(-3.5f, 2.95f, -2.0f)
            };
            foreach (var lpos in lightPositions)
            {
                var cl = SpawnPrefab(pCeilingLight, gEstructura.transform, lpos, Quaternion.identity);
                if (cl != null) SetStatic(cl, true);
            }

            // -----------------------------------------------------------------------------------------
            // 02_Mobiliario_Trabajo: 2 Islas de Trabajo separadas con pasillo >= 1.8m
            // Cada puesto: [Prb]OfficeChair, [Prb]PC, [Prb]PCCase, [Prb]TrashCan, [Prb]DeskLight
            // -----------------------------------------------------------------------------------------
            GameObject isla1 = new GameObject("Isla_Trabajo_01");
            isla1.transform.SetParent(gMobiliario.transform);
            isla1.transform.position = Vector3.zero;

            GameObject isla2 = new GameObject("Isla_Trabajo_02");
            isla2.transform.SetParent(gMobiliario.transform);
            isla2.transform.position = Vector3.zero;

            // ISLA 1 (Norte-Centro): Desks alineados enfrentados en Z = 1.75 y Z = 0.75
            // Puesto 1A:
            var d1 = SpawnPrefab(pDesk1, isla1.transform, new Vector3(-0.6f, 0f, 1.75f), Quaternion.identity);
            SetStatic(d1, true);
            EnsureBoxCollider(d1);
            SpawnPrefab(pChair, isla1.transform, new Vector3(-0.6f, 0f, 2.35f), Quaternion.Euler(0f, 180f, 0f));
            SpawnPrefab(pPC, isla1.transform, new Vector3(-0.6f, 0.947f, 1.85f), Quaternion.identity);
            SpawnPrefab(pPCCase, isla1.transform, new Vector3(-1.15f, 0f, 1.75f), Quaternion.identity);
            SpawnPrefab(pTrashCan, isla1.transform, new Vector3(-1.15f, 0f, 2.35f), Quaternion.identity);
            SpawnPrefab(pDeskLight, isla1.transform, new Vector3(-0.15f, 0.947f, 1.85f), Quaternion.identity);
            SpawnPrefab(pBookPile1, isla1.transform, new Vector3(-0.95f, 0.947f, 1.85f), Quaternion.identity);

            // Puesto 1B:
            var d2 = SpawnPrefab(pDesk2, isla1.transform, new Vector3(-0.6f, 0f, 0.75f), Quaternion.Euler(0f, 180f, 0f));
            SetStatic(d2, true);
            EnsureBoxCollider(d2);
            SpawnPrefab(pChair, isla1.transform, new Vector3(-0.6f, 0f, 0.15f), Quaternion.identity);
            SpawnPrefab(pPC, isla1.transform, new Vector3(-0.6f, 0.947f, 0.65f), Quaternion.Euler(0f, 180f, 0f));
            SpawnPrefab(pPCCase, isla1.transform, new Vector3(-1.15f, 0f, 0.75f), Quaternion.Euler(0f, 180f, 0f));
            SpawnPrefab(pTrashCan, isla1.transform, new Vector3(-1.15f, 0f, 0.15f), Quaternion.Euler(0f, 180f, 0f));
            SpawnPrefab(pDeskLight, isla1.transform, new Vector3(-0.15f, 0.947f, 0.65f), Quaternion.Euler(0f, 180f, 0f));

            // ISLA 2 (Sur-Centro): Desks en Z = -1.85 y Z = -2.85 (Separacin con Isla 1: 1.85m de pasillo peatonal)
            // Puesto 2A:
            var d3 = SpawnPrefab(pDesk1, isla2.transform, new Vector3(-0.6f, 0f, -1.85f), Quaternion.identity);
            SetStatic(d3, true);
            EnsureBoxCollider(d3);
            SpawnPrefab(pChair, isla2.transform, new Vector3(-0.6f, 0f, -1.25f), Quaternion.Euler(0f, 180f, 0f));
            SpawnPrefab(pPC, isla2.transform, new Vector3(-0.6f, 0.947f, -1.75f), Quaternion.identity);
            SpawnPrefab(pPCCase, isla2.transform, new Vector3(-1.15f, 0f, -1.85f), Quaternion.identity);
            SpawnPrefab(pTrashCan, isla2.transform, new Vector3(-1.15f, 0f, -1.25f), Quaternion.identity);
            SpawnPrefab(pDeskLight, isla2.transform, new Vector3(-0.15f, 0.947f, -1.75f), Quaternion.identity);
            SpawnPrefab(pBookPile2, isla2.transform, new Vector3(-0.95f, 0.947f, -1.75f), Quaternion.identity);

            // Puesto 2B (Foco del incidente de emergencia):
            var d4 = SpawnPrefab(pDesk2, isla2.transform, new Vector3(-0.6f, 0f, -2.85f), Quaternion.Euler(0f, 180f, 0f));
            SetStatic(d4, true);
            EnsureBoxCollider(d4);
            SpawnPrefab(pChair, isla2.transform, new Vector3(-0.6f, 0f, -3.45f), Quaternion.identity);
            SpawnPrefab(pPC, isla2.transform, new Vector3(-0.6f, 0.947f, -2.95f), Quaternion.Euler(0f, 180f, 0f));
            SpawnPrefab(pPCCase, isla2.transform, new Vector3(-1.15f, 0f, -2.85f), Quaternion.Euler(0f, 180f, 0f));
            var trashCanFuego = SpawnPrefab(pTrashCan, isla2.transform, new Vector3(-1.15f, 0f, -3.45f), Quaternion.Euler(0f, 180f, 0f));
            SpawnPrefab(pDeskLight, isla2.transform, new Vector3(-0.15f, 0.947f, -2.95f), Quaternion.Euler(0f, 180f, 0f));

            // Reposicionamiento del FireHazard en la papelera / bajo escritorio de Isla 2
            if (fireHazard != null)
            {
                fireHazard.transform.SetParent(isla2.transform);
                fireHazard.transform.position = new Vector3(-1.15f, 0.05f, -3.45f);
                fireHazard.transform.rotation = Quaternion.identity;
                Debug.Log("FireHazard reubicado con xito en la papelera de la Isla de Trabajo 2.");
            }

            // -----------------------------------------------------------------------------------------
            // 03_Sala_Reunion_Coffee: Sala de reuniones con ConferenceTable y Coffee Corner
            // -----------------------------------------------------------------------------------------
            GameObject gReunion = new GameObject("Zona_Reunion");
            gReunion.transform.SetParent(gReunionCoffee.transform);

            // ConferenceTable rodeada de 6 OfficeChairs (Centro en X = 4.1, Z = 0.0)
            var ctable = SpawnPrefab(pConfTable, gReunion.transform, new Vector3(4.1f, 0f, 0f), Quaternion.identity);
            SetStatic(ctable, true);
            EnsureBoxCollider(ctable);

            // Sillas de reuniones (4 laterales, 2 cabeceras)
            SpawnPrefab(pChair, gReunion.transform, new Vector3(3.3f, 0f, 0.65f), Quaternion.Euler(0f, 90f, 0f));
            SpawnPrefab(pChair, gReunion.transform, new Vector3(3.3f, 0f, -0.65f), Quaternion.Euler(0f, 90f, 0f));
            SpawnPrefab(pChair, gReunion.transform, new Vector3(4.9f, 0f, 0.65f), Quaternion.Euler(0f, -90f, 0f));
            SpawnPrefab(pChair, gReunion.transform, new Vector3(4.9f, 0f, -0.65f), Quaternion.Euler(0f, -90f, 0f));
            SpawnPrefab(pChair, gReunion.transform, new Vector3(4.1f, 0f, 1.65f), Quaternion.Euler(0f, 180f, 0f));
            SpawnPrefab(pChair, gReunion.transform, new Vector3(4.1f, 0f, -1.65f), Quaternion.identity);

            // Separador arquitectnico de cristal hacia el pasillo
            var sepReunion = SpawnPrefab(pSeparator, gReunion.transform, new Vector3(2.2f, 0f, 0.8f), Quaternion.Euler(0f, 90f, 0f));
            if (sepReunion != null)
            {
                SetStatic(sepReunion, true);
                EnsureBoxCollider(sepReunion);
            }

            // rea Coffee / Descanso (Suroeste: X = -4.0, Z = -3.6)
            GameObject gCoffee = new GameObject("Zona_Coffee");
            gCoffee.transform.SetParent(gReunionCoffee.transform);

            // Mueble bajo [Prb]BigDrawer contra pared sur
            var bigDrawer = SpawnPrefab(pBigDrawer, gCoffee.transform, new Vector3(-4.0f, 0f, -3.65f), Quaternion.identity);
            SetStatic(bigDrawer, true);
            EnsureBoxCollider(bigDrawer);

            // [Prb]CoffePot sobre el mueble (superficie Y ~ 1.18m)
            SpawnPrefab(pCoffeePot, gCoffee.transform, new Vector3(-4.2f, 1.18f, -3.65f), Quaternion.Euler(0f, 180f, 0f));

            // [Prb]WaterDispenser de pie junto al mueble
            var waterDisp = SpawnPrefab(pWater, gCoffee.transform, new Vector3(-5.3f, 0f, -3.2f), Quaternion.Euler(0f, 90f, 0f));
            EnsureBoxCollider(waterDisp);
            SetStatic(waterDisp, true);

            // -----------------------------------------------------------------------------------------
            // 04_Props_Decoracion: Plantas, archivadores y separadores bioflicos
            // -----------------------------------------------------------------------------------------
            // PlantBoxes como delimitadores visuales (sin invadir el pasillo de 1.8m)
            var pb1 = SpawnPrefab(pPlantBox, gProps.transform, new Vector3(2.2f, 0f, -1.6f), Quaternion.Euler(0f, 90f, 0f));
            if (pb1 != null) { SetStatic(pb1, true); EnsureBoxCollider(pb1); }

            var pb2 = SpawnPrefab(pPlantBox, gProps.transform, new Vector3(-3.0f, 0f, 1.2f), Quaternion.Euler(0f, 90f, 0f));
            if (pb2 != null) { SetStatic(pb2, true); EnsureBoxCollider(pb2); }

            // Plantas en esquinas
            var pl1 = SpawnPrefab(pPlant1, gProps.transform, new Vector3(5.4f, 0f, 3.4f), Quaternion.identity);
            if (pl1 != null) { SetStatic(pl1, true); EnsureBoxCollider(pl1); }

            var pl2 = SpawnPrefab(pPlant1, gProps.transform, new Vector3(5.4f, 0f, -3.4f), Quaternion.identity);
            if (pl2 != null) { SetStatic(pl2, true); EnsureBoxCollider(pl2); }

            var pl3 = SpawnPrefab(pPlant1, gProps.transform, new Vector3(-5.4f, 0f, 3.4f), Quaternion.identity);
            if (pl3 != null) { SetStatic(pl3, true); EnsureBoxCollider(pl3); }

            // Archivadores adicionales sobre el mueble de descanso
            SpawnPrefab(pBookPile1, gProps.transform, new Vector3(-3.0f, 1.18f, -3.65f), Quaternion.identity);

            // -----------------------------------------------------------------------------------------
            // Reposicionamiento de Mecnicas XR:
            // 1. XR Origin Hands en punto de spawn en el pasillo de entrada
            // 2. Extintor interactivo (fire_extinguisher) en pared/soporte a altura ergonmica 1.0m - 1.2m
            // -----------------------------------------------------------------------------------------
            GameObject xrOrigin = GameObject.Find("XR Origin Hands (XR Rig)");
            if (xrOrigin != null)
            {
                xrOrigin.transform.position = new Vector3(-3.5f, 0f, 2.8f);
                xrOrigin.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                Debug.Log("XR Origin Hands posicionado en spawn de entrada: (-3.5, 0, 2.8).");
            }

            // Soporte de pared ergonmico para el extintor
            GameObject soporteExtintor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            soporteExtintor.name = "Soporte_Extintor";
            soporteExtintor.transform.SetParent(gEstructura.transform);
            soporteExtintor.transform.position = new Vector3(-4.8f, 1.0f, 3.85f);
            soporteExtintor.transform.localScale = new Vector3(0.35f, 0.05f, 0.25f);
            EnsureBoxCollider(soporteExtintor);
            SetStatic(soporteExtintor, true);

            // Extintor interactivo (fire_extinguisher) colocado sobre su soporte
            GameObject extintor = GameObject.Find("fire_extinguisher");
            if (extintor != null)
            {
                // Altura de agarre ergonmica estndar: 1.12m sobre el suelo
                extintor.transform.position = new Vector3(-4.8f, 1.12f, 3.85f);
                extintor.transform.rotation = Quaternion.Euler(270f, 180f, 0f);
                Debug.Log("Extintor posicionado a altura ergonmica 1.12m sobre pared norte de entrada.");
            }

            // Marcar escena como modificada y guardar
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Undo.CollapseUndoOperations(undoGroup);
            return "Entorno de oficina construido y guardado con xito en " + scene.path;
        }

        private static GameObject CreateSubGroup(GameObject parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(go, "Crear grupo " + name);
            return go;
        }

        private static GameObject SpawnPrefab(string path, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("No se encontr el prefab en la ruta: " + path);
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent);
            instance.transform.localPosition = localPos;
            instance.transform.localRotation = localRot;
            Undo.RegisterCreatedObjectUndo(instance, "Instanciar " + prefab.name);
            return instance;
        }

        private static void EnsureBoxCollider(GameObject go)
        {
            if (go == null) return;
            var col = go.GetComponent<Collider>();
            if (col == null)
            {
                var r = go.GetComponentInChildren<Renderer>();
                var box = go.AddComponent<BoxCollider>();
                if (r != null)
                {
                    box.center = go.transform.InverseTransformPoint(r.bounds.center);
                    box.size = r.bounds.size;
                }
            }
        }

        private static void SetStatic(GameObject go, bool isStatic)
        {
            if (go == null) return;
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic |
                                      StaticEditorFlags.OccludeeStatic |
                                      StaticEditorFlags.OccluderStatic |
                                      StaticEditorFlags.ContributeGI;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            foreach (Transform t in go.transform)
            {
                SetStatic(t.gameObject, isStatic);
            }
        }
    }
}
