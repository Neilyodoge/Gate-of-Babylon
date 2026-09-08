using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 当前单局的灵宠经验与天赋点宿主。首批原型按出战成员共享遭遇经验，
    /// 不复用旧InsightSystem的永久货币结算。
    /// </summary>
    public sealed class SpiritTalentRunController : MonoBehaviour
    {
        private readonly Dictionary<Guid, SpiritRunTalentState> _states =
            new();

        public IReadOnlyDictionary<Guid, SpiritRunTalentState> States
            => _states;

        private void Awake()
        {
            TryConfigure(SaveSystem.Instance.Data);
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            GameEvents.Subscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Subscribe<GameEvents.StarterSpiritAttachmentChanged>(
                OnStarterSpiritAttachmentChanged);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            GameEvents.Unsubscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Unsubscribe<GameEvents.StarterSpiritAttachmentChanged>(
                OnStarterSpiritAttachmentChanged);
        }

        public bool TryConfigure(SaveDataV1 save)
        {
            _states.Clear();
            if (save?.spiritRoster == null ||
                save.activeSpiritInstanceGuids == null)
            {
                return false;
            }

            var roster = new Dictionary<Guid, SpiritInstanceSave>();
            foreach (SpiritInstanceSave entry in save.spiritRoster)
            {
                if (entry != null &&
                    Guid.TryParse(entry.instanceGuid, out Guid id) &&
                    id != Guid.Empty)
                {
                    roster[id] = entry;
                }
            }

            foreach (string guidText in save.activeSpiritInstanceGuids)
            {
                if (!Guid.TryParse(guidText, out Guid id) ||
                    !roster.TryGetValue(id, out SpiritInstanceSave entry) ||
                    !SpiritSaveMapper.TryRestore(
                        entry,
                        out SpiritInstanceState spirit) ||
                    !StarterSpiritCarrierRuntime.IsStarterSpecies(
                        spirit.Identity.SpeciesConfigId))
                {
                    continue;
                }
                _states[id] = new SpiritRunTalentState(spirit);
            }
            return _states.Count > 0;
        }

        public void GrantSharedExperience(int amount, string reason)
        {
            if (amount <= 0)
                return;

            foreach (SpiritRunTalentState state in _states.Values)
            {
                int gainedLevels = state.AddExperience(amount);
                GameEvents.Publish(new GameEvents.SpiritTalentProgressed
                {
                    SpiritInstanceId =
                        state.Spirit.Identity.InstanceId,
                    SpeciesId =
                        state.Spirit.Identity.SpeciesConfigId,
                    Level = state.Level,
                    Experience = state.Experience,
                    ExperienceToNextPoint =
                        state.ExperienceToNextPoint,
                    UnspentPoints = state.UnspentPoints,
                    GainedLevels = gainedLevels,
                    Reason = reason
                });
            }
        }

        public SpiritTalentActivationResult TryActivate(
            Guid spiritInstanceId,
            StableConfigId talentId)
        {
            if (!_states.TryGetValue(
                    spiritInstanceId,
                    out SpiritRunTalentState state))
            {
                return SpiritTalentActivationResult.WrongSpecies;
            }

            SpiritTalentActivationResult result =
                state.TryActivate(talentId);
            if (result == SpiritTalentActivationResult.Success)
            {
                GameEvents.Publish(new GameEvents.SpiritTalentActivated
                {
                    SpiritInstanceId = spiritInstanceId,
                    SpeciesId = state.Spirit.Identity.SpeciesConfigId,
                    TalentId = talentId,
                    UnspentPoints = state.UnspentPoints
                });
            }
            return result;
        }

        public bool TryGetState(
            Guid spiritInstanceId,
            out SpiritRunTalentState state)
        {
            return _states.TryGetValue(spiritInstanceId, out state);
        }

        public int ResetTalents(Guid spiritInstanceId)
        {
            if (!_states.TryGetValue(
                    spiritInstanceId,
                    out SpiritRunTalentState state))
            {
                return 0;
            }

            int refunded = state.RefundActiveTalents();
            if (refunded > 0)
            {
                GameEvents.Publish(new GameEvents.SpiritTalentsReset
                {
                    SpiritInstanceId = spiritInstanceId,
                    SpeciesId = state.Spirit.Identity.SpeciesConfigId,
                    RefundedPoints = refunded
                });
            }
            return refunded;
        }

        public bool IsActive(
            StableConfigId speciesId,
            string talentId)
        {
            foreach (SpiritRunTalentState state in _states.Values)
            {
                if (state.Spirit.Identity.SpeciesConfigId == speciesId)
                    return state.IsActive(talentId);
            }
            return false;
        }

        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            int amount = 1;
            string enemyName = evt.Enemy != null ? evt.Enemy.name : "";
            if (enemyName.IndexOf(
                    "Boss",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                amount = 10;
            }
            else if (enemyName.IndexOf(
                         "Elite",
                         StringComparison.OrdinalIgnoreCase) >= 0)
            {
                amount = 3;
            }
            GrantSharedExperience(amount, "击败敌人");
        }

        private void OnRoomCleared(GameEvents.RoomCleared evt)
        {
            GrantSharedExperience(2, "完成地点");
        }

        private void OnStarterSpiritAttachmentChanged(
            GameEvents.StarterSpiritAttachmentChanged evt)
        {
            if (!_states.ContainsKey(evt.SpiritInstanceId))
                TryConfigure(SaveSystem.Instance.Data);
        }
    }
}
