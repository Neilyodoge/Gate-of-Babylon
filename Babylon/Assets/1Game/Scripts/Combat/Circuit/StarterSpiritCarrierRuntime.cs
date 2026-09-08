using System;
using System.Collections.Generic;

namespace XianTu
{
    public enum StarterSpiritAttachmentResult
    {
        Success = 0,
        NoChange = 1,
        InCombat = 2,
        UnsupportedCarrier = 3,
    }

    public readonly struct StarterSpiritCarrierStep
    {
        public CarrierSlot Carrier { get; }
        public bool Activated { get; }
        public int Progress { get; }

        public StarterSpiritCarrierStep(
            CarrierSlot carrier,
            bool activated,
            int progress)
        {
            Carrier = carrier;
            Activated = activated;
            Progress = progress;
        }
    }

    /// <summary>
    /// 初契单宠的三载体启动规则。三只灵宠共享载体时机，
    /// 具体产出火花、复响或扩散由Unity宿主按种族解析。
    /// </summary>
    public sealed class StarterSpiritCarrierRuntime
    {
        public const int WeaponHitsPerActivation = 3;

        private int _weaponProgress;
        private int _weaponHitsPerActivation = WeaponHitsPerActivation;
        private int _retainedWeaponProgress;

        public StableConfigId SpeciesId { get; }
        public CarrierSlot Attachment { get; private set; }
        public int Progress => _weaponProgress;

        public StarterSpiritCarrierRuntime(
            StableConfigId speciesId,
            CarrierSlot initialAttachment = CarrierSlot.Weapon)
        {
            if (!IsStarterSpecies(speciesId))
                throw new ArgumentException(
                    "Unsupported starter spirit species.",
                    nameof(speciesId));
            if (!Supports(initialAttachment))
                throw new ArgumentOutOfRangeException(
                    nameof(initialAttachment));

            SpeciesId = speciesId;
            Attachment = initialAttachment;
        }

        public StarterSpiritAttachmentResult TryAttach(
            CarrierSlot carrier,
            bool isInCombat)
        {
            if (!Supports(carrier))
                return StarterSpiritAttachmentResult.UnsupportedCarrier;
            if (carrier == Attachment)
                return StarterSpiritAttachmentResult.NoChange;
            if (isInCombat)
                return StarterSpiritAttachmentResult.InCombat;

            Attachment = carrier;
            return StarterSpiritAttachmentResult.Success;
        }

        public StarterSpiritCarrierStep RegisterWeaponHit()
        {
            bool activated = false;
            if (Attachment == CarrierSlot.Weapon)
            {
                _weaponProgress++;
                if (_weaponProgress >= _weaponHitsPerActivation)
                {
                    _weaponProgress = Math.Max(
                        _weaponProgress - _weaponHitsPerActivation,
                        _retainedWeaponProgress);
                    activated = true;
                }
            }
            return new StarterSpiritCarrierStep(
                CarrierSlot.Weapon,
                activated,
                _weaponProgress);
        }

        public void ConfigureWeaponProgress(
            int hitsPerActivation,
            int retainedProgress)
        {
            _weaponHitsPerActivation = Math.Max(1, hitsPerActivation);
            _retainedWeaponProgress = Math.Max(
                0,
                Math.Min(
                    _weaponHitsPerActivation - 1,
                    retainedProgress));
            _weaponProgress = Math.Min(
                _weaponProgress,
                _weaponHitsPerActivation - 1);
        }

        public StarterSpiritCarrierStep RegisterTechniqueHit(
            CarrierSlot carrier)
        {
            return new StarterSpiritCarrierStep(
                carrier,
                carrier == CarrierSlot.TechniqueQ &&
                Attachment == carrier,
                _weaponProgress);
        }

        public StarterSpiritCarrierStep RegisterMobilityFinished()
        {
            return new StarterSpiritCarrierStep(
                CarrierSlot.Mobility,
                Attachment == CarrierSlot.Mobility,
                _weaponProgress);
        }

        public static bool Supports(CarrierSlot carrier)
        {
            return carrier == CarrierSlot.Weapon ||
                   carrier == CarrierSlot.TechniqueQ ||
                   carrier == CarrierSlot.Mobility;
        }

        public static bool IsStarterSpecies(StableConfigId species)
        {
            return species ==
                       FirstSpiritCircuitContent.SparkRaccoonSpecies ||
                   species ==
                       FirstSpiritCircuitContent.EchoOwlSpecies ||
                   species ==
                       FirstSpiritCircuitContent.BounceGelSpecies;
        }
    }

    public static class StarterSpiritCarrierLoadoutProvider
    {
        public static bool TryRestoreSingle(
            SaveDataV1 save,
            out SpiritInstanceState spirit)
        {
            spirit = null;
            if (save?.spiritRoster == null ||
                save.activeSpiritInstanceGuids == null ||
                save.activeSpiritInstanceGuids.Count != 1 ||
                !Guid.TryParse(
                    save.activeSpiritInstanceGuids[0],
                    out Guid activeId))
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

            return roster.TryGetValue(
                       activeId,
                       out SpiritInstanceSave active) &&
                   SpiritSaveMapper.TryRestore(active, out spirit) &&
                   StarterSpiritCarrierRuntime.IsStarterSpecies(
                       spirit.Identity.SpeciesConfigId);
        }

        public static bool TryRestoreSingle(
            SaveDataV1 save,
            StableConfigId expectedSpecies,
            out SpiritInstanceState spirit)
        {
            return TryRestoreSingle(save, out spirit) &&
                   spirit.Identity.SpeciesConfigId == expectedSpecies;
        }
    }
}
