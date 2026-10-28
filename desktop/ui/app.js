const $=(selector,root=document)=>root.querySelector(selector);
const icon=name=>`<svg aria-hidden="true"><use href="#${name}"/></svg>`;
const escapeHTML=value=>String(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const statusNames={playing:'Играю',backlog:'На потом',finished:'Пройдено',hold:'На паузе',dropped:'Брошено',forever:'Постоянно'};
const fmt=value=>{const total=Math.floor(Math.max(0,Number(value)||0));if(total<60)return QuestlioLocale.language==='en'?'Less than a minute':'меньше минуты';const minutes=Math.floor(total/60);if(minutes<60)return QuestlioLocale.language==='en'?`${minutes} min`:`${minutes} мин`;const hours=Math.floor(minutes/60),rest=minutes%60;return QuestlioLocale.language==='en'?`${hours} h${rest?` ${rest} min`:''}`:`${hours} ч${rest?` ${rest} м`:''}`;};
const fmtCompact=value=>{const total=Math.floor(Math.max(0,Number(value)||0));if(total<60)return QuestlioLocale.language==='en'?'< 1 min':'< 1 мин';const minutes=Math.floor(total/60);if(minutes<60)return QuestlioLocale.language==='en'?`${minutes} min`:`${minutes} мин`;const hours=Math.floor(minutes/60),rest=minutes%60;return QuestlioLocale.language==='en'?`${hours} h${rest?` ${rest} min`:''}`:`${hours} ч${rest?` ${rest} мин`:''}`;};
const dateFormats=new Map();
GaidenUI.formatDate=(value,options={day:'numeric',month:'short',hour:'2-digit',minute:'2-digit'})=>{const date=value instanceof Date?value:new Date(value);if(!Number.isFinite(date.getTime()))return 'Дата неизвестна';const settings={...options};delete settings.second;const key=JSON.stringify(settings);if(!dateFormats.has(key))dateFormats.set(key,new Intl.DateTimeFormat((QuestlioLocale.language==='en'?'en-US':'ru-RU'),settings));return dateFormats.get(key).format(date);};
let toastTimer,lastFocused;
function read(key, fallback) {try {const value=localStorage.getItem(key);return value ? JSON.parse(value) : fallback;}catch {return fallback;}}
function write(key,value){
 if(key==='gaiden.games.v1'&&window.chrome?.webview){window.chrome.webview.postMessage({action:'save',games:value});return;}
 try{localStorage.setItem(key,JSON.stringify(value));}catch{storageAvailable=false;notify('Хранилище недоступно. Изменения останутся до закрытия страницы.');}
}
function notify(message,options) {if(GaidenUI.components.kit)return GaidenUI.components.kit.toast(message,options);(GaidenUI.state.earlyNotices??=[]).push([message,options]);}
function applyPrefs(){GaidenTheme.apply(prefs);$('#theme-toggle').setAttribute('aria-pressed',String(!!prefs.dark));window.chrome?.webview?.postMessage({action:'themeChanged',dark:!!prefs.dark});}
function plural(n,forms){const a=n%100,b=n%10;return forms[a>10&&a<20?2:b===1?0:b>=2&&b<=4?1:2];}
function heading(title,subtitle=''){return `<div class="heading"><div><h1>${title}</h1>${subtitle?`<p class="subtitle">${subtitle}</p>`:''}</div></div>`;}
function displayMinutes(g){return Math.max(Number(g.minutes)||0,Number(g.steam?.minutes)||0);}
function metrics(){return GaidenUI.components.overviewMetrics();}
function card(g){return `<button class="game-card" data-game="${escapeHTML(g.id)}" aria-label="Открыть ${escapeHTML(g.name)}"><div class="cover">${media(g,'card-'+g.id)}</div><div class="game-name">${escapeHTML(g.name)}</div><div class="game-meta"><span class="game-meta-platform">${escapeHTML(g.platform)}</span><span class="game-meta-time"${g.steam?` title="${escapeHTML('Steam: '+fmt(g.steam.minutes*60)+' · Questlio: '+fmt(g.minutes*60))}"`:""}>${g.steam?fmtCompact(displayMinutes(g)*60):fmtCompact(g.minutes*60)}</span></div><span class="game-status status-${g.status}"><span class="status-dot" aria-hidden="true"></span>${statusNames[g.status]}</span></button>`;}
function renderHome(){const g=games[0];if(!g)return heading('Обзор')+`<section class="panel home-empty"><div class="home-empty-icon" aria-hidden="true">${icon('game')}</div><h2>Библиотека пока пуста</h2><p class="muted">Добавьте игру или импортируйте часы из Steam — статистика появится после первого запуска.</p><div class="empty-actions"><button class="primary" data-action="add">Добавить игру</button><button class="glass-button" data-steam-open>Импорт из Steam</button></div></section>`;return heading('Обзор')+`<section class="hero">${media(g,'hero','eager')}<div class="hero-content"><div class="hero-platform">Продолжить</div><h2>${escapeHTML(g.name)}</h2><p class="hero-summary">Играли ${fmt(g.minutes*60)} · последний раз ${GaidenDetail.lastPlayed(g).toLocaleLowerCase('ru')}</p><div class="hero-bottom">${GaidenUI.components.playButton(g)}<button class="glass-button" data-game="${escapeHTML(g.id)}">Подробнее ${icon('arrow')}</button></div></div></section>`+metrics()+`<section class="recent-section"><div class="section-heading"><h2>Последние игры</h2><button class="text-button" data-view="library">Вся библиотека ${icon('arrow')}</button></div><div class="cover-grid">${games.slice(0,4).map(card).join('')}</div></section><div class="overview-lower">${weeklyPanel()}${sessionPanel()}</div>`;}
function renderLibrary(){return libraryHeading()+`<div class="tools"><label class="search-box">${icon('search')}<input id="library-search" type="search" aria-label="Поиск по библиотеке" placeholder="Поиск игр" value="${escapeHTML(search)}"></label><div class="segmented" role="group" aria-label="Фильтр по статусу">${[['all','Все'],['playing','Играю'],['backlog','На потом'],['finished','Пройдено']].map(([id,label])=>`<button data-filter="${id}" class="${filter===id?'selected':''}" aria-pressed="${filter===id}">${label}</button>`).join('')}</div>${GaidenUI.components.sortControl()}</div><div class="cover-grid library-grid" id="library-grid">${libraryCards()}</div>`;}
function libraryHeading(){const template=document.createElement('template');template.innerHTML=heading('Библиотека',`${games.length} ${plural(games.length,['игра','игры','игр'])}${games.length?' · '+fmt(games.reduce((sum,g)=>sum+(Number(g.minutes)||0),0)*60)+' в Questlio':''}`);const button=document.createElement('button');button.className='glass-button';button.type='button';button.dataset.steamOpen='';button.textContent='Импорт из Steam';template.content.querySelector('.heading').append(button);const host=document.createElement('div');host.append(template.content);return host.innerHTML;}
function libraryCards(){return GaidenUI.components.libraryCards();}
function weeklyPanel(){return GaidenUI.components.weeklyPanel();}
function sessionPanel(){return GaidenUI.components.sessionPanel();}
function renderStats(){return GaidenUI.screens.stats();}
function renderComputer(){return GaidenUI.screens.computer();}
function renderSessions(){return GaidenUI.screens.sessions();}
function renderSettings(){return GaidenUI.layouts.settings(GaidenUI.settings.appearance()+GaidenUI.settings.emulators()+GaidenUI.settings.backups());}
function render(){
 if(!GaidenUI.state.ready)return;
 const labels={home:'Обзор',library:'Библиотека',stats:'Статистика',sessions:'История сессий',settings:'Настройки',computer:'Мой компьютер'};
 document.querySelectorAll('.nav-item').forEach(button=>{button.classList.toggle('active',button.dataset.view===view);if(button.dataset.view===view)button.setAttribute('aria-current','page');else button.removeAttribute('aria-current');});$('#library-count').textContent=games.length;
 const html=({home:renderHome,library:renderLibrary,stats:renderStats,sessions:renderSessions,settings:renderSettings,computer:renderComputer})[view]();
 GaidenUI.paint($('#main'),html);GaidenUI.emit('rendered');
}
function navigate(next){const previous=view;GaidenUI.emit('beforeNavigate',next,previous);view=next;history.replaceState(null,'','#'+view);render();window.scrollTo({top:0,behavior:'instant'});GaidenUI.emit('navigated',next,previous);}
function openDetail(id){
 if($('#detail-dialog').open||GaidenUI.state.detailClosing)return;const game=games.find(g=>g.id===id);if(!game)return;
 GaidenUI.emit('beforeDetail',id);lastFocused=document.activeElement;
 const card=document.querySelector(`.game-card[data-game="${CSS.escape(String(id))}"]`),poster=card?.querySelector('.cover');
 const clear=()=>document.querySelectorAll('[style*="view-transition-name"]').forEach(node=>{node.style.viewTransitionName='';});
 const run=()=>{clear();GaidenUI.detail.build(game);$('#detail-dialog').showModal();GaidenUI.emit('detailOpened',id);const art=$('#detail-dialog .detail-art');if(art)GaidenMotion?.armPoster('gaiden-art',art);};
 if(!poster||!GaidenMotion?.armPoster('gaiden-art',poster)){run();return;}
 const transition=GaidenMotion.transition(run,'detail');
 if(!transition)clear();else transition.finished.finally(clear);
}
