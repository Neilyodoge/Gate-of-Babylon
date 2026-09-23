using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    public enum StarterSpiritTrialStage
    {
        Inactive = 0,
        ChooseFirstAssignment = 1,
        VerifyFirstAssignment = 2,
        ChooseDifferentCarrier = 3,
        VerifyDifferentCarrier = 4,
        ChooseDifferentSpecies = 5,
        VerifyDifferentSpecies = 6,
        FreeTrial = 7,
        Committed = 8
    }

    /// <summary>
    /// 初契前赋予教学：强引导完成首次赋予，随后保持面板常驻，
    /// 使用三种不同组合即可结契。试玩状态不写存档。
    /// </summary>
    public sealed class StarterSpiritTrialController : MonoBehaviour
    {
        public const int RequiredCombinationCount = 3;

        private readonly HashSet<string> _completed = new();
        private readonly List<GameObject> _targets = new();
        private Action<StableConfigId, SpiritInstanceState> _onChosen;
        private StarterSpiritCarrierController _carrier;
        private StarterSpiritTrialHUD _hud;
        private StarterSpiritChoiceWorldEntity[] _entities;
        private int _verificationProgress;
        private int _hintLevel;
        private bool _transitioning;
        private bool _confirming;
        private bool _assistArmed;
        private bool _hasComparisonAnchor;
        private StableConfigId _comparisonSpecies;
        private CarrierSlot _comparisonCarrier;
        private StableConfigId _currentSpecies;
        private CarrierSlot _currentCarrier = CarrierSlot.Weapon;
        private StableConfigId _pendingSpecies;
        private CarrierSlot? _pendingCarrier;
        private StableConfigId _previousSpecies;
        private CarrierSlot _previousCarrier;
        private float _verificationStartedAt;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;
        private bool _cursorCaptured;

        public StarterSpiritTrialStage Stage { get; private set; }
        public StableConfigId CurrentSpecies => _currentSpecies;
        public CarrierSlot CurrentCarrier => _currentCarrier;
        public StableConfigId PendingSpecies => _pendingSpecies;
        public CarrierSlot? PendingCarrier => _pendingCarrier;

        public static bool Begin(
            Transform trialOrigin,
            Action<StableConfigId, SpiritInstanceState> onChosen)
        {
            if (FindObjectOfType<StarterSpiritTrialController>() != null)
                return false;
            GameObject go = new("StarterSpiritTrialController");
            if (trialOrigin != null)
                go.transform.position = trialOrigin.position;
            StarterSpiritTrialController controller =
                go.AddComponent<StarterSpiritTrialController>();
            controller._onChosen = onChosen;
            controller.StartCoroutine(controller.BeginRoutine());
            return true;
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<
                GameEvents.StarterSpiritCarrierAdvanced>(
                OnCarrierAdvanced);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<
                GameEvents.StarterSpiritCarrierAdvanced>(
                OnCarrierAdvanced);
        }

        private IEnumerator BeginRoutine()
        {
            while (PlayerController.Instance == null)
                yield return null;
            _carrier = PlayerController.Instance.GetComponent<
                StarterSpiritCarrierController>();
            if (_carrier == null)
            {
                Debug.LogError("[初契试玩] 玩家缺少单宠附着控制器。");
                Destroy(gameObject);
                yield break;
            }

            if (!FeatureFlags.EnableCircuitRuntime)
            {
                Debug.LogWarning(
                    "[赋予教学] 回路运行时未启用，回退原初契界面。");
                StarterSpiritChoiceUI.ShowForScene(_onChosen);
                Destroy(gameObject);
                yield break;
            }

            _entities = FindObjectsOfType<
                StarterSpiritChoiceWorldEntity>(true);
            EnterCandidatePresentation();
            _hud = StarterSpiritTrialHUD.Create();
            _hud.SpeciesRequested += SelectSpecies;
            _hud.CarrierRequested += SelectCarrier;
            _hud.AssignmentRequested += RequestAssignment;
            _hud.CommitRequested += RequestCommit;
            CreateTargets();
            CaptureCursor();
            Stage = StarterSpiritTrialStage.ChooseFirstAssignment;
            _pendingSpecies = default;
            _pendingCarrier = null;
            RefreshSelectionHud();
            PublishObjective("任选一只灵宠，再选择要让它改变的动作");
        }

        private void Update()
        {
            if (!IsVerificationStage(Stage) || _transitioning)
                return;

            float elapsed = Time.unscaledTime - _verificationStartedAt;
            int nextHintLevel = elapsed >= 45f
                ? 4
                : elapsed >= 25f
                    ? 3
                    : elapsed >= 15f
                        ? 2
                        : elapsed >= 8f
                            ? 1
                            : 0;
            if (nextHintLevel == _hintLevel)
                return;

            _hintLevel = nextHintLevel;
            _assistArmed |= _hintLevel >= 4;
            RefreshVerificationHud();
        }

        private void OnCarrierAdvanced(
            GameEvents.StarterSpiritCarrierAdvanced evt)
        {
            if (evt.SpeciesId != _currentSpecies ||
                evt.Carrier != _currentCarrier)
            {
                return;
            }

            _verificationProgress = Mathf.Max(
                _verificationProgress,
                evt.Progress);
            if (evt.Activated)
            {
                if (!_hasComparisonAnchor)
                {
                    _hasComparisonAnchor = true;
                    _comparisonSpecies = evt.SpeciesId;
                    _comparisonCarrier = evt.Carrier;
                }
                _completed.Add(
                    StarterSpiritTrialHUD.Key(
                        evt.SpeciesId,
                        evt.Carrier));
            }

            if (IsVerificationStage(Stage))
            {
                if (!evt.Activated && _assistArmed)
                {
                    _assistArmed = false;
                    _carrier.ForceStarterTrialActivation(
                        TargetCenter());
                    return;
                }

                RefreshVerificationHud();
                if (evt.Activated && !_transitioning)
                {
                    _transitioning = true;
                    StartCoroutine(
                        AdvanceGuidedStepAfterFeedback(Stage));
                }
                return;
            }

            if (Stage == StarterSpiritTrialStage.FreeTrial)
            {
                RefreshFreeHud();
                PublishPracticeObjective();
            }
        }

        private IEnumerator AdvanceGuidedStepAfterFeedback(
            StarterSpiritTrialStage completedStage)
        {
            _hud.ShowGuidedSuccess(
                GuidedStepFor(completedStage),
                _previousSpecies,
                _previousCarrier,
                _currentSpecies,
                _currentCarrier);
            yield return new WaitForSeconds(1.35f);

            _transitioning = false;
            switch (completedStage)
            {
                case StarterSpiritTrialStage.VerifyFirstAssignment:
                    Stage = StarterSpiritTrialStage
                        .ChooseDifferentCarrier;
                    _pendingSpecies = _currentSpecies;
                    _pendingCarrier = _currentCarrier;
                    PublishObjective(
                        "保持当前灵宠，把它改附到另一个动作");
                    RefreshSelectionHud();
                    break;
                case StarterSpiritTrialStage.VerifyDifferentCarrier:
                    Stage = StarterSpiritTrialStage
                        .ChooseDifferentSpecies;
                    _pendingSpecies = _currentSpecies;
                    _pendingCarrier = _currentCarrier;
                    PublishObjective(
                        "保持当前动作，换另一只灵宠比较效果");
                    RefreshSelectionHud();
                    break;
                case StarterSpiritTrialStage.VerifyDifferentSpecies:
                    BeginFreeTrial();
                    break;
            }
        }

        private void BeginFreeTrial()
        {
            Stage = StarterSpiritTrialStage.FreeTrial;
            _transitioning = false;
            _confirming = false;
            _pendingSpecies = _currentSpecies;
            _pendingCarrier = _currentCarrier;
            RefreshFreeHud();
            PublishPracticeObjective();
        }

        private void SelectSpecies(StableConfigId species)
        {
            if (!StarterSpiritChoice.IsOption(species))
                return;

            if (Stage == StarterSpiritTrialStage
                    .ChooseFirstAssignment ||
                Stage == StarterSpiritTrialStage
                    .ChooseDifferentCarrier ||
                Stage == StarterSpiritTrialStage
                    .ChooseDifferentSpecies ||
                Stage == StarterSpiritTrialStage.FreeTrial)
            {
                _pendingSpecies = species;
                _confirming = false;
                FocusWorldEntity(species);
                if (Stage == StarterSpiritTrialStage.FreeTrial)
                {
                    ApplyFreeAssignment(
                        species,
                        _pendingCarrier ?? _currentCarrier);
                }
                else
                    RefreshSelectionHud();
            }
        }

        private void SelectCarrier(CarrierSlot carrier)
        {
            if (!StarterSpiritCarrierRuntime.Supports(carrier))
                return;

            if (Stage == StarterSpiritTrialStage
                    .ChooseFirstAssignment ||
                Stage == StarterSpiritTrialStage
                    .ChooseDifferentCarrier ||
                Stage == StarterSpiritTrialStage
                    .ChooseDifferentSpecies ||
                Stage == StarterSpiritTrialStage.FreeTrial)
            {
                _pendingCarrier = carrier;
                _confirming = false;
                if (Stage == StarterSpiritTrialStage.FreeTrial)
                {
                    ApplyFreeAssignment(
                        _pendingSpecies.IsEmpty
                            ? _currentSpecies
                            : _pendingSpecies,
                        carrier);
                }
                else
                    RefreshSelectionHud();
            }
        }

        private void RequestAssignment()
        {
            if (_pendingSpecies.IsEmpty ||
                !_pendingCarrier.HasValue)
            {
                _hud.ShowError("先选择灵宠和动作");
                return;
            }

            StableConfigId nextSpecies = _pendingSpecies;
            CarrierSlot nextCarrier = _pendingCarrier.Value;
            _previousSpecies = _currentSpecies;
            _previousCarrier = _currentCarrier;

            switch (Stage)
            {
                case StarterSpiritTrialStage.ChooseFirstAssignment:
                    if (!_carrier.BeginStarterTrial(
                            nextSpecies,
                            nextCarrier,
                            grantTechnique: true))
                    {
                        _hud.ShowError("当前无法开始赋予教学");
                        return;
                    }
                    _currentSpecies = nextSpecies;
                    _currentCarrier = nextCarrier;
                    BeginFreeTrial();
                    break;
                case StarterSpiritTrialStage
                    .ChooseDifferentCarrier:
                    if (nextSpecies != _currentSpecies)
                    {
                        _hud.ShowError(
                            "可以预览其他组合；本步确认时请保持当前灵宠");
                        return;
                    }
                    if (nextCarrier == _currentCarrier)
                    {
                        _hud.ShowError("请选择另一个动作");
                        return;
                    }
                    if (_carrier.TryAttach(nextCarrier, false) !=
                        StarterSpiritAttachmentResult.Success)
                    {
                        _hud.ShowError("当前无法切换赋予动作");
                        return;
                    }
                    _currentCarrier = nextCarrier;
                    BeginVerification(
                        StarterSpiritTrialStage
                            .VerifyDifferentCarrier);
                    break;
                case StarterSpiritTrialStage
                    .ChooseDifferentSpecies:
                    if (nextCarrier != _currentCarrier)
                    {
                        _hud.ShowError(
                            "可以预览其他组合；本步确认时请保持当前动作");
                        return;
                    }
                    if (nextSpecies == _currentSpecies)
                    {
                        _hud.ShowError("请选择另一只灵宠");
                        return;
                    }
                    if (!_carrier.BeginStarterTrial(
                            nextSpecies,
                            _currentCarrier,
                            grantTechnique: true))
                    {
                        _hud.ShowError("当前无法替换试玩灵宠");
                        return;
                    }
                    _currentSpecies = nextSpecies;
                    BeginVerification(
                        StarterSpiritTrialStage
                            .VerifyDifferentSpecies);
                    break;
                case StarterSpiritTrialStage.FreeTrial:
                    ApplyFreeAssignment(nextSpecies, nextCarrier);
                    break;
            }
        }

        private void ApplyFreeAssignment(
            StableConfigId species,
            CarrierSlot carrier)
        {
            bool speciesChanged = species != _currentSpecies;
            bool carrierChanged = carrier != _currentCarrier;
            if (!speciesChanged && !carrierChanged)
                return;

            // 每个试玩组合必须从自己的首次动作开始计数。
            // 否则LMB打两次后切走再切回，会让下一击看似直接触发三击效果。
            bool success = _carrier.BeginStarterTrial(
                species,
                carrier,
                grantTechnique: true);
            if (!success)
            {
                _hud.ShowError("当前无法应用这个试玩组合");
                return;
            }

            _currentSpecies = species;
            _currentCarrier = carrier;
            _pendingSpecies = species;
            _pendingCarrier = carrier;
            _confirming = false;
            FocusWorldEntity(species);
            RefreshFreeHud();
        }

        private void BeginVerification(
            StarterSpiritTrialStage stage)
        {
            Stage = stage;
            _verificationProgress = 0;
            _verificationStartedAt = Time.unscaledTime;
            _hintLevel = 0;
            _assistArmed = false;
            _transitioning = false;
            FocusWorldEntity(_currentSpecies);
            RefreshVerificationHud();
            PublishObjective(
                $"使用{StarterSpiritTrialHUD.CarrierName(_currentCarrier)}" +
                "攻击练习靶，触发灵宠显化");
        }

        private void RequestCommit()
        {
            if (Stage != StarterSpiritTrialStage.FreeTrial)
                return;
            if (_completed.Count < RequiredCombinationCount)
            {
                _hud.ShowError(
                    $"再实际使用{RequiredCombinationCount - _completed.Count}" +
                    "种不同组合即可结契");
                return;
            }
            if (!_confirming)
            {
                _confirming = true;
                RefreshFreeHud();
                return;
            }

            StarterSpiritChoiceResult result =
                CommitTrialChoice(
                    SaveSystem.Instance.Data,
                    _currentSpecies,
                    _currentCarrier,
                    out SpiritInstanceState spirit);
            if (result != StarterSpiritChoiceResult.Success)
            {
                _confirming = false;
                _hud.ShowError($"无法完成结契：{result}");
                return;
            }

            SaveSystem.Instance.Save();
            Stage = StarterSpiritTrialStage.Committed;
            StableConfigId chosen = _currentSpecies;
            Action<StableConfigId, SpiritInstanceState> callback =
                _onChosen;
            CleanupTrial();
            callback?.Invoke(chosen, spirit);
            Destroy(gameObject);
        }

        public static StarterSpiritChoiceResult CommitTrialChoice(
            SaveDataV1 save,
            StableConfigId species,
            CarrierSlot carrier,
            out SpiritInstanceState spirit)
        {
            StarterSpiritChoiceResult result =
                StarterSpiritChoice.TryChoose(
                    save,
                    species,
                    out spirit);
            if (result != StarterSpiritChoiceResult.Success)
                return result;

            StarterPrologueAdvanceResult attachmentResult =
                StarterPrologueProgression.RecordAttachment(
                    save,
                    carrier);
            if (attachmentResult !=
                    StarterPrologueAdvanceResult.Success &&
                attachmentResult !=
                    StarterPrologueAdvanceResult.NoChange)
            {
                throw new InvalidOperationException(
                    $"初契成功但附着提交失败：{attachmentResult}");
            }
            return result;
        }

        private void RefreshFreeHud()
        {
            _hud.ShowFree(
                _currentSpecies,
                _currentCarrier,
                _completed,
                RequiredCombinationCount,
                _confirming,
                PracticeSuggestion());
        }

        private void PublishPracticeObjective()
        {
            int completed = Mathf.Min(
                _completed.Count,
                RequiredCombinationCount);
            PublishObjective(
                completed >= RequiredCombinationCount
                    ? "已使用3种组合，可以随时结契"
                    : $"使用3种不同的灵宠＋动作组合　" +
                      $"{completed} / {RequiredCombinationCount}");
        }

        private string PracticeSuggestion()
        {
            if (_completed.Count >= RequiredCombinationCount ||
                !_hasComparisonAnchor)
            {
                return string.Empty;
            }

            if (!TryFindComparisonCarrier(out CarrierSlot carrier))
            {
                return $"建议：保留" +
                       $"{StarterSpiritTrialHUD.NameOf(_comparisonSpecies)}，" +
                       "只换一个动作比较（不强制）";
            }

            if (!HasDifferentSpeciesOn(carrier))
            {
                return $"建议：保持" +
                       $"{StarterSpiritTrialHUD.CarrierName(carrier)}，" +
                       "换另一只灵宠比较（不强制）";
            }

            return "继续使用任意一种尚未完成的组合";
        }

        private bool TryFindComparisonCarrier(
            out CarrierSlot comparisonCarrier)
        {
            CarrierSlot[] carriers =
            {
                CarrierSlot.Weapon,
                CarrierSlot.TechniqueQ,
                CarrierSlot.Mobility
            };
            foreach (CarrierSlot carrier in carriers)
            {
                if (carrier == _comparisonCarrier)
                    continue;
                if (_completed.Contains(
                        StarterSpiritTrialHUD.Key(
                            _comparisonSpecies,
                            carrier)))
                {
                    comparisonCarrier = carrier;
                    return true;
                }
            }
            comparisonCarrier = default;
            return false;
        }

        private bool HasDifferentSpeciesOn(CarrierSlot carrier)
        {
            foreach (StarterSpiritChoiceProfile profile in
                     StarterSpiritChoicePresentation.Profiles)
            {
                if (profile.SpeciesId == _comparisonSpecies)
                    continue;
                if (_completed.Contains(
                        StarterSpiritTrialHUD.Key(
                            profile.SpeciesId,
                            carrier)))
                {
                    return true;
                }
            }
            return false;
        }

        private void RefreshSelectionHud()
        {
            switch (Stage)
            {
                case StarterSpiritTrialStage.ChooseFirstAssignment:
                    _hud.ShowFirstAssignment(
                        _pendingSpecies,
                        _pendingCarrier);
                    break;
                case StarterSpiritTrialStage.ChooseDifferentCarrier:
                    _hud.ShowDifferentCarrier(
                        _currentSpecies,
                        _currentCarrier,
                        _pendingSpecies,
                        _pendingCarrier);
                    break;
                case StarterSpiritTrialStage.ChooseDifferentSpecies:
                    _hud.ShowDifferentSpecies(
                        _currentSpecies,
                        _currentCarrier,
                        _pendingSpecies,
                        _pendingCarrier ?? _currentCarrier);
                    break;
            }
        }

        private void RefreshVerificationHud()
        {
            _hud.ShowVerification(
                GuidedStepFor(Stage),
                _currentSpecies,
                _currentCarrier,
                _verificationProgress,
                _hintLevel);
        }

        private void CreateTargets()
        {
            Transform player = PlayerController.Instance.transform;
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (StarterSpiritChoiceWorldEntity entity in _entities)
            {
                if (entity == null)
                    continue;
                center += entity.transform.position;
                count++;
            }
            if (count > 0)
                center /= count;
            Vector3 forward = center - player.position;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = PlayerController.Instance.AimDirection;
            if (forward.sqrMagnitude < 0.01f)
                forward = player.forward;
            forward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, forward);

            CreateTarget(
                "StarterTrialTarget_A",
                player.position + forward * 2.4f - side * 0.65f,
                new Color(0.30f, 0.66f, 0.78f));
            CreateTarget(
                "StarterTrialTarget_B",
                player.position + forward * 3.8f + side * 1.1f,
                new Color(0.48f, 0.78f, 0.57f));
            Physics.SyncTransforms();
        }

        private void CreateTarget(
            string targetName,
            Vector3 position,
            Color color)
        {
            GameObject target =
                GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            target.name = targetName;
            target.transform.position =
                new Vector3(position.x, position.y + 0.85f, position.z);
            target.transform.localScale =
                new Vector3(0.62f, 0.85f, 0.62f);
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
                target.layer = enemyLayer;
            target.AddComponent<StarterSpiritTrialTarget>();
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material =
                    new(MaterialHelper.GetLitShader());
                material.color = color;
                material.EnableKeyword("_EMISSION");
                material.SetColor(
                    "_EmissionColor",
                    color * 0.55f);
                renderer.material = material;
            }
            _targets.Add(target);
        }

        private void FocusWorldEntity(StableConfigId species)
        {
            foreach (StarterSpiritChoiceWorldEntity entity in _entities)
            {
                if (entity != null)
                {
                    entity.SetTrialSelected(
                        entity.SpeciesId == species);
                }
            }
        }

        private void EnterCandidatePresentation()
        {
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (StarterSpiritChoiceWorldEntity entity in _entities)
            {
                if (entity == null)
                    continue;
                center += entity.transform.position;
                count++;
            }
            if (count == 0)
                return;
            center /= count;
            foreach (StarterSpiritChoiceWorldEntity entity in _entities)
            {
                if (entity != null)
                    entity.EnterTrialPresentation(center);
            }
        }

        private static void PublishObjective(string text)
        {
            GameEvents.Publish(
                new GameEvents.StarterPrologueObjectiveChanged
                {
                    Text = text,
                    HasWorldPosition = false
                });
        }

        private void CaptureCursor()
        {
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _cursorCaptured = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private Vector3 TargetCenter()
        {
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (GameObject target in _targets)
            {
                if (target == null)
                    continue;
                center += target.transform.position;
                count++;
            }
            return count > 0
                ? center / count
                : PlayerController.Instance.transform.position;
        }

        public static bool IsVerificationStage(
            StarterSpiritTrialStage stage)
        {
            return stage ==
                       StarterSpiritTrialStage.VerifyFirstAssignment ||
                   stage ==
                       StarterSpiritTrialStage.VerifyDifferentCarrier ||
                   stage ==
                       StarterSpiritTrialStage.VerifyDifferentSpecies;
        }

        public static int GuidedStepFor(
            StarterSpiritTrialStage stage)
        {
            return stage switch
            {
                StarterSpiritTrialStage.ChooseFirstAssignment or
                    StarterSpiritTrialStage.VerifyFirstAssignment => 1,
                StarterSpiritTrialStage.ChooseDifferentCarrier or
                    StarterSpiritTrialStage.VerifyDifferentCarrier => 2,
                StarterSpiritTrialStage.ChooseDifferentSpecies or
                    StarterSpiritTrialStage.VerifyDifferentSpecies => 3,
                _ => 0
            };
        }

        private void CleanupTrial()
        {
            if (_hud != null)
            {
                _hud.SpeciesRequested -= SelectSpecies;
                _hud.CarrierRequested -= SelectCarrier;
                _hud.AssignmentRequested -= RequestAssignment;
                _hud.CommitRequested -= RequestCommit;
                Destroy(_hud.gameObject);
                _hud = null;
            }
            foreach (GameObject target in _targets)
            {
                if (target != null)
                    Destroy(target);
            }
            _targets.Clear();
            foreach (StarterSpiritChoiceWorldEntity entity in _entities)
            {
                if (entity != null)
                    entity.ExitTrialPresentation();
            }
            if (_cursorCaptured)
            {
                Cursor.lockState = _previousCursorLock;
                Cursor.visible = _previousCursorVisible;
                _cursorCaptured = false;
            }
        }

        private void OnDestroy()
        {
            if (Stage != StarterSpiritTrialStage.Committed)
                CleanupTrial();
        }
    }
}
