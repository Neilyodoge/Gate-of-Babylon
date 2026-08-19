using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace XianTu.Editor
{
    public static class KayKitMonsterPrefabBuilder
    {
        private const string OutputFolder = "Assets/1Game/Resources/MonsterPrefabs/KayKit";
        private const string SkeletonRoot =
            "Assets/1Game/ArtRes/Package/Monster/KayKit/Characters/KayKit - Skeletons (for Unity)/Prefabs";
        private const string AdventurerRoot =
            "Assets/1Game/ArtRes/Package/Monster/KayKit/Characters/KayKit - Adventurers (for Unity)/Prefabs";
        private const string AnimationRoot =
            "Assets/1Game/ArtRes/Package/Monster/KayKit/Characters/Animations/Animations";

        [MenuItem("仙途秘境/怪物/生成 KayKit 昼夜怪物", false, 30)]
        public static void Build()
        {
            EnsureFolder("Assets/1Game/Resources", "MonsterPrefabs");
            EnsureFolder("Assets/1Game/Resources/MonsterPrefabs", "KayKit");

            RuntimeAnimatorController mediumController = CreateLocomotionController(
                $"{OutputFolder}/KayKit_Medium_Enemy.controller",
                $"{AnimationRoot}/Rig_Medium/General/Idle_A.anim",
                $"{AnimationRoot}/Rig_Medium/Movement Basic/Running_A.anim");
            RuntimeAnimatorController largeController = CreateLocomotionController(
                $"{OutputFolder}/KayKit_Large_Enemy.controller",
                $"{AnimationRoot}/Rig_Large/General/Idle_A.anim",
                $"{AnimationRoot}/Rig_Large/Movement Basic/Running_A.anim");

            var dayMelee = BuildVariant("Day_Melee_Knight", AdventurerRoot, "Knight",
                mediumController, ("sword_1handed", "handslot.r"), ("shield_badge", "handslot.l"));
            var dayRanged = BuildVariant("Day_Ranged_Rogue", AdventurerRoot, "Rogue",
                mediumController, ("crossbow_1handed", "handslot.r"));
            var dayCharger = BuildVariant("Day_Charger_RogueHooded", AdventurerRoot, "Rogue_Hooded",
                mediumController, ("dagger", "handslot.r"), ("dagger", "handslot.l"));
            var dayMage = BuildVariant("Day_Mage", AdventurerRoot, "Mage",
                mediumController, ("staff", "handslot.r"));
            var dayElite = BuildVariant("Day_Elite_Ranger", AdventurerRoot, "Ranger",
                mediumController, ("bow_withString", "handslot.l"));
            var dayBoss = BuildVariant("Day_Boss_BarbarianLarge", AdventurerRoot, "Barbarian_Large",
                largeController, ("axe_2handed_Large", "handslot.r"));

            var nightMelee = BuildVariant("Night_Melee_SkeletonWarrior", SkeletonRoot, "Skeleton_Warrior",
                mediumController, ("Skeleton_Blade", "handslot.r"), ("Skeleton_Shield_Small_A", "handslot.l"));
            var nightRanged = BuildVariant("Night_Ranged_SkeletonRogue", SkeletonRoot, "Skeleton_Rogue",
                mediumController, ("Skeleton_Crossbow", "handslot.r"));
            var nightCharger = BuildVariant("Night_Charger_SkeletonMinion", SkeletonRoot, "Skeleton_Minion",
                mediumController, ("Skeleton_Axe", "handslot.r"));
            var nightMage = BuildVariant("Night_Mage_SkeletonMage", SkeletonRoot, "Skeleton_Mage",
                mediumController, ("Skeleton_Staff", "handslot.r"));
            var nightElite = BuildVariant("Night_Elite_Necromancer", SkeletonRoot, "Necromancer",
                mediumController, ("Skeleton_Scythe", "handslot.r"));
            var nightBoss = BuildVariant("Night_Boss_SkeletonGolem", SkeletonRoot, "Skeleton_Golem",
                largeController, ("Skeleton_Golem_Axe_Large", "handslot.r"));

            var config = AssetDatabase.LoadAssetAtPath<MonsterPrefabs>(
                "Assets/1Game/Resources/MonsterPrefabs.asset");
            if (config == null)
                throw new FileNotFoundException("找不到 MonsterPrefabs.asset");

            config.普通小怪Prefab = dayMelee;
            config.远程敌人Prefab = dayRanged;
            config.冲锋敌人Prefab = dayCharger;
            config.法师敌人Prefab = dayMage;
            config.白昼精英Prefab = dayElite;
            config.Boss敌人Prefab = dayBoss;
            config.永夜普通小怪Prefab = nightMelee;
            config.永夜远程敌人Prefab = nightRanged;
            config.永夜冲锋敌人Prefab = nightCharger;
            config.永夜法师敌人Prefab = nightMage;
            config.永夜精英Prefab = nightElite;
            config.Boss_Act2_Prefab = nightBoss;
            config.Boss_Act3_Prefab = nightBoss;

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = config;
            Debug.Log("<color=#66ff99>[KayKit] 昼夜小怪、精英、Boss 与武器挂点已生成并接入。</color>");
        }

        private static GameObject BuildVariant(
            string outputName,
            string packageRoot,
            string characterName,
            RuntimeAnimatorController controller,
            params (string accessory, string socket)[] equipment)
        {
            var character = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{packageRoot}/Characters/{characterName}.prefab");
            if (character == null)
                throw new FileNotFoundException($"找不到 KayKit 角色：{characterName}");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(character);
            instance.name = outputName;
            var animator = instance.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
            }

            if (instance.GetComponent<KayKitLocomotionDriver>() == null)
                instance.AddComponent<KayKitLocomotionDriver>();

            foreach (var item in equipment)
                AttachAccessory(instance.transform, packageRoot, item.accessory, item.socket);

            string outputPath = $"{OutputFolder}/{outputName}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
            Object.DestroyImmediate(instance);
            if (saved == null)
                throw new IOException($"保存 KayKit 怪物 Prefab 失败：{outputPath}");
            return saved;
        }

        private static void AttachAccessory(
            Transform characterRoot,
            string packageRoot,
            string accessoryName,
            string socketName)
        {
            Transform socket = FindDeepChild(characterRoot, socketName);
            if (socket == null)
                throw new MissingReferenceException($"{characterRoot.name} 缺少挂点 {socketName}");

            var accessory = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{packageRoot}/Accessories/{accessoryName}.prefab");
            if (accessory == null)
                throw new FileNotFoundException($"找不到 KayKit 配件：{accessoryName}");

            var item = (GameObject)PrefabUtility.InstantiatePrefab(accessory, socket);
            item.name = accessoryName;
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
            item.transform.localScale = Vector3.one;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child;
            return null;
        }

        private static RuntimeAnimatorController CreateLocomotionController(
            string controllerPath,
            string idlePath,
            string runPath)
        {
            AssetDatabase.DeleteAsset(controllerPath);
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(idlePath);
            var run = AssetDatabase.LoadAssetAtPath<AnimationClip>(runPath);
            if (idle == null || run == null)
                throw new FileNotFoundException($"找不到 KayKit 移动动画：{idlePath} / {runPath}");

            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.CreateBlendTreeInController("Locomotion", out BlendTree blendTree);
            blendTree.blendType = BlendTreeType.Simple1D;
            blendTree.blendParameter = "Speed";
            blendTree.AddChild(idle, 0f);
            blendTree.AddChild(run, 0.1f);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
