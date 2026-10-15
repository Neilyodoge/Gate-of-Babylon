from PIL import Image, ImageOps, ImageDraw, ImageFont
from pathlib import Path
import sys, json, re

root=Path(sys.argv[1])
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
labels={'fit-upgraded-sources':'强化辅助牌也能铺盾','fit-upgraded-rain':'强化小雨点配合冲撞','fit-upgraded-breath':'强化深呼吸配合冲撞','fit-upgraded-dual':'多种搭配只举一个例子','relic-0':'没有小物','relic-1':'树叶帽开局护盾','relic-2':'旧存档电花伤害加成','relic-reward-2':'已有小物不误报新奖励','camp-full':'满体力先查看强化','camp-partial':'只恢复实际缺少的体力','camp-hurt':'休息仍有恢复上限','camp-complete':'全部强化后直接继续','damage-snail':'两段攻击计入中途缩壳','damage-blocked':'护盾挡住时显示零伤害','damage-refresh':'出牌后重算潮湿与敌盾','damage-lethal':'伤害不超过剩余体力','damage-unaffordable':'能量不足时保留原说明','turn-counter':'敌人行动后实际反击与抽牌','turn-return':'返回后仍可回看结算','turn-carry':'实际反击与保留护盾','turn-short':'牌不够时不虚报多抽','turn-rest':'休息时没有虚假反击','status-carry':'持续显示余盾保留','status-carry-used':'保留完成不再提示待触发','status-fragile':'等待破盾反击','status-fragile-used':'触发后清除效果','status-waiting':'减伤等到真正攻击','status-combined':'多种当前效果并存','status-reflect':'反击累计实际数值','inspect-end-focus':'选结束回合时显示真实风险','inspect-focus-return':'鼠标离开后恢复键盘查看','inspect-pointer-return':'焦点移开后恢复鼠标查看','inspect-reward-focus':'保留键盘选牌搭配','inspect-reward-pointer':'保留鼠标选牌搭配','receipt-heal':'只恢复实际缺少的体力','receipt-full':'满血时说明没有恢复','receipt-breath':'回能抽牌护盾一并显示','receipt-return':'返回后保留实际收获','receipt-empty':'没有可抽的牌','receipt-cap':'手牌满时显示实际抽牌','receipt-static':'关闭动作仍有实际反馈','lesson-start':'第一步只提示泼水','lesson-shield-first':'先出盾也能接着学','lesson-detour':'根据现有手牌指路','lesson-end':'没牌可打再结束','lesson-next-turn':'下一回合自行选择','lesson-wet':'潮湿后提示电花','lesson-guard':'连招后提示防守','browse-restored':'牌组回到阅读位置','browse-upgrade':'固定返回营地','browse-upgrade-end':'列表底部仍可返回','fit-start':'选牌前查看搭配','fit-wet':'已有潮湿搭档','fit-shield':'已有护盾搭档','fit-missing':'还缺连招铺垫','fit-long':'长牌组摘要','piles-all':'整套牌合并计数','piles-draw':'查看还没抽到的牌','piles-discard':'实际用过的牌','piles-exhaust':'本场用尽','piles-long':'长牌组浏览','forecast-danger':'结束前看掉血','forecast-shield':'先出护盾的结果','forecast-protected':'护盾已经够用','forecast-lethal':'败退预警','forecast-kill':'直接击败对手','energy-full':'醒目的能量','energy-empty':'用完与下一步','attack-hit':'攻击与跳字','enemy-hit':'敌人攻击','enemy-blocked':'护盾挡伤','attack-start':'出手前','attack-return':'回到原位','feedback-static':'关闭动作','last-hit':'最后一击','branch':'盾牌分支','branch-battle':'第二场试招','snail':'蜗牛缩壳','rest':'赤钳休息','charge':'打断进度','interrupted':'成功打断','thick':'厚泡泡', 'lobby':'出发','battle':'第一手牌','combo':'水电连招','reward':'战后选牌','route':'路线取舍','camp':'休息或强化','upgrade':'强化预览','boss':'苔冠守门人','victory':'结算边界测试','playthrough':'实际完整对局','defeat':'失败重开','cancel':'暂离与结束','deck':'本局牌组'}
def sheet(states,scales,name):
    rows=len(states); cols=len(scales); cw,ch=468,446
    out=Image.new('RGB',(cols*cw,rows*ch),(245,239,223)); d=ImageDraw.Draw(out)
    for row,state in enumerate(states):
        for col,scale in enumerate(scales):
            source=Image.open(root/f'cards-{state}-{scale}.png').convert('RGBA')
            bg=Image.new('RGBA',source.size,(245,239,223,255));bg.alpha_composite(source)
            tile=bg.convert('RGB').resize((456,398),Image.Resampling.LANCZOS)
            out.paste(tile,(col*cw+6,row*ch+40))
            d.text((col*cw+12,row*ch+8),f'{labels[state]} · {[100,115,130][scale]}%',font=font,fill='#4A4337')
    out.save(root/(name+'.png'));ImageOps.grayscale(out).save(root/(name+'-gray.png'))
