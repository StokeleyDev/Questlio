const modelMedia=GaidenUI.moduleState('media',{failedSteamCovers:new Set()});
 
 const fallbackMedia=(g,uid,loading)=>{if(g.exeIcon?.startsWith('data:image/'))return `<div class="icon-cover" aria-hidden="true"><img class="game-icon" src="${escapeHTML(g.exeIcon)}" alt="" loading="${loading}"></div>`;const data=g.legacy?.icon?.data;if(data&&/^(\/9j\/|iVBOR)/.test(data))return `<div class="icon-cover" aria-hidden="true"><img class="game-icon" src="data:image/${data.startsWith('/9j/')?'jpeg':'png'};base64,${data}" alt="" loading="${loading}"></div>`;return `<div class="placeholder-cover" aria-hidden="true">${icon('game')}<span>${escapeHTML(g.name)}</span></div>`;};
 function media(g,uid,loading='lazy'){
   if(g.customCover?.startsWith('data:image/'))return `<img class="game-art" src="${escapeHTML(g.customCover)}" alt="" loading="${loading}">`;
   const appId=Number(g.steam?.appId);
   if(Number.isSafeInteger(appId)&&appId>0&&window.gaidenCoverState?.enabled&&window.gaidenCoverState.available?.includes(appId)&&!modelMedia.failedSteamCovers.has(appId))return `<img class="game-art steam-poster cover-loading" data-steam-cover="${appId}" data-cover-game="${escapeHTML(g.id)}" src="https://covers.gaiden.local/${appId}.jpg" alt="" loading="${loading}">`;
   if(window.gaidenCoverState?.enabled&&window.gaidenCoverState.pending?.includes(appId))return `<div class="poster-skeleton ui-skeleton-card" aria-hidden="true" aria-busy="true"></div>`;
   return fallbackMedia(g,uid,loading);
 };
 document.addEventListener('error',event=>{const image=event.target;if(!image.matches?.('img[data-steam-cover]'))return;const appId=Number(image.dataset.steamCover);modelMedia.failedSteamCovers.add(appId);for(const broken of document.querySelectorAll('img[data-steam-cover]'))if(Number(broken.dataset.steamCover)===appId){const g=games.find(x=>x.id===broken.dataset.coverGame);if(g)broken.outerHTML=fallbackMedia(g,'fallback','lazy');}},true);
