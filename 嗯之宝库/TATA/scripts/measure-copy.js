const fs=require('fs'),path=require('path');
const root=process.argv[2],names=['lobby','battle','combo','reward','branch','branch-battle','route','camp','charge'];
const segmenter=new Intl.Segmenter('zh-CN',{granularity:'word'});
const items=names.map(name=>{const text=fs.readFileSync(path.join(root,'cards-copy-'+name+'.txt'),'utf8');return {state:name,han:(text.match(/\p{Script=Han}/gu)||[]).length,wordlike:[...segmenter.segment(text)].filter(x=>x.isWordLike).length};});
const hints=fs.readFileSync(path.join(root,'cards-lesson-copy.txt'),'utf8').split(/\r?\n/).filter(Boolean);
const metrics={scope:'Nine logical UI states at 100%, including shell/buttons and collapsed option text; excludes custom-drawn scene labels. Repeated text counted per state; Chinese units are not English words.',states:items,han:items.reduce((n,x)=>n+x.han,0),wordlike:items.reduce((n,x)=>n+x.wordlike,0),tutorialScope:'Six actual first-turn receipt+hint scenarios exported at 100%; includes detours, excludes next turn; totals across samples are not one tutorial session.',tutorialSamples:hints.length,tutorialHintsHan:hints.join('').match(/\p{Script=Han}/gu).length,unskippableTextScreens:0};
fs.writeFileSync(path.join(root,'cards-ux-metrics.json'),JSON.stringify(metrics,null,2));console.log(JSON.stringify(metrics));