sheet(['fit-upgraded-sources','fit-upgraded-rain','fit-upgraded-breath','fit-upgraded-dual'],[0,1,2],'cards-upgraded-fit-overview')
sheet(['relic-0','relic-1','relic-2','relic-reward-2'],[0,1,2],'cards-relic-overview')
sheet(['forecast-danger','forecast-shield','forecast-lethal'],[0,1,2],'cards-reading-overview')
sheet(['camp-full','camp-partial','damage-snail','turn-counter','attack-hit','playthrough'],[0,2],'cards-overview')
sheet(['camp-full','camp-partial','camp-hurt','camp-complete'],[0,1,2],'cards-camp-overview')
sheet(['damage-snail','damage-blocked','damage-refresh','damage-lethal','damage-unaffordable'],[0,1,2],'cards-damage-overview')
sheet(['turn-counter','turn-carry','turn-short','turn-rest'],[0,1,2],'cards-turn-receipt-overview')
sheet(['status-carry','status-fragile','status-waiting','status-combined'],[0,1,2],'cards-status-overview')
sheet(['inspect-focus-return','inspect-pointer-return','inspect-end-focus','inspect-reward-focus','inspect-reward-pointer'],[0,1,2],'cards-inspect-overview')
sheet(['receipt-heal','receipt-breath','receipt-empty','receipt-cap'],[0,1,2],'cards-receipt-overview')
sheet(['lesson-start','lesson-shield-first','lesson-detour','lesson-end','lesson-wet','lesson-guard','lesson-next-turn'],[0,1,2],'cards-lesson-overview')
sheet(['browse-restored','browse-upgrade','browse-upgrade-end'],[0,1,2],'cards-browse-overview')
sheet(['fit-wet','fit-shield','fit-missing'],[0,1,2],'cards-reward-fit-overview')
sheet(['piles-all','piles-draw','piles-exhaust'],[0,1,2],'cards-piles-overview')
sheet(['lobby','battle','reward','branch','route','camp','upgrade','defeat','playthrough'],[0,2],'cards-flow-overview')
for state in labels:
    p=root/f'cards-{state}-2.png'
    ImageOps.grayscale(Image.open(p).convert('RGB')).save(root/f'cards-{state}-gray.png')
def luminance(hex):
    values=[int(hex[i:i+2],16)/255 for i in (1,3,5)]
    values=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in values]
    return sum(v*c for v,c in zip(values,[.2126,.7152,.0722]))
colors=[('#8A4432','#FFFCF3'),('#436C53','#E1EBDA'),('#4A4337','#E1EBDA'),('#4A4337','#F5EFDF'),('#70685B','#F5EFDF'),('#436C53','#F5EFDF'),('#FFFFFF','#436C53'),('#764D3C','#E1EBDA')]
contrasts=[{'foreground':a,'background':b,'ratio':round((max(luminance(a),luminance(b))+.05)/(min(luminance(a),luminance(b))+.05),2)} for a,b in colors]
assert all(item['ratio'] >= 4.5 for item in contrasts), 'Text contrast is below 4.5:1'
(root/'cards-contrast.json').write_text(json.dumps(contrasts,ensure_ascii=False,indent=2),encoding='utf8')
print('Saved contact sheets, grayscale screenshots and contrast measurements.')


# Production-rendered frames at the existing 67 ms animation cadence.
frames=[Image.open(root/f'cards-motion-{i:02d}.png').convert('RGB').crop((0,55,764,310)) for i in range(15)]
frames[0].save(root/'cards-attack-preview.gif',save_all=True,append_images=frames[1:],duration=67,loop=0)
strip=Image.new('RGB',(764,255*3))
for row,index in enumerate([0,4,12]): strip.paste(frames[index],(0,row*255))
strip.save(root/'cards-motion-strip.png')
