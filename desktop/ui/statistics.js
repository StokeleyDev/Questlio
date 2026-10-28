(function(root){
 const key=date=>`${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}`;
 const amount=(s,field,fallback=0)=>Number.isFinite(s[field])&&s[field]>=0?s[field]:fallback;
 const seconds=s=>amount(s,'activeSeconds',amount(s,'activeMinutes')*60);
 function segments(session){
   const startedAt=new Date(session.startedAt);if(!session.startedAt||!Number.isFinite(startedAt.getTime()))return[];
   const total=seconds(session),idle=Number.isFinite(session.idleSeconds)&&session.idleSeconds>=0?session.idleSeconds:null,parts=[];
   let at=startedAt,left=total;
   do{const midnight=new Date(at.getFullYear(),at.getMonth(),at.getDate()+1),span=Math.max(0,(midnight-at)/1000),piece=Math.min(left,span);
     parts.push({session,seconds:piece,startedAt:at,idleSeconds:idle===null?null:total>0?idle*piece/total:idle});
     left-=piece;at=midnight;
   }while(left>0.000001&&parts.length<1000);
   return parts;
 }
 function dayTotals(sessions){
   const days=new Map();
   for(const session of sessions){const parts=segments(session);if(!parts.length)continue;
     for(const part of parts){const k=key(part.startedAt),day=days.get(k)||{seconds:0,idleSeconds:0,knownIdle:0,sessions:[],entries:[]};
       day.seconds+=part.seconds;if(part.idleSeconds!==null){day.idleSeconds+=part.idleSeconds;day.knownIdle++;}
       day.sessions.push(part.session);day.entries.push(part);days.set(k,day);
     }
   }return days;
 }
 function aggregate(sessions){return dayTotals(sessions);}
 function range(days,start,end){let seconds=0,idleSeconds=0,count=0,knownIdle=0;for(const [k,d] of days)if(k>=start&&k<end){seconds+=d.seconds;idleSeconds+=d.idleSeconds;count+=d.sessions.length;knownIdle+=d.knownIdle;}return{seconds,idleSeconds,count,knownIdle};}
 function combined(snapshot){return dayTotals(snapshot.sessions||[]);}
 function period(snapshot,mode='week',now=new Date(),year=now.getFullYear()){
  const start=mode==='year'?new Date(year,0,1,12):mode==='month'?new Date(now.getFullYear(),now.getMonth(),1,12):new Date(now.getFullYear(),now.getMonth(),now.getDate()-(now.getDay()+6)%7,12);
  const end=new Date(start),previous=new Date(start);if(mode==='year'){end.setFullYear(year+1);previous.setFullYear(year-1);}else if(mode==='month'){end.setMonth(end.getMonth()+1);previous.setMonth(previous.getMonth()-1);}else{end.setDate(end.getDate()+7);previous.setDate(previous.getDate()-7);}
  const days=combined(snapshot),total=range(days,key(start),key(end));const leap=year%4===0&&(year%100!==0||year%400===0);const daySpan=mode==='year'?(leap?366:365):mode==='month'?new Date(start.getFullYear(),start.getMonth()+1,0).getDate():7;
  const dayNumber=date=>Date.UTC(date.getFullYear(),date.getMonth(),date.getDate())/86400000;
  const elapsed=Math.max(0,Math.min(daySpan,Math.round(dayNumber(now)-dayNumber(start))+1));
  const currentEnd=new Date(start),previousEnd=new Date(previous);currentEnd.setDate(currentEnd.getDate()+elapsed);previousEnd.setDate(previousEnd.getDate()+elapsed);
  const current=range(days,key(start),key(currentEnd)),before=range(days,key(previous),key(previousEnd));
  const bins=Array.from({length:mode==='year'?12:daySpan},(_,i)=>{const date=new Date(start),next=new Date(start);if(mode==='year'){date.setMonth(i);next.setMonth(i+1);}else{date.setDate(start.getDate()+i);next.setDate(start.getDate()+i+1);}return{date,key:key(date),...range(days,key(date),key(next))};});
  return {start,end,total,current,before,elapsedDays:elapsed,bins,change:elapsed>=2&&before.seconds>0?Math.round((current.seconds-before.seconds)/before.seconds*100):null};
 }
 const api={key,seconds,segments,dayTotals,aggregate,range,combined,period};root.GaidenStatistics=api;if(typeof module!=='undefined')module.exports=api;
})(globalThis);
