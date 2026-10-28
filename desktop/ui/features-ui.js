if(window.gaidenBootstrap){
const modelFeatures=GaidenUI.moduleState('features',{featureSettings:window.gaidenFeatureSettings||{tracking:{excludeIdle:true,idleMinutes:10},startup:false},sortMode:read('gaiden.librarySort.v1','recent'),yearValue:new Date().getFullYear(),expandedGroups:new Set(),historyState:new Map()});
 const F=GaidenFeatures,post=data=>window.chrome.webview.postMessage(data),esc=escapeHTML;
 
 if(!['recent','time','name','added'].includes(modelFeatures.sortMode))modelFeatures.sortMode='recent';
 const inputYear=(id,value)=>`<label class="compact-field" for="${id}">Год<input id="${id}" type="number" min="1900" max="2200" step="1" value="${value}" required></label>`;
 const selectedGames=()=>F.sortGames(games.filter(g=>(filter==='all'||g.status===filter)&&g.name.toLocaleLowerCase('ru').includes(search.toLocaleLowerCase('ru'))),modelFeatures.sortMode);
 GaidenUI.components.libraryCards=()=>{const result=selectedGames();return (filter!=='all'||search.trim()?`<p class="muted library-result" role="status">${result.length} ${plural(result.length,['игра','игры','игр'])} · ${fmt(result.reduce((n,g)=>n+(Number(g.minutes)||0),0)*60)} в Questlio</p>`:'')+(result.map(card).join('')||(games.length?GaidenKit.empty('Ничего не найдено','Попробуйте другое название или сбросьте фильтр.','','search'):GaidenKit.empty('Здесь будут ваши игры','Добавьте EXE или импортируйте библиотеку Steam.')));};
 GaidenUI.components.sortControl=()=>{const control=document.createElement('label');control.className='sort-control';control.title='У старых записей дата добавления может быть неизвестна';control.innerHTML=`Сортировка<select id="library-sort">${[['recent','Последний запуск'],['time','Общее время'],['name','Название'],['added','Дата добавления']].map(([value,title])=>`<option value="${value}" ${modelFeatures.sortMode===value?'selected':''}>${title}</option>`).join('')}</select>`;return control.outerHTML;};
 
 function yearPanel(sessions,yr,id='annual-year',title='За год'){
  const stats=F.year(sessions,yr),max=Math.max(1,...stats.months);
  return `<section class="panel annual-panel"><div class="section-heading"><h2>${title}</h2>${inputYear(id,yr)}</div><div class="annual-totals"><strong>${fmt(stats.total)}</strong><span>${stats.days} игровых дней · ${stats.count} сессий</span></div><div class="annual-chart" role="img" aria-label="${stats.months.map((v,i)=>GaidenUI.formatDate(new Date(yr,i),{month:'long'})+': '+fmt(v)).join('; ')}">${stats.months.map((v,i)=>`<div class="annual-column"><div style="height:${v/max*100}%;min-height:${v?3:0}px" title="${fmt(v)}"></div><small>${GaidenUI.formatDate(new Date(yr,i),{month:'short'})}</small></div>`).join('')}</div><div class="year-ranking">${stats.games.slice(0,5).map(g=>`<div class="ranking"><span>${esc(games.find(x=>x.id===g.id)?.name||g.name)}</span><strong>${fmt(g.seconds)}</strong></div>`).join('')||'<p class="muted">В этом году сессий нет.</p>'}</div><p class="muted stats-note">По завершённым сессиям. Время Steam и ручные часы без дат сюда не входят.</p></section>`;
 }
 GaidenUI.components.annualPanel=()=>yearPanel(window.gaidenSessions||[],modelFeatures.yearValue);
 
 const settingsGroup=(title,subtitle,body)=>`<section class="panel settings-panel"><details class="settings-group" data-feature-group="${title}" ${modelFeatures.expandedGroups.has(title)?'open':''}><summary><span><strong>${title}</strong><small>${subtitle}</small></span><span class="settings-chevron" aria-hidden="true">›</span></summary><div class="settings-group-body">${body}</div></details></section>`;
 GaidenUI.components.settingsExtras=html=>{
  const t=modelFeatures.featureSettings.tracking;
  const section=settingsGroup('Запуск и учёт времени','Автозапуск и правило простоя',`<form id="tracking-form"><div class="setting-row"><label for="startup-enabled">Запускать с Windows в трее</label><input class="toggle" id="startup-enabled" name="startup" type="checkbox" ${modelFeatures.featureSettings.startup?'checked':''}></div><div class="setting-row"><label for="idle-enabled">Вычитать простой</label><input class="toggle" id="idle-enabled" name="excludeIdle" type="checkbox" ${t.excludeIdle?'checked':''}></div><label>Считать простоем после, минут<input name="idleMinutes" type="number" min="1" max="120" value="${t.idleMinutes}" required></label><p class="muted">При отсутствии ввода дольше порога последующее время считается простоем. Предыдущие минуты остаются игровыми. Старые сессии не пересчитываются.</p><button class="primary" type="submit">Сохранить</button></form>`);
  const diagnostic=settingsGroup('Диагностика','Журнал ошибок и сведения о версии',`<p class="muted">Архив содержит журнал, версию программы, Windows и количество записей. База игр, заметки, обложки и ключ Steam не включаются. Журнал может содержать пути к файлам из сообщений об ошибках.</p><button class="glass-button" type="button" data-export-diagnostics>Сохранить диагностику…</button>`);
  const template=document.createElement('template');template.innerHTML=html;const group=template.content.querySelector('.settings-groups');if(group)group.insertAdjacentHTML('beforeend',section+diagnostic);const holder=document.createElement('div');holder.append(template.content);return holder.innerHTML;
 };
 
 function historyHTML(id){
  const state=modelFeatures.historyState.get(id),all=F.gameSessions(window.gaidenSessions||[],id),selected=F.gameSessions(all,id,{year:state.year,month:state.month,day:state.day}),summary=F.year(all,state.year);
  const days=new Map();for(const s of selected){const key=GaidenStatistics.key(new Date(s.startedAt));days.set(key,(days.get(key)||0)+F.seconds(s));}
  const total=selected.reduce((n,s)=>n+F.seconds(s),0);
  return `<div class="section-heading"><h3>История игры</h3>${inputYear('game-history-year',state.year)}</div><div class="history-filters"><label>Месяц<select id="game-history-month"><option value="0">Весь год</option>${Array.from({length:12},(_,i)=>`<option value="${i+1}" ${state.month===i+1?'selected':''}>${GaidenUI.formatDate(new Date(2026,i),{month:'long'})}</option>`).join('')}</select></label><label>День<input id="game-history-day" type="date" value="${state.day&&state.month?state.year+'-'+String(state.month).padStart(2,'0')+'-'+String(state.day).padStart(2,'0'):''}"></label><button class="text-button" type="button" data-history-reset>Весь год</button></div><p><strong>${fmt(total)}</strong> · ${selected.length} сессий за выбранный период</p><p class="muted">За ${state.year}: ${fmt(summary.total)} · ${summary.days} игровых дней</p><details class="history-days"><summary>По дням · ${days.size}</summary>${[...days].map(([day,value])=>`<div class="ranking"><span>${GaidenUI.formatDate(new Date(day+'T12:00:00'),{day:'numeric',month:'short'})}</span><strong>${fmt(value)}</strong></div>`).join('')||'<p class="muted">Сессий нет.</p>'}</details><div class="table-wrap"><table><thead><tr><th>Начало</th><th>Активное время</th><th>Простой</th></tr></thead><tbody>${selected.slice(0,state.limit).map(s=>`<tr><td>${GaidenUI.formatDate(s.startedAt)}</td><td>${fmt(F.seconds(s))}</td><td>${Number.isFinite(s.idleSeconds)?fmt(s.idleSeconds):'—'}</td></tr>`).join('')||'<tr><td colspan="3">Нет сессий за выбранный период.</td></tr>'}</tbody></table></div><div class="session-pagination"><span class="muted">${Math.min(state.limit,selected.length)} из ${selected.length}</span>${selected.length>state.limit?'<button class="glass-button" type="button" data-game-history-more>Ещё 10</button>':''}</div><p class="muted stats-note">Дни — по местной дате начала. Неизвестный простой старых сессий показан прочерком.</p>`;
 }
 function refreshHistory(){const section=document.querySelector('#game-history');if(section)section.innerHTML=historyHTML(section.dataset.featureGame);}
 GaidenUI.detail.featureSections=(g)=>{const id=g.id;
  const section=document.createElement('section');section.className='detail-section time-adjustment';
  const baseline=Math.max(0,(Number(g.minutes)||0)-Math.floor((Number(g.trackedActiveSeconds)||0)/60));
  section.innerHTML=`<details><summary>Время до трекера · ${fmt(baseline*60)}</summary><form id="baseline-form" data-feature-game="${esc(id)}"><div class="two-fields"><label>Часы<input name="hours" type="number" min="0" max="1000000" step="1" value="${Math.floor(baseline/60)}" required></label><label>Минуты<input name="minutes" type="number" min="0" max="59" step="1" value="${baseline%60}" required></label></div><p class="muted">Время из старого счётчика или до установки Questlio. Записанные сессии останутся без изменений. Время Steam хранится отдельно; в библиотеке показывается большее из Steam и Questlio.</p><button class="glass-button" type="submit">Сохранить время</button><p class="muted">Записано трекером: ${fmt(Number(g.trackedActiveSeconds)||0)}</p></form></details>`;

  const all=F.gameSessions(window.gaidenSessions||[],id);modelFeatures.historyState.set(id,{year:all.length?new Date(all[0].startedAt).getFullYear():new Date().getFullYear(),month:0,day:0,limit:10});
  const history=document.createElement('section');history.id='game-history';history.className='detail-section';history.dataset.featureGame=id;history.innerHTML=historyHTML(id);history.hidden=!all.length;return [section,history];
 };
 document.addEventListener('toggle',e=>{const key=e.target.dataset?.featureGroup;if(key){if(e.target.open)modelFeatures.expandedGroups.add(key);else modelFeatures.expandedGroups.delete(key);}},true);
 document.addEventListener('change',e=>{
  if(e.target.id==='library-sort'){modelFeatures.sortMode=e.target.value;write('gaiden.librarySort.v1',modelFeatures.sortMode);document.querySelector('#library-grid').innerHTML=libraryCards();}
  if(e.target.id==='annual-year'&&e.target.checkValidity()){modelFeatures.yearValue=Number(e.target.value);render();document.querySelector('#annual-year')?.focus();}
  if(e.target.id.startsWith('game-history-')){if(!e.target.checkValidity())return;const id=document.querySelector('#game-history').dataset.featureGame,state=modelFeatures.historyState.get(id);if(e.target.id==='game-history-year'){state.year=Number(e.target.value);state.day=0;}if(e.target.id==='game-history-month'){state.month=Number(e.target.value);state.day=0;}if(e.target.id==='game-history-day'){const d=F.validDate(e.target.value+'T12:00:00');if(d){state.year=d.getFullYear();state.month=d.getMonth()+1;state.day=d.getDate();}else state.day=0;}state.limit=10;const focus=e.target.id;refreshHistory();document.getElementById(focus)?.focus();}
 });
 document.addEventListener('click',e=>{
  if(e.target.closest('[data-export-diagnostics]'))post({action:'exportDiagnostics'});
  if(e.target.closest('[data-game-history-more]')){modelFeatures.historyState.get(document.querySelector('#game-history').dataset.featureGame).limit+=10;refreshHistory();document.querySelector('[data-game-history-more]')?.focus();}
  if(e.target.closest('[data-history-reset]')){const s=modelFeatures.historyState.get(document.querySelector('#game-history').dataset.featureGame);s.month=0;s.day=0;s.limit=10;refreshHistory();document.querySelector('[data-history-reset]')?.focus();}
 });
 document.addEventListener('submit',e=>{
  if(e.target.id==='tracking-form'){e.preventDefault();const f=e.target.elements;post({action:'saveTracking',tracking:{excludeIdle:f.excludeIdle.checked,idleMinutes:Number(f.idleMinutes.value)},startup:f.startup.checked});}
  if(e.target.id==='baseline-form'){e.preventDefault();const f=e.target.elements;post({action:'setBaseline',id:e.target.dataset.featureGame,minutes:Number(f.hours.value)*60+Number(f.minutes.value)});}
 });
 window.chrome.webview.addEventListener('message',e=>{
  if(e.data.featureNotice){notify(e.data.featureNotice);return;}
  if(!e.data.featuresUpdated)return;
  modelFeatures.featureSettings=e.data.settings;window.gaidenFeatureSettings=modelFeatures.featureSettings;window.applyNativeSnapshot(e.data.snapshot);
  if(e.data.operation==='setBaseline'){const section=document.querySelector('#baseline-form');const g=games.find(x=>x.id===section?.dataset.featureGame);if(g){section.closest('details').querySelector('summary').textContent='Время до трекера · '+fmt(g.baselineMinutes*60);document.querySelector('.detail-stat strong').textContent=fmt(g.minutes*60);}}
  if(e.data.operation==='updateEmulator'){document.querySelector('#emulator-editor')?.close();notify('Эмулятор обновлён');}
  else if(e.data.operation!=='featureSettings')notify('Изменения сохранены');
 });
 window.gaidenFeatureTest={yearPanel,historyHTML,refreshHistory};
}
