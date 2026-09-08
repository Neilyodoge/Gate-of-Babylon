using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// ProjectR运行时UI美术入口。生成图保留在ArtRes，
    /// 运行时界面只通过本主题资产取图，避免散落路径字符串。
    /// </summary>
    [CreateAssetMenu(
        fileName = "ProjectRUITheme",
        menuName = "ProjectR/UI/UI Theme")]
    public sealed class ProjectRUITheme : ScriptableObject
    {
        private const string ResourcePath =
            "UI/ProjectR/ProjectRUITheme";
        private static ProjectRUITheme _instance;

        [Header("初契灵宠")]
        [SerializeField] private Sprite sparkRaccoon;
        [SerializeField] private Sprite echoOwl;
        [SerializeField] private Sprite bounceGel;

        [Header("载体")]
        [SerializeField] private Sprite weapon;
        [SerializeField] private Sprite techniqueQ;
        [SerializeField] private Sprite lockedTechniqueE;
        [SerializeField] private Sprite lockedTechniqueR;
        [SerializeField] private Sprite mobility;

        [Header("状态")]
        [SerializeField] private Sprite health;

        [Header("HUD框体")]
        [SerializeField] private Sprite healthFrame;
        [SerializeField] private Sprite spiritFrame;
        [SerializeField] private Sprite skillFrame;
        [SerializeField] private Sprite skillFrameActive;
        [SerializeField] private Sprite skillFrameLocked;
        [SerializeField] private Sprite keycap;
        [SerializeField] private Sprite tooltipPanel;
        [SerializeField] private Sprite circuitConnector;
        [SerializeField] private Sprite statusPips;

        [Header("灵宠天赋树")]
        [SerializeField] private Sprite talentTreePanel;
        [SerializeField] private Texture2D sparkTalentAtlas;
        [SerializeField] private Texture2D echoTalentAtlas;
        [SerializeField] private Texture2D gelTalentAtlas;
        [SerializeField] private Sprite sparkTalentIllustration;
        [SerializeField] private Sprite echoTalentIllustration;
        [SerializeField] private Sprite gelTalentIllustration;

        public static ProjectRUITheme Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<ProjectRUITheme>(
                        ResourcePath);
                return _instance;
            }
        }

        public Sprite Health => health;
        public Sprite HealthFrame => healthFrame;
        public Sprite SpiritFrame => spiritFrame;
        public Sprite SkillFrame => skillFrame;
        public Sprite SkillFrameActive => skillFrameActive;
        public Sprite SkillFrameLocked => skillFrameLocked;
        public Sprite Keycap => keycap;
        public Sprite TooltipPanel => tooltipPanel;
        public Sprite CircuitConnector => circuitConnector;
        public Sprite StatusPips => statusPips;
        public Sprite TalentTreePanel => talentTreePanel;
        public Sprite SparkTalentIllustration =>
            sparkTalentIllustration;

        public Sprite TalentIllustration(StableConfigId species)
        {
            if (species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies)
                return sparkTalentIllustration;
            if (species ==
                FirstSpiritCircuitContent.EchoOwlSpecies)
                return echoTalentIllustration;
            if (species ==
                FirstSpiritCircuitContent.BounceGelSpecies)
                return gelTalentIllustration;
            return null;
        }

        public Sprite SpiritPortrait(StableConfigId species)
        {
            if (species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies)
                return sparkRaccoon;
            if (species ==
                FirstSpiritCircuitContent.EchoOwlSpecies)
                return echoOwl;
            if (species ==
                FirstSpiritCircuitContent.BounceGelSpecies)
                return bounceGel;
            return null;
        }

        public Sprite CarrierIcon(int visualIndex)
        {
            return visualIndex switch
            {
                0 => techniqueQ,
                1 => lockedTechniqueE,
                2 => lockedTechniqueR,
                3 => mobility,
                4 => weapon,
                _ => null
            };
        }

        public Texture2D TalentIconAtlas(StableConfigId species)
        {
            if (species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies)
            {
                return sparkTalentAtlas;
            }
            if (species ==
                FirstSpiritCircuitContent.EchoOwlSpecies)
            {
                return echoTalentAtlas;
            }
            if (species ==
                FirstSpiritCircuitContent.BounceGelSpecies)
            {
                return gelTalentAtlas;
            }
            return null;
        }

#if UNITY_EDITOR
        public void Configure(
            Sprite spark,
            Sprite echo,
            Sprite bounce,
            Sprite weaponSprite,
            Sprite techniqueQSprite,
            Sprite lockedESprite,
            Sprite lockedRSprite,
            Sprite mobilitySprite,
            Sprite healthSprite)
        {
            sparkRaccoon = spark;
            echoOwl = echo;
            bounceGel = bounce;
            weapon = weaponSprite;
            techniqueQ = techniqueQSprite;
            lockedTechniqueE = lockedESprite;
            lockedTechniqueR = lockedRSprite;
            mobility = mobilitySprite;
            health = healthSprite;
        }
#endif

        public bool HasCompleteCoreSet()
        {
            return sparkRaccoon != null &&
                   echoOwl != null &&
                   bounceGel != null &&
                   weapon != null &&
                   techniqueQ != null &&
                   lockedTechniqueE != null &&
                   lockedTechniqueR != null &&
                   mobility != null &&
                   health != null;
        }

#if UNITY_EDITOR
        public void ConfigureChrome(
            Sprite healthFrameSprite,
            Sprite spiritFrameSprite,
            Sprite skillFrameSprite,
            Sprite skillFrameActiveSprite,
            Sprite skillFrameLockedSprite,
            Sprite keycapSprite,
            Sprite tooltipPanelSprite,
            Sprite circuitConnectorSprite,
            Sprite statusPipsSprite)
        {
            healthFrame = healthFrameSprite;
            spiritFrame = spiritFrameSprite;
            skillFrame = skillFrameSprite;
            skillFrameActive = skillFrameActiveSprite;
            skillFrameLocked = skillFrameLockedSprite;
            keycap = keycapSprite;
            tooltipPanel = tooltipPanelSprite;
            circuitConnector = circuitConnectorSprite;
            statusPips = statusPipsSprite;
        }

        public void ConfigureTalentTree(Sprite panel)
        {
            talentTreePanel = panel;
        }

        public void ConfigureTalentAtlases(
            Texture2D sparkAtlas,
            Texture2D echoAtlas,
            Texture2D gelAtlas)
        {
            sparkTalentAtlas = sparkAtlas;
            echoTalentAtlas = echoAtlas;
            gelTalentAtlas = gelAtlas;
        }

        public void ConfigureTalentIllustrations(
            Sprite sparkIllustrationSprite,
            Sprite echoIllustrationSprite,
            Sprite gelIllustrationSprite)
        {
            sparkTalentIllustration = sparkIllustrationSprite;
            echoTalentIllustration = echoIllustrationSprite;
            gelTalentIllustration = gelIllustrationSprite;
        }
#endif

        public bool HasCompleteHudChrome()
        {
            return healthFrame != null &&
                   spiritFrame != null &&
                   skillFrame != null &&
                   skillFrameActive != null &&
                   skillFrameLocked != null &&
                   keycap != null &&
                   tooltipPanel != null &&
                   circuitConnector != null &&
                   statusPips != null;
        }
    }
}
