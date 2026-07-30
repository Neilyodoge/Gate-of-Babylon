using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// Gameplay 类别构建器：临时地面 + 玩家。
    /// 挂在场景「Gameplay」根节点上，由 <see cref="Demo1Setup"/> 按序调用；
    /// 生成的对象统一挂到本节点下，便于在 Hierarchy 按类别查看。
    /// </summary>
    public class GameplayBuilder : MonoBehaviour
    {
        /// <summary>临时地面，防止房间生成前玩家掉落（1 秒后销毁）。</summary>
        public void BuildGround()
        {
            var tempGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
            tempGround.name = "TempGround";
            tempGround.transform.SetParent(transform, false);
            tempGround.transform.position = Vector3.zero;
            tempGround.transform.localScale = Vector3.one;
            var renderer = tempGround.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.12f, 0.14f, 0.18f);
                renderer.material = mat;
            }
            Destroy(tempGround, 1f);
        }

        /// <summary>创建玩家（含模型 / 攻击原点 / 战斗组件 / 主角档案热构建）。</summary>
        public void BuildPlayer(GameObject playerModelPrefab, RuntimeAnimatorController animatorController,
            GameObject slashVFXPrefab, GameObject hitVFXPrefab,
            SkillData testSkillQ, SkillData testSkillE, SkillData testSkillR)
        {
            var playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            playerGo.transform.SetParent(transform, false);
            playerGo.transform.position = new Vector3(0, 0, 0);

            var cc = playerGo.AddComponent<CharacterController>();
            cc.radius = 0.3f;
            cc.height = 1.8f;
            cc.center = new Vector3(0, 0.9f, 0);

            // 优先走主角档案系统（PlayerCharacterProfile）：若已选档案且带模型，
            // 则跳过这里的序列化模型构建，改由 PlayerController.ApplyCharacterProfile 在组件就绪后热构建。
            var selectedProfile = PlayerCharacterRegistry.Selected;
            bool useProfile = selectedProfile != null && selectedProfile.modelPrefab != null;

            Transform modelTransform = null;
            Animator modelAnimator = null;

            if (useProfile)
            {
                // 模型延迟到组件挂载后由 ApplyCharacterProfile 构建
            }
            else if (playerModelPrefab != null)
            {
                var model = Instantiate(playerModelPrefab, playerGo.transform);
                model.name = "PlayerModel";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                modelTransform = model.transform;
                modelAnimator = model.GetComponentInChildren<Animator>();

                if (animatorController != null && modelAnimator != null)
                    modelAnimator.runtimeAnimatorController = animatorController;

                if (modelAnimator != null)
                    modelAnimator.applyRootMotion = false;
            }
            else
            {
                // 回退：胶囊体
                var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                model.name = "PlayerModel";
                model.transform.SetParent(playerGo.transform);
                model.transform.localPosition = new Vector3(0, 1f, 0);
                model.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

                var modelCol = model.GetComponent<Collider>();
                if (modelCol != null) Destroy(modelCol);

                var rend = model.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = new Material(MaterialHelper.GetLitShader());
                    mat.color = new Color(0.3f, 0.6f, 1f);
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.1f, 0.2f, 0.4f));
                    rend.material = mat;
                }

                var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
                indicator.name = "DirectionIndicator";
                indicator.transform.SetParent(model.transform);
                indicator.transform.localPosition = new Vector3(0, 0, 0.8f);
                indicator.transform.localScale = new Vector3(0.3f, 0.3f, 0.5f);
                var indCol = indicator.GetComponent<Collider>();
                if (indCol != null) Destroy(indCol);
                var indRenderer = indicator.GetComponent<Renderer>();
                if (indRenderer != null)
                {
                    var mat = new Material(MaterialHelper.GetLitShader());
                    mat.color = new Color(1f, 0.8f, 0.2f);
                    indRenderer.material = mat;
                }

                modelTransform = model.transform;
            }

            // 攻击原点 & 刀光生成点
            var attackOrigin = new GameObject("AttackOrigin");
            attackOrigin.transform.SetParent(playerGo.transform);
            attackOrigin.transform.localPosition = new Vector3(0, 0.9f, 0.6f);

            var slashSpawnPoint = new GameObject("SlashVFXPoint");
            slashSpawnPoint.transform.SetParent(playerGo.transform);
            slashSpawnPoint.transform.localPosition = new Vector3(0, 1.0f, 0.8f);

            var playerCtrl = playerGo.AddComponent<PlayerController>();
            playerCtrl.SetModelTransform(modelTransform);

            var playerAnim = playerGo.AddComponent<PlayerAnimator>();
            if (modelAnimator != null)
            {
                playerAnim.SetAnimator(modelAnimator);

                var animatorGo = modelAnimator.gameObject;
                if (animatorGo.GetComponent<AnimationEventRelay>() == null)
                    animatorGo.AddComponent<AnimationEventRelay>();
            }

            var combat = playerGo.AddComponent<PlayerCombat>();
            combat.SetAttackOrigin(attackOrigin.transform);
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            if (enemyLayerIndex >= 0)
                combat.SetEnemyLayer(1 << enemyLayerIndex);
            else
                combat.SetEnemyLayer(LayerMask.GetMask("Default"));

            if (slashVFXPrefab != null)
                combat.SetSlashVFX(slashVFXPrefab, slashSpawnPoint.transform);
            if (hitVFXPrefab != null)
                combat.SetHitVFX(hitVFXPrefab);

            // V0.4：玩家初始无技能，Q/E/R 全部留空；仅保留 Inspector 手动配置的测试技能。
            if (testSkillQ != null) combat.EquipSkillQ(testSkillQ);
            if (testSkillE != null) combat.EquipSkillE(testSkillE);
            if (testSkillR != null) combat.EquipSkillR(testSkillR);
            playerGo.AddComponent<PlayerResources>();

            if (useProfile)
                playerCtrl.ApplyCharacterProfile(selectedProfile);
        }
    }
}
