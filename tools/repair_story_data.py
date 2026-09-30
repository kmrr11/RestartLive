import csv,io,subprocess
from pathlib import Path
root=Path('Assets/Data')
def read(n):
 return list(csv.DictReader((root/(n+'.csv')).read_text(encoding='utf-8-sig').splitlines()))
def write(n,rows,fields):
 s=io.StringIO(newline=''); w=csv.DictWriter(s,fieldnames=fields); w.writeheader();w.writerows(rows)
 for p in [root/(n+'.csv'),Path('Assets/Resources/Data')/(n+'.txt')]:p.write_text(s.getvalue(),encoding='utf-8',newline='')
raw=subprocess.check_output(['git','-c','safe.directory=D:/Backup/renSheng/proj_renSheng','show','HEAD:Assets/Data/StorySteps.csv']).decode('utf-8')
f=next(csv.reader(io.StringIO(raw)));steps=list(csv.DictReader(io.StringIO(raw)))
for r in steps:
 for k in f:r[k]=r.get(k) or ''
def step(id,order,text,choices='',require='',end='0',effects=''):
 r=dict.fromkeys(f,'');r.update(storyId='superpower_arc',stepId=id,order=str(order),text=text,choices=choices,require=require,end=end,effects=effects,advanceSeason='0');steps.append(r)
step('sp_intro',10,'一个普通的早晨，你发现世界偶尔会听错你的愿望。你拿出笔记本，决定先弄清发生了什么。')
step('sp_awaken_sneeze',20,'一个喷嚏让桌上的橡皮悬在半空。你盯着它，它仿佛也在等你先开口。','sp_awaken_try;sp_awaken_hide','tag:superpower_telekinesis')
step('sp_awaken_try',30,'你关好门窗，开始练习控制漂浮的文具。','sp_try_control;sp_try_fun','tag:superpower_telekinesis')
step('sp_bus_pause',40,'车票落下时，全车忽然静止。只有你还能动，和一只同样震惊的苍蝇。','sp_bus_help;sp_bus_secret','tag:superpower_time')
step('sp_cafe_mindread',50,'咖啡馆里，你听见邻座在心里排练告白。紧接着，勺子也开始发表意见。','sp_cafe_listen;sp_cafe_stir','tag:superpower_mindread')
step('sp_heal_day',60,'你替邻居贴创可贴，伤口却先一步愈合。他坚持把没用上的创可贴送给你当纪念。',require='tag:superpower_heal',effects='luck+1')
step('sp_luck_day',70,'你连续接住三片掉落的面包，黄油面都朝上。朋友怀疑你和地心引力有亲戚关系。',require='tag:superpower_luck',effects='luck+1')
step('sp_phone_float',80,'手机响了，屏幕显示来电人是明天的自己。','sp_phone_answer;sp_phone_ignore','tag:superpower_echo')
step('sp_finish',100,'你给笔记本写上规矩：先保护别人，也照顾自己。晚饭还是会糊，但普通日子从此多了一点可能。',end='1',effects='tag:superpower_done')
write('StorySteps',steps,f)
e=read('Events'); ef=list(e[0]); [r.pop(None,None) for r in e]; e=[r for r in e if r['id']!='lise_war_choice']
# The Lise choice belongs inside the main story, not the seasonal pool.
for id,lo,hi,txt,story in [('isekai_light',20,24,'便利店门口的白光把你连同塑料袋一起吞没。','isekai_arc'),('superpower_entry',14,16,'平凡的一天里，某件不可能的小事发生了。','superpower_arc')]:
 r=dict.fromkeys(ef,'');r.update(id=id,ageMin=str(lo),ageMax=str(hi),weight='20',text=txt,once='1',tags='milestone',season='春',startStory=story);e=[x for x in e if x['id']!=id];e.append(r)
write('Events',e,ef)
print('Restored',len(steps),'steps; connected three main stories')

