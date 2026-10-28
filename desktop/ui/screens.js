(() => {
const modelSessions=GaidenUI.moduleState('sessions',{sessionLimit:30});
 const snapshot=()=>GaidenUI.state.snapshot;
 const ordered=()=>[...snapshot().sessions].sort((a,b)=>String(b.startedAt).localeCompare(String(a.startedAt)));
 const date=s=>s.startedAt?GaidenUI.formatDate(s.startedAt):'Дата неизвестна';
 
 GaidenUI.on('beforeNavigate',(next,previous)=>{if(next==='sessions'&&previous!=='sessions')modelSessions.sessionLimit=30;});
 document.addEventListener('click',e=>{if(!e.target.closest('[data-more-sessions]'))return;modelSessions.sessionLimit+=30;render();document.querySelector('[data-more-sessions]')?.focus({preventScroll:true});});
 const fmtSession=s=>fmt(Number.isFinite(s.activeSeconds)?s.activeSeconds:(s.activeMinutes||0)*60);
 const sessionRows = () => ordered().slice(0,modelSessions.sessionLimit).map(s=>{
   const g=games.find(g=>g.id===s.gameId);
   return `<tr><td>${g?`<button class="text-button" data-game="${escapeHTML(g.id)}">${escapeHTML(g.name)}</button>`:escapeHTML(s.legacyGameName)}</td><td>${escapeHTML(date(s))}</td><td>${fmtSession(s)}</td><td>${escapeHTML(g?.platform||'—')}</td></tr>`;
 }).join('');
GaidenUI.screens.sessions=()=>GaidenUI.components.sessionHistory();
GaidenUI.components.sessionPanel=() => `<section class="panel session-panel"><div class="section-heading"><h2>Последние сессии</h2><button class="text-button" data-view="sessions">Все ${icon('arrow')}</button></div>${ordered().slice(0,3).map(s=>`<div class="session-item"><span class="session-text"><strong>${escapeHTML(s.legacyGameName)}</strong><small>${escapeHTML(date(s))}</small></span><span>${fmtCompact(Number.isFinite(s.activeSeconds)?s.activeSeconds:(s.activeMinutes||0)*60)}</span></div>`).join('')||'<p>Сессий пока нет.</p>'}</section>`;
GaidenUI.settings.appearance=() => heading('Настройки')+`<section class="panel settings-panel"><h2>Внешний вид</h2><div class="setting-row"><div class="setting-copy"><label for="pref-language">Язык</label></div><select id="pref-language" data-questlio-language><option value="en" ${QuestlioLocale.language==='en'?'selected':''}>English</option><option value="ru" ${QuestlioLocale.language==='ru'?'selected':''}>Русский</option></select></div><div class="setting-row"><div class="setting-copy"><label for="pref-theme">Тема</label><small id="pref-theme-description">Авто следует настройкам Windows</small></div><select id="pref-theme" data-theme-mode>${[['auto','Авто'],['light','Светлая'],['dark','Тёмная']].map(([id,title])=>`<option value="${id}" ${prefs.theme===id?'selected':''}>${title}</option>`).join('')}</select></div>${[['reduced','Меньше движения'],['opaque','Меньше прозрачности']].map(([id,title])=>`<div class="setting-row"><div class="setting-copy"><label for="pref-${id}">${title}</label><small id="pref-${id}-description">${id==='reduced'?'Отключает анимации интерфейса':'Заменяет стеклянные поверхности сплошными'}</small></div><input id="pref-${id}" class="toggle" type="checkbox" data-pref="${id}" ${prefs[id]?'checked':''}></div>`).join('')}</section>`;
})();
