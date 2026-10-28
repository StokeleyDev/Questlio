(function(root){
 const seconds=s=>Number.isFinite(s.activeSeconds)&&s.activeSeconds>=0?s.activeSeconds:Math.max(0,Number(s.activeMinutes)||0)*60;
 const validDate=value=>{const d=new Date(value);return value&&Number.isFinite(d.getTime())?d:null;};
 const latest=g=>Math.max(0,...[g.lastPlayedAt,g.legacy?.lastPlayedAt,g.steam?.lastPlayedAt].map(v=>validDate(v)?.getTime()||0));
 const minutes=g=>Math.max(Number(g.minutes)||0,Number(g.steam?.minutes)||0);
 function sortGames(games,mode){const tie=(a,b)=>a.name.localeCompare(b.name,'ru')||String(a.id).localeCompare(String(b.id));return [...games].sort((a,b)=>mode==='name'?tie(a,b):mode==='time'?minutes(b)-minutes(a)||tie(a,b):mode==='added'?(validDate(b.createdAt)?.getTime()||0)-(validDate(a.createdAt)?.getTime()||0)||tie(a,b):latest(b)-latest(a)||tie(a,b));}
 function year(sessions,selectedYear){
  const months=Array(12).fill(0),days=new Set(),games=new Map();let total=0,count=0;
  for(const s of sessions){const date=validDate(s.startedAt);if(!date||date.getFullYear()!==selectedYear)continue;const value=seconds(s);months[date.getMonth()]+=value;total+=value;count++;if(value>0)days.add(`${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`);const key=s.gameId||s.legacyGameName||'unknown';const item=games.get(key)||{id:s.gameId,name:s.legacyGameName||'Игра',seconds:0};item.seconds+=value;games.set(key,item);}
  return {months,total,count,days:days.size,games:[...games.values()].sort((a,b)=>b.seconds-a.seconds)};
 }
 function gameSessions(sessions,id,{year:yr,month,day}={}){return sessions.filter(s=>{if(s.gameId!==id)return false;const d=validDate(s.startedAt);return d&&(!yr||d.getFullYear()===yr)&&(!month||d.getMonth()+1===month)&&(!day||d.getDate()===day);}).sort((a,b)=>validDate(b.startedAt)-validDate(a.startedAt));}
 const api={seconds,validDate,latest,minutes,sortGames,year,gameSessions};root.GaidenFeatures=api;if(typeof module!=='undefined')module.exports=api;
})(globalThis);
