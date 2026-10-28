// Overview presentation only; stored totals, statistics and history remain unchanged.
(() => {
 const number=value=>Math.max(0,Number(value)||0);
 GaidenUI.components.overviewMetrics=()=>`<div class="metric-row overview-metrics"><div class="metric"><div class="metric-label">В Questlio</div><div class="metric-value">${fmt(games.reduce((sum,g)=>sum+number(g.minutes),0)*60)}</div></div>${games.reduce((sum,g)=>sum+number(g.steam?.minutes),0)>0?`<div class="metric"><div class="metric-label">Часы из Steam</div><div class="metric-value">${fmt(games.reduce((sum,g)=>sum+number(g.steam?.minutes),0)*60)}</div><small class="muted">импортировано</small></div>`:''}<div class="metric"><div class="metric-label">В библиотеке</div><div class="metric-value">${games.length}<small>игр</small></div><small class="muted">${games.filter(g=>g.status==='finished').length} пройдено</small></div>${GaidenUI.components.todayPanel()}</div>`;
 GaidenUI.components.playButton=g=>`<button class="${g.launchAvailable?'primary':'secondary'} hero-play" data-play-game="${escapeHTML(g.id)}">${g.launchAvailable?'Играть':'Указать файл игры'}</button>`;
 function todaySummary(snapshot=GaidenUI.state.snapshot,now=new Date()){
  const key=d=>`${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
  const today=key(now),daily=new Map();for(const row of snapshot.daily||[]){const date=row.play_date||row.date;if(date)daily.set(date,(daily.get(date)||0)+number(row.play_time??row.minutes));}
  const sessions=(snapshot.sessions||[]).filter(s=>s.startedAt&&key(new Date(s.startedAt))===today);
  const seconds=s=>number(s.activeSeconds??number(s.activeMinutes)*60);
  const native=sessions.filter(s=>s.source==='gaiden-tracker');
  const totalSeconds=daily.has(today)?daily.get(today)*60+native.reduce((sum,s)=>sum+seconds(s),0):sessions.reduce((sum,s)=>sum+seconds(s),0);
  const days=new Set([...daily].filter(([,minutes])=>minutes>0).map(([date])=>date));
  // Daily records supply imported days; native sessions supply days recorded since import.
  for(const s of snapshot.sessions||[])if(s.source==='gaiden-tracker'&&seconds(s)>0)days.add(key(new Date(s.startedAt)));
  const cursor=new Date(now.getFullYear(),now.getMonth(),now.getDate());if(!days.has(today))cursor.setDate(cursor.getDate()-1);let streak=0;while(days.has(key(cursor))&&streak<100000){streak++;cursor.setDate(cursor.getDate()-1);}
  return {minutes:Math.floor(totalSeconds/60),seconds:totalSeconds,sessions:sessions.length,streak};
 }
 GaidenUI.components.todaySummary=todaySummary;
 GaidenUI.components.todayPanel=()=>{const t=todaySummary();if(!t.seconds&&!t.streak)return '';return `<div class="today-chips"><span class="today-chip"><span class="today-chip-label">Сегодня</span><strong>${fmt(t.seconds)}</strong></span><span class="today-chip"><span class="today-chip-label">Серия</span><strong>${t.streak} дн.</strong></span></div>`;};
 document.addEventListener('click',event=>{const b=event.target.closest('[data-play-game]');if(!b)return;const g=games.find(g=>g.id===b.dataset.playGame);if(g?.launchAvailable)GaidenDetail.launch(g.id);else if(g)window.chooseGameExe({action:'bind',id:g.id});});
})();
