using System;
using System.Collections.Generic;

namespace XianTu
{
    public sealed class FirstSpiritCircuitLoadoutBinding
    {
        public SpiritLoadoutRuntime Loadout { get; }
        public SpiritInstanceState SparkRaccoon { get; }
        public SpiritInstanceState EchoOwl { get; }
        public SpiritInstanceState BounceGel { get; }

        public FirstSpiritCircuitLoadoutBinding(
            SpiritLoadoutRuntime loadout,
            SpiritInstanceState sparkRaccoon,
            SpiritInstanceState echoOwl,
            SpiritInstanceState bounceGel)
        {
            Loadout = loadout;
            SparkRaccoon = sparkRaccoon;
            EchoOwl = echoOwl;
            BounceGel = bounceGel;
        }
    }

    /// <summary>
    /// 从永久契匣与出战GUID恢复首批三宠的本局编队。
    /// 当前存档不保存局内附着，因此按首批内容定义建立默认附着。
    /// </summary>
    public static class FirstSpiritCircuitLoadoutProvider
    {
        public static bool TryRestore(
            SaveDataV1 save,
            out FirstSpiritCircuitLoadoutBinding binding)
        {
            binding = null;
            if (save?.spiritRoster == null ||
                save.activeSpiritInstanceGuids == null ||
                save.activeSpiritInstanceGuids.Count !=
                    SpiritAttachmentLayout.MaxActiveSpirits)
            {
                return false;
            }

            var roster = new Dictionary<Guid, SpiritInstanceSave>();
            foreach (SpiritInstanceSave entry in save.spiritRoster)
            {
                if (entry != null &&
                    Guid.TryParse(entry.instanceGuid, out Guid id) &&
                    id != Guid.Empty &&
                    !roster.ContainsKey(id))
                {
                    roster.Add(id, entry);
                }
            }

            var loadout = new SpiritLoadoutRuntime();
            SpiritInstanceState sparkRaccoon = null;
            SpiritInstanceState echoOwl = null;
            SpiritInstanceState bounceGel = null;
            foreach (string activeGuid
                     in save.activeSpiritInstanceGuids)
            {
                if (!Guid.TryParse(activeGuid, out Guid id) ||
                    !roster.TryGetValue(id, out SpiritInstanceSave entry) ||
                    !SpiritSaveMapper.TryRestore(
                        entry,
                        out SpiritInstanceState spirit) ||
                    !TryResolveCarrier(
                        spirit.Identity.SpeciesConfigId,
                        out CarrierSlot carrier) ||
                    loadout.Add(spirit, carrier) !=
                        SpiritLoadoutChangeResult.Success)
                {
                    return false;
                }

                StableConfigId species =
                    spirit.Identity.SpeciesConfigId;
                if (species ==
                    FirstSpiritCircuitContent.SparkRaccoonSpecies)
                {
                    if (sparkRaccoon != null)
                        return false;
                    sparkRaccoon = spirit;
                }
                else if (species ==
                    FirstSpiritCircuitContent.EchoOwlSpecies)
                {
                    if (echoOwl != null)
                        return false;
                    echoOwl = spirit;
                }
                else if (species ==
                    FirstSpiritCircuitContent.BounceGelSpecies)
                {
                    if (bounceGel != null)
                        return false;
                    bounceGel = spirit;
                }
            }

            if (sparkRaccoon == null ||
                echoOwl == null ||
                bounceGel == null)
            {
                return false;
            }

            binding = new FirstSpiritCircuitLoadoutBinding(
                loadout,
                sparkRaccoon,
                echoOwl,
                bounceGel);
            return true;
        }

        public static bool TryResolveCarrier(
            StableConfigId species,
            out CarrierSlot carrier)
        {
            if (species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies)
            {
                carrier = CarrierSlot.Weapon;
                return true;
            }
            if (species ==
                FirstSpiritCircuitContent.EchoOwlSpecies)
            {
                carrier = CarrierSlot.TechniqueQ;
                return true;
            }
            if (species ==
                FirstSpiritCircuitContent.BounceGelSpecies)
            {
                carrier = CarrierSlot.Mobility;
                return true;
            }

            carrier = default;
            return false;
        }
    }
}
