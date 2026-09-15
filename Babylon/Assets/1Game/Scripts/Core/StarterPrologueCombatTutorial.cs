using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu
{
    public enum StarterPrologueCombatTutorialStage
    {
        Inactive = 0,
        BasicActions = 1,
        BasicActionsComplete = 2,
        CarrierPractice = 3,
        CarrierComplete = 4,
        BossRule = 5,
        Complete = 6,
    }

    /// <summary>序章战斗教学的纯状态，供运行时宿主和合同测试共用。</summary>
    public sealed class StarterPrologueCombatTutorialRuntime
    {
        public const float CarrierReminderSeconds = 10f;
        public const float PrimerAutoReleaseSeconds = 30f;

        private float _elapsed;

        public StarterPrologueCombatTutorialStage Stage { get; private set; }
        public bool BasicAttackDone { get; private set; }
        public bool TechniqueDone { get; private set; }
        public bool DodgeDone { get; private set; }
        public bool PrimerAutoReleased { get; private set; }
        public bool CarrierReminderShown { get; private set; }
        public bool CarrierActivated { get; private set; }
        public CarrierSlot Carrier { get; private set; }
        public int CarrierProgress { get; private set; }

        public void BeginBasicActions()
        {
            Stage = StarterPrologueCombatTutorialStage.BasicActions;
            BasicAttackDone = false;
            TechniqueDone = false;
            DodgeDone = false;
            PrimerAutoReleased = false;
            _elapsed = 0f;
        }

        public bool RecordBasicAttack()
        {
            if (Stage != StarterPrologueCombatTutorialStage.BasicActions)
                return false;
            BasicAttackDone = true;
            return TryCompleteBasicActions();
        }

        public bool RecordTechnique()
        {
            if (Stage != StarterPrologueCombatTutorialStage.BasicActions)
                return false;
            TechniqueDone = true;
            return TryCompleteBasicActions();
        }

        public bool RecordDodge()
        {
            if (Stage != StarterPrologueCombatTutorialStage.BasicActions)
                return false;
            DodgeDone = true;
            return TryCompleteBasicActions();
        }

        public void BeginCarrierPractice(CarrierSlot carrier)
        {
            Carrier = carrier;
            CarrierProgress = 0;
            CarrierActivated = false;
            CarrierReminderShown = false;
            _elapsed = 0f;
            Stage =
                StarterPrologueCombatTutorialStage.CarrierPractice;
        }

        public bool RecordCarrierAdvanced(
            GameEvents.StarterSpiritCarrierAdvanced evt)
        {
            if (Stage !=
                    StarterPrologueCombatTutorialStage.CarrierPractice ||
                evt.Carrier != Carrier)
            {
                return false;
            }

            CarrierProgress = Mathf.Max(CarrierProgress, evt.Progress);
            if (!evt.Activated)
                return false;
            CarrierActivated = true;
            Stage =
                StarterPrologueCombatTutorialStage.CarrierComplete;
            return true;
        }

        public bool Tick(float deltaTime)
        {
            _elapsed += Mathf.Max(0f, deltaTime);
            if (Stage ==
                    StarterPrologueCombatTutorialStage.BasicActions &&
                _elapsed >= PrimerAutoReleaseSeconds)
            {
                BasicAttackDone = true;
                TechniqueDone = true;
                DodgeDone = true;
                PrimerAutoReleased = true;
                Stage =
                    StarterPrologueCombatTutorialStage
                        .BasicActionsComplete;
                return true;
            }

            if (Stage ==
                    StarterPrologueCombatTutorialStage.CarrierPractice &&
                !CarrierReminderShown &&
                _elapsed >= CarrierReminderSeconds)
            {
                CarrierReminderShown = true;
                return true;
            }
            return false;
        }

        public void BeginBossRule()
        {
            Stage = StarterPrologueCombatTutorialStage.BossRule;
        }

        public void Complete()
        {
            Stage = StarterPrologueCombatTutorialStage.Complete;
        }

        private bool TryCompleteBasicActions()
        {
            if (!BasicAttackDone || !TechniqueDone || !DodgeDone)
                return false;
            Stage =
                StarterPrologueCombatTutorialStage
                    .BasicActionsComplete;
            return true;
        }
    }

    /// <summary>
    /// 序章战斗教学宿主。基础操作只在本次灵宠会话中要求一次，
    /// 实战阶段根据首次附着追踪对应显化。
    /// </summary>
    public sealed class StarterPrologueCombatTutorial : MonoBehaviour
    {
        private const float PrimerCompletionDelay = 0.65f;
        private static readonly HashSet<string>
            CompletedPrimerSessions = new();

        private readonly StarterPrologueCombatTutorialRuntime _runtime =
            new();
        private StarterPrologueCombatGuideHUD _hud;
        private Action _onPrimerCompleted;
        private string _sessionKey;
        private bool _primerFinishing;

        public StarterPrologueCombatTutorialRuntime Runtime => _runtime;

        public static StarterPrologueCombatTutorial EnsureExists()
        {
            StarterPrologueCombatTutorial existing =
                FindObjectOfType<StarterPrologueCombatTutorial>();
            if (existing != null)
                return existing;
            return new GameObject("StarterPrologueCombatTutorial")
                .AddComponent<StarterPrologueCombatTutorial>();
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.SkillCastStarted>(
                OnSkillCastStarted);
            GameEvents.Subscribe<GameEvents.DodgeFinished>(
                OnDodgeFinished);
            GameEvents.Subscribe<
                GameEvents.StarterSpiritCarrierAdvanced>(
                OnCarrierAdvanced);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.SkillCastStarted>(
                OnSkillCastStarted);
            GameEvents.Unsubscribe<GameEvents.DodgeFinished>(
                OnDodgeFinished);
            GameEvents.Unsubscribe<
                GameEvents.StarterSpiritCarrierAdvanced>(
                OnCarrierAdvanced);
        }

        private void Update()
        {
            if (_runtime.Stage ==
                StarterPrologueCombatTutorialStage.BasicActions)
            {
                Mouse mouse = Mouse.current;
                if (mouse != null &&
                    mouse.leftButton.wasPressedThisFrame)
                {
                    bool completed = _runtime.RecordBasicAttack();
                    RefreshPrimer();
                    if (completed)
                        FinishPrimer();
                }
            }

            if (_runtime.Tick(Time.unscaledDeltaTime))
            {
                if (_runtime.Stage ==
                    StarterPrologueCombatTutorialStage
                        .BasicActionsComplete)
                {
                    RefreshPrimer();
                    FinishPrimer();
                }
                else if (_runtime.Stage ==
                         StarterPrologueCombatTutorialStage
                             .CarrierPractice)
                {
                    _hud?.ShowCarrierReminder(
                        _runtime.Carrier,
                        ResolveSpecies());
                }
            }
        }

        public void BeginPrimer(Action onCompleted)
        {
            StopAllCoroutines();
            _onPrimerCompleted = onCompleted;
            _primerFinishing = false;
            _sessionKey = ResolveSessionKey();
            _hud = StarterPrologueCombatGuideHUD.EnsureExists();

            if (!string.IsNullOrWhiteSpace(_sessionKey) &&
                CompletedPrimerSessions.Contains(_sessionKey))
            {
                InvokePrimerCompletion();
                return;
            }

            _runtime.BeginBasicActions();
            _hud.ShowPrimer(false, false, false);
            GameEvents.Publish(
                new GameEvents.StarterPrologueObjectiveChanged
                {
                    Text = "完成基础战斗操作",
                    HasWorldPosition = false
                });
        }

        public void BeginCarrierPractice()
        {
            StarterSpiritCarrierController controller =
                FindObjectOfType<StarterSpiritCarrierController>();
            CarrierSlot carrier =
                controller != null
                    ? controller.Attachment
                    : CarrierSlot.Weapon;
            _runtime.BeginCarrierPractice(carrier);
            _hud = StarterPrologueCombatGuideHUD.EnsureExists();
            _hud.ShowCarrierPractice(
                carrier,
                ResolveSpecies(),
                0);
        }

        public void BeginBossRule()
        {
            _runtime.BeginBossRule();
            _hud = StarterPrologueCombatGuideHUD.EnsureExists();
            _hud.ShowBossRule();
        }

        public void Complete()
        {
            StopAllCoroutines();
            _runtime.Complete();
            _onPrimerCompleted = null;
            _hud?.Hide();
        }

        public static void ResetSessionForTests()
        {
            CompletedPrimerSessions.Clear();
        }

        private void OnSkillCastStarted(
            GameEvents.SkillCastStarted evt)
        {
            if (evt.SlotIndex != 0 ||
                _runtime.Stage !=
                    StarterPrologueCombatTutorialStage.BasicActions)
            {
                return;
            }
            bool completed = _runtime.RecordTechnique();
            RefreshPrimer();
            if (completed)
                FinishPrimer();
        }

        private void OnDodgeFinished(GameEvents.DodgeFinished evt)
        {
            if (_runtime.Stage !=
                StarterPrologueCombatTutorialStage.BasicActions)
            {
                return;
            }
            bool completed = _runtime.RecordDodge();
            RefreshPrimer();
            if (completed)
                FinishPrimer();
        }

        private void OnCarrierAdvanced(
            GameEvents.StarterSpiritCarrierAdvanced evt)
        {
            if (_runtime.Stage !=
                StarterPrologueCombatTutorialStage.CarrierPractice)
            {
                return;
            }

            bool completed = _runtime.RecordCarrierAdvanced(evt);
            _hud?.ShowCarrierPractice(
                _runtime.Carrier,
                evt.SpeciesId,
                _runtime.CarrierProgress);
            if (completed)
            {
                _hud?.ShowCarrierComplete(
                    evt.SpeciesId,
                    evt.Carrier);
            }
        }

        private void RefreshPrimer()
        {
            _hud?.ShowPrimer(
                _runtime.BasicAttackDone,
                _runtime.TechniqueDone,
                _runtime.DodgeDone);
        }

        private void FinishPrimer()
        {
            if (_primerFinishing)
                return;
            _primerFinishing = true;
            if (!string.IsNullOrWhiteSpace(_sessionKey))
                CompletedPrimerSessions.Add(_sessionKey);
            _hud?.ShowPrimerComplete(
                _runtime.PrimerAutoReleased);
            StartCoroutine(CompletePrimerAfterDelay());
        }

        private IEnumerator CompletePrimerAfterDelay()
        {
            yield return new WaitForSecondsRealtime(
                PrimerCompletionDelay);
            InvokePrimerCompletion();
        }

        private void InvokePrimerCompletion()
        {
            Action callback = _onPrimerCompleted;
            _onPrimerCompleted = null;
            callback?.Invoke();
        }

        private static StableConfigId ResolveSpecies()
        {
            StarterSpiritCarrierController controller =
                FindObjectOfType<StarterSpiritCarrierController>();
            if (controller?.Spirit != null)
                return controller.Spirit.Identity.SpeciesConfigId;
            string species =
                SaveSystem.Instance.Data.starterSpiritSpeciesId;
            return string.IsNullOrWhiteSpace(species)
                ? default
                : new StableConfigId(species);
        }

        private static string ResolveSessionKey()
        {
            StarterSpiritCarrierController controller =
                FindObjectOfType<StarterSpiritCarrierController>();
            if (controller?.Spirit != null)
            {
                Guid instanceId =
                    controller.Spirit.Identity.InstanceId;
                if (instanceId != Guid.Empty)
                    return instanceId.ToString("N");
            }

            SaveDataV1 save = SaveSystem.Instance.Data;
            if (save.activeSpiritInstanceGuids != null &&
                save.activeSpiritInstanceGuids.Count > 0)
            {
                return save.activeSpiritInstanceGuids[0];
            }
            return save.starterSpiritSpeciesId ?? string.Empty;
        }
    }
}
