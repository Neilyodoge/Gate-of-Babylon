#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace XianTu.Editor
{
    public static class VillageHubWhiteboxBuilder
    {
        private const string RootFolder = "Assets/1Game/Resources/LevelDesign/VillageHub";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string PrefabPath = RootFolder + "/WB_VillageHub.prefab";

        [MenuItem("仙途秘境/关卡工具/生成基地白盒", false, 80)]
        public static void Build()
        {
            EnsureFolder("Assets/1Game/Resources/LevelDesign", "VillageHub");
            EnsureFolder(RootFolder, "Materials");

            Material stone = GetOrCreateMaterial("M_Hub_Stone", new Color(0.68f, 0.66f, 0.58f));
            Material floor = GetOrCreateMaterial("M_Hub_Floor", new Color(0.48f, 0.44f, 0.34f));
            Material blue = GetOrCreateMaterial("M_Hub_Blue", new Color(0.18f, 0.32f, 0.48f));
            Material gold = GetOrCreateMaterial("M_Hub_Gold", new Color(0.75f, 0.55f, 0.18f));
            Material green = GetOrCreateMaterial("M_Hub_Green", new Color(0.28f, 0.46f, 0.24f));
            Material wood = GetOrCreateMaterial("M_Hub_Wood", new Color(0.30f, 0.20f, 0.12f));

            var root = new GameObject("WB_VillageHub");
            var geometry = new GameObject("WhiteboxGeometry");
            geometry.transform.SetParent(root.transform, false);

            CreateCube(geometry.transform, "Floor", new Vector3(0f, -0.25f, 0f),
                new Vector3(32f, 0.5f, 30f), floor, true);

            BuildBoundary(geometry.transform, stone, gold);
            BuildCentralLandmark(geometry.transform, stone, green, gold);
            BuildShelter(geometry.transform, new Vector3(-10f, 0f, 1.5f),
                "PreparationShelter", stone, blue, wood);
            BuildShelter(geometry.transform, new Vector3(10f, 0f, 1.5f),
                "CaveShelter", stone, blue, wood);
            BuildMapTablet(geometry.transform, new Vector3(6f, 0f, 8f), stone, green);

            var slots = new GameObject("FunctionalSlots");
            slots.transform.SetParent(root.transform, false);
            CreateSlot(slots.transform, VillageHubSlotType.PlayerSpawn, new Vector3(0f, 0.1f, -10f));
            CreateSlot(slots.transform, VillageHubSlotType.RealmPortal, new Vector3(0f, 0f, 11.8f));
            CreateSlot(slots.transform, VillageHubSlotType.PreparationStation, new Vector3(-10f, 0f, 1.5f));
            CreateSlot(slots.transform, VillageHubSlotType.CaveStation, new Vector3(10f, 0f, 1.5f));
            CreateSlot(slots.transform, VillageHubSlotType.MapTablet, new Vector3(6f, 0f, 8f));

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log($"<color=#66ff99>[基地白盒] 已生成 {PrefabPath}，含 5 个功能插槽。</color>");
        }

        private static void BuildBoundary(Transform parent, Material stone, Material gold)
        {
            CreateCube(parent, "Wall_Left", new Vector3(-15.5f, 2.4f, 0f),
                new Vector3(1f, 4.8f, 30f), stone, true);
            CreateCube(parent, "Wall_Right", new Vector3(15.5f, 2.4f, 0f),
                new Vector3(1f, 4.8f, 30f), stone, true);
            CreateCube(parent, "Wall_Back_Left", new Vector3(-10f, 2.4f, 14.5f),
                new Vector3(11f, 4.8f, 1f), stone, true);
            CreateCube(parent, "Wall_Back_Right", new Vector3(10f, 2.4f, 14.5f),
                new Vector3(11f, 4.8f, 1f), stone, true);
            CreateCube(parent, "Gatehouse_Top", new Vector3(0f, 5.2f, 14.5f),
                new Vector3(9f, 1.2f, 1.4f), stone, true);
            CreateCube(parent, "Gatehouse_Left", new Vector3(-3.8f, 3.1f, 14.2f),
                new Vector3(1.5f, 6.2f, 2f), stone, true);
            CreateCube(parent, "Gatehouse_Right", new Vector3(3.8f, 3.1f, 14.2f),
                new Vector3(1.5f, 6.2f, 2f), stone, true);
            CreateCube(parent, "GateMark", new Vector3(0f, 6f, 13.75f),
                new Vector3(1.4f, 1.4f, 0.15f), gold, false);

            CreateCube(parent, "Wall_Front_Left", new Vector3(-9.5f, 1.6f, -14.5f),
                new Vector3(12f, 3.2f, 1f), stone, true);
            CreateCube(parent, "Wall_Front_Right", new Vector3(9.5f, 1.6f, -14.5f),
                new Vector3(12f, 3.2f, 1f), stone, true);
        }

        private static void BuildCentralLandmark(
            Transform parent,
            Material stone,
            Material green,
            Material gold)
        {
            var dais = CreatePrimitive(parent, "CentralPlaza", PrimitiveType.Cylinder,
                new Vector3(0f, 0.03f, 0f), new Vector3(4.8f, 0.06f, 4.8f), stone, false);
            dais.transform.localRotation = Quaternion.Euler(0f, 22.5f, 0f);

            CreatePrimitive(parent, "AncientTree_Trunk", PrimitiveType.Cylinder,
                new Vector3(0f, 1.35f, 0f), new Vector3(0.55f, 1.35f, 0.55f), gold, false);
            CreatePrimitive(parent, "AncientTree_Crown_A", PrimitiveType.Sphere,
                new Vector3(-0.6f, 3.2f, 0f), new Vector3(2.1f, 1.5f, 1.8f), green, false);
            CreatePrimitive(parent, "AncientTree_Crown_B", PrimitiveType.Sphere,
                new Vector3(0.8f, 3.5f, 0.2f), new Vector3(1.9f, 1.7f, 1.7f), green, false);
        }

        private static void BuildShelter(
            Transform parent,
            Vector3 origin,
            string name,
            Material stone,
            Material cloth,
            Material wood)
        {
            var shelter = new GameObject(name);
            shelter.transform.SetParent(parent, false);
            shelter.transform.localPosition = origin;
            CreateCube(shelter.transform, "Back", new Vector3(0f, 1.5f, 2.2f),
                new Vector3(6.5f, 3f, 0.5f), stone, true);
            CreateCube(shelter.transform, "Post_Left", new Vector3(-2.6f, 1.5f, -1.5f),
                new Vector3(0.35f, 3f, 0.35f), wood, false);
            CreateCube(shelter.transform, "Post_Right", new Vector3(2.6f, 1.5f, -1.5f),
                new Vector3(0.35f, 3f, 0.35f), wood, false);
            var roof = CreateCube(shelter.transform, "ClothRoof", new Vector3(0f, 3.05f, 0.3f),
                new Vector3(6.2f, 0.18f, 4.5f), cloth, false);
            roof.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
            CreateCube(shelter.transform, "Counter", new Vector3(0f, 0.65f, 0.9f),
                new Vector3(3.8f, 1.3f, 0.8f), wood, true);
        }

        private static void BuildMapTablet(
            Transform parent,
            Vector3 origin,
            Material stone,
            Material accent)
        {
            var tablet = new GameObject("MapTabletVisual");
            tablet.transform.SetParent(parent, false);
            tablet.transform.localPosition = origin;
            CreateCube(tablet.transform, "Base", new Vector3(0f, 0.3f, 0f),
                new Vector3(2.4f, 0.6f, 1.4f), stone, false);
            var face = CreateCube(tablet.transform, "Face", new Vector3(0f, 1.25f, 0f),
                new Vector3(1.9f, 1.6f, 0.28f), accent, false);
            face.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        }

        private static void CreateSlot(
            Transform parent,
            VillageHubSlotType type,
            Vector3 localPosition)
        {
            var go = new GameObject($"Slot_{type}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<VillageHubSlot>().Configure(type);
        }

        private static GameObject CreateCube(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            return CreatePrimitive(parent, name, PrimitiveType.Cube,
                localPosition, localScale, material, keepCollider);
        }

        private static GameObject CreatePrimitive(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                var collider = go.GetComponent<Collider>();
                if (collider != null)
                    Object.DestroyImmediate(collider);
            }
            return go;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
