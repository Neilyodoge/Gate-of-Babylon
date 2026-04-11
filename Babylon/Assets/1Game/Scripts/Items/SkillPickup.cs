using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>
    /// 功法（技能）地面拾取物
    /// 靠近时显示技能信息提示，按F拾取自动装备到第一个空位
    /// 长按F分解获得资源
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class SkillPickup : MonoBehaviour
    {
        [Header("功法数据")]
        public SkillData skillData;

        [Header("表现")]
        [SerializeField] private float bobSpeed = 1.5f;
        [SerializeField] private float bobHeight = 0.4f;
        [SerializeField] private float rotateSpeed = 60f;
        [SerializeField] private float interactRadius = 2.5f;

        // 提示UI
        private GameObject _promptUI;
        private Text _promptText;
        private Text _skillInfoText;
        private bool _playerInRange;
        private PlayerCombat _nearbyPlayerCombat;
        private Vector3 _startPos;
        private bool _pickedUp;

        // 长按分解
        private float _holdTimer;
        private const float HOLD_TO_DECOMPOSE = 1.5f; // 长按1.5秒分解
        private Image _holdProgressFill;

        private void Start()
        {
            _startPos = transform.position;

            // 设置触发器
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = interactRadius;

            // 设置显示（用书卷形状的Cube表示功法）
            SetupVisual();
        }

        private void Update()
        {
            if (_pickedUp) return;

            // 浮动动画
            float newY = _startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(_startPos.x, newY, _startPos.z);
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);

            // 交互逻辑
            if (_playerInRange && _nearbyPlayerCombat != null)
            {
                var kb = Keyboard.current;
                if (kb == null) return;

                if (kb.fKey.isPressed)
                {
                    _holdTimer += Time.deltaTime;

                    // 更新长按进度
                    if (_holdProgressFill != null)
                        _holdProgressFill.fillAmount = _holdTimer / HOLD_TO_DECOMPOSE;

                    // 长按分解
                    if (_holdTimer >= HOLD_TO_DECOMPOSE)
                    {
                        Decompose();
                        return;
                    }
                }

                if (kb.fKey.wasReleasedThisFrame && _holdTimer < HOLD_TO_DECOMPOSE)
                {
                    // 短按拾取
                    TryPickup();
                }

                if (!kb.fKey.isPressed)
                {
                    _holdTimer = 0f;
                    if (_holdProgressFill != null)
                        _holdProgressFill.fillAmount = 0f;
                }
            }
        }

        /// <summary>尝试拾取功法 → 自动装备到第一个空位</summary>
        private void TryPickup()
        {
            if (skillData == null || _nearbyPlayerCombat == null) return;

            // 先找空闲槽位
            int emptySlot = _nearbyPlayerCombat.FindEmptySlot();
            if (emptySlot >= 0)
            {
                _nearbyPlayerCombat.EquipSkillToSlot(skillData, emptySlot);
                string slotName = GetSlotKeyName(emptySlot);
                Debug.Log($"<color=cyan>装备功法：{skillData.skillName} → {slotName}槽位</color>");

                GameEvents.Publish(new GameEvents.SkillEquipped
                {
                    Skill = skillData,
                    SlotIndex = emptySlot
                });

                OnPickedUp();
            }
            else
            {
                // 满了 → 替换R槽位，旧技能掉落
                int replaceSlot = 2;
                SkillData oldSkill = _nearbyPlayerCombat.EquipSkillToSlot(skillData, replaceSlot);

                string slotName = GetSlotKeyName(replaceSlot);
                Debug.Log($"<color=cyan>替换功法：{skillData.skillName} → {slotName}槽位（{oldSkill?.skillName ?? "空"} 被替换）</color>");

                GameEvents.Publish(new GameEvents.SkillEquipped
                {
                    Skill = skillData,
                    SlotIndex = replaceSlot
                });

                if (oldSkill != null)
                {
                    Vector3 dropPos = transform.position + Random.insideUnitSphere * 1.5f;
                    dropPos.y = _startPos.y;
                    Spawn(oldSkill, dropPos);
                }

                OnPickedUp();
            }
        }

        /// <summary>分解功法（长按F）</summary>
        private void Decompose()
        {
            if (skillData == null) return;

            // 分解获得灵力碎片
            int shards = PlayerResources.GetDecomposeShards(skillData.rarity);
            if (PlayerResources.Instance != null)
                PlayerResources.Instance.AddShards(shards);

            Debug.Log($"<color=yellow>分解功法：{skillData.skillName} → 获得 {shards} 灵力碎片</color>");

            GameEvents.Publish(new GameEvents.SkillDecomposed
            {
                Skill = skillData
            });

            OnPickedUp();
        }

        private void OnPickedUp()
        {
            _pickedUp = true;
            HidePrompt();
            Destroy(gameObject);
        }

        private string GetSlotKeyName(int slotIndex)
        {
            return slotIndex switch
            {
                0 => "Q",
                1 => "E",
                2 => "R",
                _ => "?"
            };
        }

        // ==================== 提示UI ====================

        private void OnTriggerEnter(Collider other)
        {
            if (_pickedUp) return;
            if (!other.CompareTag("Player")) return;

            _nearbyPlayerCombat = other.GetComponent<PlayerCombat>();
            if (_nearbyPlayerCombat == null) return;

            _playerInRange = true;
            ShowPrompt();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _playerInRange = false;
            _nearbyPlayerCombat = null;
            _holdTimer = 0f;
            HidePrompt();
        }

        private void ShowPrompt()
        {
            if (_promptUI != null) return;
            if (skillData == null) return;

            // 创建世界空间提示UI
            var canvasGo = new GameObject("SkillPromptCanvas");
            canvasGo.transform.SetParent(transform);
            canvasGo.transform.localPosition = new Vector3(0, 3.0f, 0);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(500, 220);
            rt.localScale = Vector3.one * 0.035f;

            // 背景
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);

            // 技能名称
            var nameGo = new GameObject("SkillName");
            nameGo.transform.SetParent(canvasGo.transform, false);
            var nameRt = nameGo.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0, 0.65f);
            nameRt.anchorMax = new Vector2(1, 1);
            nameRt.offsetMin = new Vector2(8, 0);
            nameRt.offsetMax = new Vector2(-8, -4);
            _skillInfoText = nameGo.AddComponent<Text>();
            _skillInfoText.text = $"{skillData.skillName}（{GetRarityName(skillData.rarity)}）";
            _skillInfoText.fontSize = 28;
            _skillInfoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _skillInfoText.color = GetRarityColor(skillData.rarity);
            _skillInfoText.alignment = TextAnchor.MiddleCenter;
            _skillInfoText.fontStyle = FontStyle.Bold;
            // 描边让文字更清晰
            var nameOutline = nameGo.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0, 0, 0, 0.9f);
            nameOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // 技能类型标签
            string typeStr = skillData.skillType switch
            {
                SkillType.AreaDamage => "范围伤害",
                SkillType.Projectile => "投射物",
                SkillType.Dash => "位移",
                SkillType.Buff => "增益",
                _ => "未知"
            };
            var typeGo = new GameObject("Type");
            typeGo.transform.SetParent(canvasGo.transform, false);
            var typeRt = typeGo.AddComponent<RectTransform>();
            typeRt.anchorMin = new Vector2(0, 0.55f);
            typeRt.anchorMax = new Vector2(1, 0.65f);
            typeRt.offsetMin = new Vector2(8, 0);
            typeRt.offsetMax = new Vector2(-8, 0);
            var typeText = typeGo.AddComponent<Text>();
            typeText.text = $"类型：{typeStr}  |  CD：{skillData.cooldown}s  |  伤害：{skillData.baseDamage}";
            typeText.fontSize = 18;
            typeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            typeText.color = new Color(0.5f, 0.9f, 0.5f, 0.9f);
            typeText.alignment = TextAnchor.MiddleCenter;

            // 描述
            var descGo = new GameObject("Desc");
            descGo.transform.SetParent(canvasGo.transform, false);
            var descRt = descGo.AddComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0, 0.3f);
            descRt.anchorMax = new Vector2(1, 0.65f);
            descRt.offsetMin = new Vector2(8, 0);
            descRt.offsetMax = new Vector2(-8, 0);
            var descText = descGo.AddComponent<Text>();
            descText.text = skillData.description;
            descText.fontSize = 18;
            descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            descText.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
            descText.alignment = TextAnchor.MiddleCenter;

            // 操作提示
            var promptGo = new GameObject("Prompt");
            promptGo.transform.SetParent(canvasGo.transform, false);
            var promptRt = promptGo.AddComponent<RectTransform>();
            promptRt.anchorMin = new Vector2(0, 0);
            promptRt.anchorMax = new Vector2(1, 0.3f);
            promptRt.offsetMin = new Vector2(8, 4);
            promptRt.offsetMax = new Vector2(-8, 0);
            _promptText = promptGo.AddComponent<Text>();
            int shards = PlayerResources.GetDecomposeShards(skillData.rarity);
            _promptText.text = $"[F] 拾取  |  长按[F] 分解（✦{shards}）";
            _promptText.fontSize = 18;
            _promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _promptText.color = new Color(0.6f, 0.8f, 1f, 0.9f);
            _promptText.alignment = TextAnchor.MiddleCenter;

            // 长按进度条背景
            var holdBgGo = new GameObject("HoldBg");
            holdBgGo.transform.SetParent(canvasGo.transform, false);
            var holdBgRt = holdBgGo.AddComponent<RectTransform>();
            holdBgRt.anchorMin = new Vector2(0.1f, 0.02f);
            holdBgRt.anchorMax = new Vector2(0.9f, 0.08f);
            holdBgRt.offsetMin = Vector2.zero;
            holdBgRt.offsetMax = Vector2.zero;
            var holdBgImg = holdBgGo.AddComponent<Image>();
            holdBgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);

            // 长按进度条填充
            var holdFillGo = new GameObject("HoldFill");
            holdFillGo.transform.SetParent(holdBgGo.transform, false);
            var holdFillRt = holdFillGo.AddComponent<RectTransform>();
            holdFillRt.anchorMin = Vector2.zero;
            holdFillRt.anchorMax = Vector2.one;
            holdFillRt.offsetMin = Vector2.zero;
            holdFillRt.offsetMax = Vector2.zero;
            _holdProgressFill = holdFillGo.AddComponent<Image>();
            _holdProgressFill.color = new Color(1f, 0.4f, 0.2f, 0.8f);
            _holdProgressFill.type = Image.Type.Filled;
            _holdProgressFill.fillMethod = Image.FillMethod.Horizontal;
            _holdProgressFill.fillAmount = 0f;

            _promptUI = canvasGo;

            // 让提示面板始终面向相机
            canvasGo.AddComponent<BillboardUI>();
        }

        private void HidePrompt()
        {
            if (_promptUI != null)
            {
                Destroy(_promptUI);
                _promptUI = null;
            }
        }

        // ==================== 视觉表现 ====================

        private void SetupVisual()
        {
            if (skillData == null) return;

            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                Color rarityColor = GetRarityColor(skillData.rarity);
                mat.color = rarityColor;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", rarityColor * 0.8f);
                renderer.material = mat;
            }
        }

        private Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Fan => Color.white,
                ItemRarity.Ling => Color.green,
                ItemRarity.Xuan => new Color(0.3f, 0.5f, 1f),
                ItemRarity.Di => new Color(0.7f, 0.3f, 1f),
                ItemRarity.Tian => new Color(1f, 0.85f, 0f),
                _ => Color.white
            };
        }

        private string GetRarityName(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Fan => "凡品",
                ItemRarity.Ling => "灵品",
                ItemRarity.Xuan => "玄品",
                ItemRarity.Di => "地品",
                ItemRarity.Tian => "天品",
                _ => "凡品"
            };
        }

        // ==================== 工厂方法 ====================

        /// <summary>
        /// 在指定位置生成功法拾取物
        /// </summary>
        public static SkillPickup Spawn(SkillData data, Vector3 position)
        {
            if (data == null) return null;

            // 用扁平的Cube表示书卷/功法
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"SkillPickup_{data.skillName}";
            go.transform.position = position + Vector3.up * 0.5f;
            go.transform.localScale = new Vector3(0.5f, 0.1f, 0.35f);
            go.layer = LayerMask.NameToLayer("Default");

            // 移除默认BoxCollider，添加SphereCollider
            var boxCol = go.GetComponent<BoxCollider>();
            if (boxCol != null) Object.Destroy(boxCol);
            go.AddComponent<SphereCollider>();

            var pickup = go.AddComponent<SkillPickup>();
            pickup.skillData = data;

            return pickup;
        }
    }

    /// <summary>
    /// 简单的Billboard组件，让UI始终面向相机
    /// </summary>
    public class BillboardUI : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
