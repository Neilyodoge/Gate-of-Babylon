using System;
using System.Linq;
public static class FarmUX
{
    public static string FeedLabel(FarmGame g,int pet){return (g.Level(pet)>=10&&g.NeedsEvolutionBond(pet)?"亲密喂养":"喂养")+" · 莓果 2";}
    public static string FeedHint(FarmGame g,int pet){
        if(!g.AtHome)return "返家后可以喂养。";
        if(g.Level(pet)>=10&&!g.NeedsEvolutionBond(pet))return "等级已满，进化所需亲密已满足，无需继续消耗莓果。";
        string effect=g.Level(pet)>=10?"亲密 +1；等级已满，不增加成长。":"成长 +12"+(pet==0||pet==4?"，亲密 +1。":"。");
        return effect+(g.S.Berries<2?" 还差莓果 "+(2-g.S.Berries)+"，农田成熟后自动入库。":"");
    }
    public static string ActionLabel(FarmGame g){if(g.GuideActive&&g.AtHome&&g.S.GuideStep<=3)return g.S.GuideOutingAccepted||g.S.GuideHomeStage>=HomeGuideStage.Invitation?"去小溪看看":"看看伙伴";if(g.R.Casual)return g.R.Phase=="result"?"查看收获":g.R.JourneyPaused?"继续旅程":"查看旅程";switch(g.R.Phase){case "draft":return "选择能力";case "route":return g.R.Node==2?"营地选择":"选择下一站";case "battle":return "查看战斗";case "result":return "查看收获";default:return "去冒险";}}
    public static string Activity(FarmGame g,bool playing){if(g.AtHome&&(!g.GuideActive&&g.S.SimpleCourtyard||g.GuideActive&&g.S.GuideStep<=3))return "农田在慢慢长 · 有空陪伙伴一会儿";if(g.R.Casual)return g.JourneyStatus;switch(g.R.Phase){case "draft":return "战斗已结束 · 等你选择 1 项能力";case "route":return "第 "+(g.R.Node+1)+" / 5 站 · "+(g.R.Node==2?"营地等待选择":"选择下一步后才会前进");case "battle":return playing?"自动战斗中 · 每场结束会等你选择":"战斗已暂停 · 返回冒险页可继续";case "result":return "冒险已结束 · 查看收获后返家";default:return "自动种田中 · 随时可出发冒险";}}
    public static string PetSkill(FarmGame g,int p){if(p==4)return "毒种施加中毒，在敌方反击前结算持续伤害。"+FarmGame.SeedChanges[g.S.SeedStage];return p==0?(g.S.Evolved?"开场让全体敌人潮湿；攻击继续附加潮湿。":"攻击附加潮湿；让电团排在它后面，可以触发追击。"):p==1?"攻击附加灼烧，敌人每回合继续受到伤害。":p==2?"攻击潮湿目标时追加伤害；与泡泡配合。":"每回合治疗受伤伙伴，并为最前方伙伴增加 6 护盾。";}
    public static int FeedsNeeded(FarmGame g){return Math.Max(Math.Max(0,(55-g.S.Xp[0]+11)/12),Math.Max(0,3-g.S.Bond));}
    public static string EvolutionHint(FarmGame g){
        if(g.S.Evolved)return "已解锁群体潮湿、农场生长 +15% 和潮汐洞穴。";
        if(g.CanEvolve)return "条件全部满足。消耗晶石 2，永久进化为潮汐兽。";
        string growth=FeedsNeeded(g)>0?"仅靠喂养还需 "+FeedsNeeded(g)+" 次；也可用麦穗训练提升等级。":"等级与亲密已达标。";
        string stone=g.S.Crystals<2?"还差晶石 "+(2-g.S.Crystals)+"；小溪首领奖励 2。":"晶石已足够。";
        return growth+" "+stone+(!g.AtHome?" 冒险结束返家后即可培养或进化。":"");
    }
    public static string PerkEffect(int id,int level){
        level=Math.Max(1,level);switch(id){
        case 0:return "每场胜利后，全队额外恢复 "+8*level+" 体力。";
        case 1:return "电团攻击潮湿目标，额外伤害增加 "+5*level+"。";
        case 2:return "电团触发潮湿追击时，对另一名敌人造成 "+7*level+" 连锁伤害。";
        case 3:return "每回合为最前方存活的伙伴增加 "+4*level+" 护盾。";
        case 4:return "苔壳每回合额外治疗 "+5*level+" 体力。";
        case 5:return "赤钳每次攻击附加的灼烧增加 "+3*level+"。";
        case 6:return "全队体力上限增加 "+12*level+"；每次选择时恢复 12 体力。";
        case 7:return "每场首回合，全队攻击增加 "+5*level+"。";
        case 8:return "每第三回合，队伍追加一次 "+10*level+" 伤害的攻击。";
        case 9:return "每场胜利额外获得莓果 "+2*level+"。";
        case 10:return "泡泡攻击后，也让另一名敌人潮湿。此能力只需获得一次。";
        default:return "击败敌人时，为伤势最重的存活伙伴恢复 "+10*level+" 体力。";}
    }
    public static string PerkRole(int id){return new[]{"全队续航","电团强化","电团群攻","前排保护","苔壳强化","赤钳强化","全队生存","开场爆发","额外攻击","永久资源","泡泡强化","战斗续航"}[id];}
    public static string PerkAdvice(FarmGame g,int id){if(id==1||id==2)return g.S.Team.Contains(0)?"队里有泡泡，可以先挂潮湿再触发电团效果。":"需要目标处于潮湿状态才能触发。";if(id==3)return "保护第 1 位伙伴，能让后排更稳定地输出。";if(id==4)return "队里有苔壳，治疗会在每回合自动触发。";if(id==5)return "队里有赤钳，适合消耗高体力敌人。";if(id==9)return "额外莓果会永久保留，返家后能继续培养宠物。";if(id==10)return g.S.Team.Contains(2)?"让更多敌人潮湿，为电团提供追击目标。":"有多个敌人时生效，适合后续水雷配合。";if(id==6)return "立即补充体力，适合准备挑战精英或首领。";return "选择后自动生效，无需在战斗中手动使用。";}
    public static string MissingMeal(FarmGame g,int meal){if(meal==0)return "无需材料，可以直接出发。";int food=meal==1?g.S.Berries:g.S.Mint;int need=meal==1?2:1;if(food<need||g.S.Grain<2)return "材料不足：还差"+(food<need?" "+(meal==1?"莓果":"薄荷")+" "+(need-food):"")+(g.S.Grain<2?" 麦穗 "+(2-g.S.Grain):"")+"。可改为轻装出发。";return meal==1?"将消耗莓果 2、麦穗 2；本次全队体力上限 +12。":"将消耗薄荷 1、麦穗 2；每场胜利额外恢复 9 体力。";}
    public static bool CanCook(FarmGame g,int meal){return meal==0||(g.S.Grain>=2&&(meal==1?g.S.Berries>=2:g.S.Mint>=1));}
}
