using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>
    /// ???????????
    /// ?????????????F????????????
    /// ??F??????
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class SkillPickup : MonoBehaviour, IInteractable
    {
        // ===== IInteractable: unified F-key interaction router =====
        public Vector3 InteractionWorldPos => transform.position;
        public int InteractionPriority => 25;          // skill pickup: above item (20), below shop (40)
        public bool IsInteractionAvailable => !_pickedUp && _playerInRange;
        public bool IsRoutedActive { get; set; }

        [Header("????")]
        public SkillData skillData;

        [Header("??")]
        [SerializeField] private float bobSpeed = 1.5f;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float rotateSpeed = 60f;
        [SerializeField] private float interactRadius = 2.5f;

        // ??UI
        private GameObject _promptUI;
        private Text _promptText;
        private Text _skillInfoText;
        private bool _playerInRange;
        private PlayerCombat _nearbyPlayerCombat;
        private Vector3 _startPos;
        private bool _pickedUp;

        // ??????????
        private bool _waitingForSlotChoice;  // ??????????????
        private GameObject _slotChoiceUI;    // ????UI

        // ????
        private float _holdTimer;
        private const float HOLD_TO_DECOMPOSE = 1.5f; // ??1.5???
        private Image _holdProgressFill;

        // ??????????? Keyboard.current ??????????? pickup ??? Update ?????
        private Keyboard _keyboard;

        private void Awake()
        {
            _keyboard = Keyboard.current;
        }

        private void Start()
        {
            _startPos = transform.position;

            // ?????
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = interactRadius;

            // ???????????Cube?????
            SetupVisual();
        }

        private void Update()
        {
            if (_pickedUp) return;

            // ?????????????UI???????
            float newY = _startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(_startPos.x, newY, _startPos.z);
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);

            // ??UI??XZ?Y??????????
            if (_promptUI != null)
            {
                _promptUI.transform.position = new Vector3(_startPos.x, _startPos.y + 2.0f, _startPos.z);
            }

            // Sync prompt/F-handling with router-active state.
            // Once the slot-replace UI is open it is treated as a modal ?
            // we keep the prompt and hold-timer alive even if the router
            // momentarily picks something else (player must press Q/E/R or Esc).
            if (_playerInRange && IsRoutedActive)
            {
                if (_promptUI == null) ShowPrompt();
            }
            else
            {
                if (!_waitingForSlotChoice && _promptUI != null) HidePrompt();
                if (!_waitingForSlotChoice) _holdTimer = 0f;
            }

            // ????
            if (_playerInRange && _nearbyPlayerCombat != null && (IsRoutedActive || _waitingForSlotChoice))
            {
                // ??????????????????? current
                var kb = _keyboard ?? (_keyboard = Keyboard.current);
                if (kb == null) return;

                // ?????????Q/E/R??????
                if (_waitingForSlotChoice)
                {
                    if (kb.qKey.wasPressedThisFrame) ConfirmSlotReplace(0);
                    else if (kb.eKey.wasPressedThisFrame) ConfirmSlotReplace(1);
                    else if (kb.rKey.wasPressedThisFrame) ConfirmSlotReplace(2);
                    else if (kb.escapeKey.wasPressedThisFrame) CancelSlotChoice();
                    return;
                }

                if (kb.fKey.isPressed)
                {
                    _holdTimer += Time.deltaTime;

                    // ??????
                    if (_holdProgressFill != null)
                        _holdProgressFill.fillAmount = _holdTimer / HOLD_TO_DECOMPOSE;

                    // ????
                    if (_holdTimer >= HOLD_TO_DECOMPOSE)
                    {
                        Decompose();
                        return;
                    }
                }

                if (kb.fKey.wasReleasedThisFrame && _holdTimer < HOLD_TO_DECOMPOSE)
                {
                    // ????
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

        /// <summary>?????? ? ??????????????????UI???????????</summary>
        private void TryPickup()
        {
            if (skillData == null || _nearbyPlayerCombat == null) return;

            // ??????
            int emptySlot = _nearbyPlayerCombat.FindEmptySlot();
            if (emptySlot >= 0)
            {
                _nearbyPlayerCombat.EquipSkillToSlot(skillData, emptySlot);
                string slotName = GetSlotKeyName(emptySlot);
                Debug.Log($"<color=cyan>?????{skillData.skillName} ? {slotName}??</color>");

                GameEvents.Publish(new GameEvents.SkillEquipped
                {
                    Skill = skillData,
                    SlotIndex = emptySlot
                });

                OnPickedUp();
            }
            else
            {
                // ???? ? ???????????Q/E/R????????
                ShowSlotChoiceUI();
            }
        }

        /// <summary>??????UI</summary>
        private void ShowSlotChoiceUI()
        {
            if (_waitingForSlotChoice) return;
            _waitingForSlotChoice = true;

            // ??????
            if (_promptText != null)
                _promptText.text = "???????[Q] [E] [R]  |  [Esc] ??";

            // ????????
            var canvasGo = new GameObject("SlotChoiceCanvas");
            canvasGo.transform.position = new Vector3(_startPos.x, _startPos.y + 3.2f, _startPos.z);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 201;
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(500, 80);
            rt.localScale = Vector3.one * 0.00875f;

            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.05f, 0.05f, 0.9f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8, 4);
            textRt.offsetMax = new Vector2(-8, -4);
            var text = textGo.AddComponent<Text>();

            // ??????????
            string qName = _nearbyPlayerCombat.GetSkillInSlot(0)?.skillName ?? "?";
            string eName = _nearbyPlayerCombat.GetSkillInSlot(1)?.skillName ?? "?";
            string rName = _nearbyPlayerCombat.GetSkillInSlot(2)?.skillName ?? "?";
            text.text = $"[Q]{qName}  [E]{eName}  [R]{rName}  [Esc]??";
            text.fontSize = 22;
            text.font = UIBuiltins.LegacyFont;
            text.color = new Color(1f, 0.8f, 0.3f, 1f);
            text.alignment = TextAnchor.MiddleCenter;

            canvasGo.AddComponent<BillboardUI>().lerpFactor = 0.5f;
            _slotChoiceUI = canvasGo;
        }

        /// <summary>????????</summary>
        private void ConfirmSlotReplace(int slotIndex)
        {
            if (_nearbyPlayerCombat == null) return;

            SkillData oldSkill = _nearbyPlayerCombat.EquipSkillToSlot(skillData, slotIndex);
            string slotName = GetSlotKeyName(slotIndex);
            Debug.Log($"<color=cyan>?????{skillData.skillName} ? {slotName}?????{oldSkill?.skillName ?? "?"}?</color>");

            GameEvents.Publish(new GameEvents.SkillEquipped
            {
                Skill = skillData,
                SlotIndex = slotIndex
            });

            // ????????
            if (oldSkill != null)
            {
                Vector3 dropPos = transform.position + Random.insideUnitSphere * 1.5f;
                dropPos.y = _startPos.y;
                Spawn(oldSkill, dropPos);
            }

            HideSlotChoiceUI();
            OnPickedUp();
        }

        /// <summary>??????</summary>
        private void CancelSlotChoice()
        {
            _waitingForSlotChoice = false;
            HideSlotChoiceUI();
            // ??????
            if (_promptText != null)
            {
                int shards = PlayerResources.GetDecomposeShards(skillData.rarity);
                _promptText.text = $"[F] ??  |  ??[F] ????{shards}?";
            }
        }

        private void HideSlotChoiceUI()
        {
            if (_slotChoiceUI != null)
            {
                Destroy(_slotChoiceUI);
                _slotChoiceUI = null;
            }
        }

        /// <summary>???????F?</summary>
        private void Decompose()
        {
            if (skillData == null) return;

            // ????????
            int shards = PlayerResources.GetDecomposeShards(skillData.rarity);
            if (PlayerResources.Instance != null)
                PlayerResources.Instance.AddShards(shards);

            Debug.Log($"<color=yellow>?????{skillData.skillName} ? ?? {shards} ????</color>");

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

        // ==================== ??UI ====================

        private void OnTriggerEnter(Collider other)
        {
            if (_pickedUp) return;
            if (!other.CompareTag("Player")) return;

            _nearbyPlayerCombat = other.GetComponent<PlayerCombat>();
            if (_nearbyPlayerCombat == null) return;

            _playerInRange = true;
            InteractionRouter.Register(this);
            // Whether the prompt actually shows is decided each frame in Update
            // by checking IsRoutedActive (so overlapping shop/skill behave correctly).
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _playerInRange = false;
            _nearbyPlayerCombat = null;
            _holdTimer = 0f;
            _waitingForSlotChoice = false;
            InteractionRouter.Unregister(this);
            HideSlotChoiceUI();
            HidePrompt();
        }

        private void ShowPrompt()
        {
            if (_promptUI != null) return;
            if (skillData == null) return;

            // ????????UI
            var canvasGo = new GameObject("SkillPromptCanvas");
            // ????????????????
            canvasGo.transform.position = new Vector3(_startPos.x, _startPos.y + 2.0f, _startPos.z);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(500, 220);
            rt.localScale = Vector3.one * 0.00875f;

            // ??
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);

            // ????
            var nameGo = new GameObject("SkillName");
            nameGo.transform.SetParent(canvasGo.transform, false);
            var nameRt = nameGo.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0, 0.65f);
            nameRt.anchorMax = new Vector2(1, 1);
            nameRt.offsetMin = new Vector2(8, 0);
            nameRt.offsetMax = new Vector2(-8, -4);
            _skillInfoText = nameGo.AddComponent<Text>();
            _skillInfoText.text = $"{skillData.skillName}?{GetRarityName(skillData.rarity)}?";
            _skillInfoText.fontSize = 28;
            _skillInfoText.font = UIBuiltins.LegacyFont;
            _skillInfoText.color = GetRarityColor(skillData.rarity);
            _skillInfoText.alignment = TextAnchor.MiddleCenter;
            _skillInfoText.fontStyle = FontStyle.Bold;
            // ????????
            var nameOutline = nameGo.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0, 0, 0, 0.9f);
            nameOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // ??????
            string typeStr = skillData.skillType switch
            {
                SkillType.AreaDamage => "????",
                SkillType.Projectile => "???",
                SkillType.Dash => "??",
                SkillType.Buff => "??",
                SkillType.Heal => "??",
                SkillType.Summon => "??",
                _ => "??"
            };
            var typeGo = new GameObject("Type");
            typeGo.transform.SetParent(canvasGo.transform, false);
            var typeRt = typeGo.AddComponent<RectTransform>();
            typeRt.anchorMin = new Vector2(0, 0.55f);
            typeRt.anchorMax = new Vector2(1, 0.65f);
            typeRt.offsetMin = new Vector2(8, 0);
            typeRt.offsetMax = new Vector2(-8, 0);
            var typeText = typeGo.AddComponent<Text>();
            typeText.text = $"???{typeStr}  |  CD?{skillData.cooldown}s  |  ???{skillData.baseDamage}";
            typeText.fontSize = 18;
            typeText.font = UIBuiltins.LegacyFont;
            typeText.color = new Color(0.5f, 0.9f, 0.5f, 0.9f);
            typeText.alignment = TextAnchor.MiddleCenter;

            // ??
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
            descText.font = UIBuiltins.LegacyFont;
            descText.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
            descText.alignment = TextAnchor.MiddleCenter;

            // ????
            var promptGo = new GameObject("Prompt");
            promptGo.transform.SetParent(canvasGo.transform, false);
            var promptRt = promptGo.AddComponent<RectTransform>();
            promptRt.anchorMin = new Vector2(0, 0);
            promptRt.anchorMax = new Vector2(1, 0.3f);
            promptRt.offsetMin = new Vector2(8, 4);
            promptRt.offsetMax = new Vector2(-8, 0);
            _promptText = promptGo.AddComponent<Text>();
            int shards = PlayerResources.GetDecomposeShards(skillData.rarity);
            _promptText.text = $"[F] ??  |  ??[F] ????{shards}?";
            _promptText.fontSize = 18;
            _promptText.font = UIBuiltins.LegacyFont;
            _promptText.color = new Color(0.6f, 0.8f, 1f, 0.9f);
            _promptText.alignment = TextAnchor.MiddleCenter;

            // ???????
            var holdBgGo = new GameObject("HoldBg");
            holdBgGo.transform.SetParent(canvasGo.transform, false);
            var holdBgRt = holdBgGo.AddComponent<RectTransform>();
            holdBgRt.anchorMin = new Vector2(0.1f, 0.02f);
            holdBgRt.anchorMax = new Vector2(0.9f, 0.08f);
            holdBgRt.offsetMin = Vector2.zero;
            holdBgRt.offsetMax = Vector2.zero;
            var holdBgImg = holdBgGo.AddComponent<Image>();
            holdBgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);

            // ???????
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

            // ???UI?????????????????
            var billboard = canvasGo.AddComponent<BillboardUI>();
            billboard.lerpFactor = 0.5f;

            _promptUI = canvasGo;
        }

        private void HidePrompt()
        {
            if (_promptUI != null)
            {
                Destroy(_promptUI);
                _promptUI = null;
            }
        }

        private void OnDestroy()
        {
            InteractionRouter.Unregister(this);
            HidePrompt();
            HideSlotChoiceUI();
        }

        private void SetupVisual()
        {
            if (skillData == null) return;

            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                // ???? + MPB???????? new Material??????????? GPU ???? / ?????
                Color rarityColor = GetRarityColor(skillData.rarity);
                MaterialHelper.ApplyEmissiveColor(renderer, rarityColor, rarityColor * 0.8f);
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
                ItemRarity.Fan => "??",
                ItemRarity.Ling => "??",
                ItemRarity.Xuan => "??",
                ItemRarity.Di => "??",
                ItemRarity.Tian => "??",
                _ => "??"
            };
        }

        // ==================== ???? ====================

        /// <summary>
        /// ????????????
        /// </summary>
        public static SkillPickup Spawn(SkillData data, Vector3 position)
        {
            if (data == null) return null;

            // ????Cube????/??
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"SkillPickup_{data.skillName}";
            go.transform.position = position + Vector3.up * 0.15f;
            go.transform.localScale = new Vector3(0.5f, 0.1f, 0.35f);
            go.layer = LayerMask.NameToLayer("Default");

            // ????BoxCollider???SphereCollider
            var boxCol = go.GetComponent<BoxCollider>();
            if (boxCol != null) Object.Destroy(boxCol);
            go.AddComponent<SphereCollider>();

            var pickup = go.AddComponent<SkillPickup>();
            pickup.skillData = data;

            return pickup;
        }
    }

    /// <summary>
    /// Billboard????UI??????????????????????
    /// lerpFactor ???????0=????????1=??????
    /// </summary>
    public class BillboardUI : MonoBehaviour
    {
        [Tooltip("??????????0.4=???????0.7=???????")]
        public float lerpFactor = 0.5f;

        private Quaternion _initialRotation;
        private bool _initialized;

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            if (!_initialized)
            {
                _initialRotation = transform.rotation;
                _initialized = true;
            }

            // ?????????
            Quaternion lookAtCam = Quaternion.LookRotation(transform.position - cam.transform.position);
            // ??????????????
            transform.rotation = Quaternion.Slerp(_initialRotation, lookAtCam, lerpFactor);
        }
    }
}
