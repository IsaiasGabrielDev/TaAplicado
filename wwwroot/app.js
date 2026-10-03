'use strict';

const $ = s => document.querySelector(s);
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const icon = (id, cls = 'ic') => `<svg class="${cls}" aria-hidden="true"><use href="#${id}"/></svg>`;
const bridge = typeof window.external?.sendMessage === 'function';
const send = msg => bridge ? window.external.sendMessage(JSON.stringify(msg)) : window.demo?.handle(msg);

let S = null;              // last state from the host
let first = true;
let filter = 'all';        // Histórico status filter
let profileDirty = false;
let openRow = null;        // Histórico row showing what happened + answers sent
const seen = {};           // section → last rendered JSON
const UNDO_MS = 5000;
const pendingAnswers = new Map(); // aviso id → { answer, timer }   (Responder, undoable)
const pendingDeletes = new Map(); // answer key → timer              (Excluir, undoable)

/* ── formatting ─────────────────────────────────────────── */
const kind = st => st.startsWith('skip') ? 'skip' : st.startsWith('failed') ? 'failed' : st;
const LABEL = { applied: 'Enviada', waiting: 'Aguardando você', ready: 'Na fila', 'dry-run': 'Teste', skip: 'Pulada', failed: 'Falhou' };
const REASON = {
  'no easy apply': 'sem Easy Apply', 'modal did not open': 'a janela do Easy Apply não abriu',
  'no next button': 'botão Avançar não encontrado', 'stuck on a step': 'travou numa etapa', 'too many steps': 'etapas demais',
};
const reason = st => {
  const r = st.startsWith('skip: not relevant') ? 'fora do perfil: ' + st.slice(19).trim()
    : st.startsWith('skip: blocked') ? st.slice(14).trim()
    : st.includes(':') ? st.slice(st.indexOf(':') + 1).trim() : '';
  return REASON[r] ?? r;
};
const stamp = st => `<span class="stamp ${kind(st)}">${LABEL[kind(st)] ?? esc(st)}</span>`;
const isIndeed = id => String(id).startsWith('indeed-');
const code = id => isIndeed(id) ? `<span class="code indeed"><span>${esc(id.slice(7))}</span></span>`
  : `<span class="code"><span>${esc(String(id).replace(/\B(?=(\d{3})+(?!\d))/g, ' '))}</span></span>`;
const splitJob = job => { const i = (job ?? '').lastIndexOf(' | '); return i < 0 ? [job ?? '', ''] : [job.slice(0, i), job.slice(i + 3)]; };
const pad = n => String(n).padStart(2, '0');
const hhmm = at => { const d = new Date(at); return `${pad(d.getHours())}:${pad(d.getMinutes())}`; };
const hhmmss = at => `${hhmm(at)}:${pad(new Date(at).getSeconds())}`;
const ddmm = at => { const d = new Date(at); return `${pad(d.getDate())}/${pad(d.getMonth() + 1)} ${hhmm(at)}`; };
const ago = at => {
  const m = Math.round((Date.now() - new Date(at)) / 60000);
  return m < 1 ? t('agora') : m < 60 ? t('há {n} min', { n: m }) : m < 1440 ? t('há {n} h', { n: Math.round(m / 60) }) : ddmm(at);
};
const plural = (n, one, many) => `${n} ${t(n === 1 ? one : many)}`; // the words translate (i18n.js)
const announce = text => { $('#live').textContent = ''; setTimeout(() => $('#live').textContent = text, 50); };

/* Renders a section only when its data changed, and never under the user's cursor. */
function put(el, key, data, html) {
  const json = JSON.stringify(data);
  if (seen[key] === json) return;
  // Only protect what the user is typing into; a focused button must not freeze the section.
  if (el.contains(document.activeElement) && document.activeElement.matches('input, textarea, select')) return; // retried on the next push
  seen[key] = json;
  el.innerHTML = html();
}

/* ── state in ───────────────────────────────────────────── */
let userSeen = null;
let avisosSeen = null;
let runningSeen = false;
window.onState = state => {
  S = state;
  // A different user is a different app: forget everything rendered for the previous one.
  if (userSeen !== null && userSeen !== S.currentUser) {
    for (const k in seen) delete seen[k];
    profileSeen = null; profileDirty = false; openRow = null; filter = 'all'; clearColFilters(); avisosSeen = null;
    pendingAnswers.forEach(p => clearTimeout(p.timer)); pendingAnswers.clear();
    pendingDeletes.forEach(clearTimeout); pendingDeletes.clear();
    $('#save-profile').disabled = true;
    $('#save-note').textContent = $('#import-note').textContent = $('#import-url-note').textContent = '';
    $('#import-text').value = $('#import-url').value = '';
    $('#sieve-note').textContent = '';
    asked = []; $('#ask-text').value = $('#ask-note').textContent = $('#ask-msg-text').value = ''; $('#ask-msg').hidden = true;
    closeErase();
    first = true; location.hash = '';
  }
  userSeen = S.currentUser;
  if (avisosSeen !== null && S.avisos.length > avisosSeen) announce(plural(S.avisos.length, 'aviso esperando você', 'avisos esperando você'));
  avisosSeen = S.avisos.length;
  if (runningSeen && !S.running) {
    const c = S.lastRun?.counts ?? {};
    announce([t('Execução terminada'), ...RECEIPT.filter(([k]) => c[k]).map(([k, one, many]) => plural(c[k], one, many)),
      S.lastRun?.ultra ? `${t('Ultra seleção')}: ${S.lastRun.ultra}` : ''].filter(Boolean).join('. '));
  }
  runningSeen = S.running;

  setLanguage(S.lang);
  document.querySelectorAll('[name=lang]').forEach(r => r.checked = r.value === S.lang);
  renderWho();
  $('#erase').disabled = S.running || S.retrying;
  $('#export-user').disabled = $('#import-user').disabled = S.running || S.reading || S.retrying;
  $('#import').hidden = S.noAi; // the import is Claude reading the text
  if (first) {
    first = false;
    const [screen, row] = location.hash.slice(1).split(':');
    openRow = row ?? null;
    if (screen) show(screen);
    else if (!S.ledger.length && profileGaps(S.profile).length > FIELDS.length / 2) show('remetente');
  }
  renderSetup();
  renderDispatch();
  renderSieve();
  renderPile();
  renderFranking();
  renderReceipt();
  renderTransit();
  renderTrack();
  renderLedger();
  renderAnswers();
  renderSender();
  renderResumes();
  renderAsk();
};

/* ── navigation ─────────────────────────────────────────── */
function show(id) {
  if (id === 'conferencia') { id = 'caixa'; $('#f-o').value = 'review'; } // old link target
  document.querySelectorAll('.tab').forEach(t => t.dataset.screen === id ? t.setAttribute('aria-current', 'page') : t.removeAttribute('aria-current'));
  document.querySelectorAll('.screen').forEach(s => s.hidden = s.id !== id);
  if (S) renderAnswers();
}
document.querySelectorAll('.tab').forEach(t => t.addEventListener('click', () => show(t.dataset.screen)));
// Ctrl+1…6 jump between the tabs, in their order on the bar.
document.addEventListener('keydown', e => {
  const tab = e.ctrlKey && !e.altKey && !e.shiftKey && /^[1-6]$/.test(e.key) && document.querySelectorAll('.tab')[e.key - 1];
  if (tab) { e.preventDefault(); show(tab.dataset.screen); tab.focus(); }
});
document.addEventListener('click', e => {
  const go = e.target.closest('[data-go]');
  if (!go) return;
  show(go.dataset.go);
  if (go.dataset.to) { const t = $(go.dataset.to); t.scrollIntoView({ block: 'start' }); t.querySelector('textarea, input')?.focus({ preventScroll: true }); }
});

/* ── candidatar: run controls ───────────────────────────── */
const form = $('#dispatch');
const isTest = () => form.run.value === 'test';
// Terms only exist for a search (LinkedIn or Indeed); Recomendadas has nothing to type, so the field leaves the bar.
// The site switch picks where to look; Indeed has only its search, so its origin is always the terms.
const pickMode = () => form.site.value === 'indeed' ? 'indeed' : form.mode.value;
const typed = () => pickMode() === 'search' || pickMode() === 'indeed';
function syncTerms() {
  form.terms.disabled = S?.running;
  form.terms.closest('.field').hidden = !typed();
  $('#origin').hidden = form.site.value === 'indeed';
  renderSuggest();
}

// Claude's search ideas for the profile: a click puts one in the terms field.
function renderSuggest() {
  const list = S?.searches || [];
  $('#suggest').hidden = !typed() || !S || S.noAi;
  $('#suggest-list').innerHTML = list.map(t => `<button class="chip" type="button" data-term="${esc(t)}"${S?.running ? ' disabled' : ''}>${esc(t)}</button>`).join('');
  const go = $('#suggest-go');
  go.disabled = !!S?.suggesting;
  go.textContent = S?.suggesting ? 'Claude pensando…' : list.length ? 'Sugerir outras' : 'Pedir sugestões ao Claude';
}
$('#suggest-list').addEventListener('click', e => {
  const chip = e.target.closest('[data-term]');
  if (!chip) return;
  form.terms.value = chip.dataset.term;
  form.terms.setCustomValidity('');
  form.terms.focus();
});
$('#suggest-go').addEventListener('click', () => send({ type: 'suggestSearches' }));
// 0 is a valid floor ("send the best N whatever their score"); only junk falls back to 70.
const minScore = () => { const n = Math.round(+form.minScore.value); return form.minScore.value !== '' && Number.isFinite(n) ? Math.min(100, Math.max(0, n)) : 70; };
form.addEventListener('change', () => { syncTerms(); if (S) { renderDispatch(); renderSieve(); } });
// The first real run of a session asks once, inline: the button turns into "Confirmar" and the note says what goes out.
// No timeout (the user reads at their own pace); a second click within 600 ms is a double-click, not a decision.
let realOk = false, armed = null, armedAt = 0;
function needsConfirm(which) {
  if (isTest() || realOk) return false;
  if (armed === which) {
    if (Date.now() - armedAt < 600) return true;
    armed = null; realOk = true; return false;
  }
  armed = which; armedAt = Date.now();
  renderDispatch();
  announce($('#go-note').textContent);
  return true;
}
const disarm = () => { if (armed) { armed = null; renderDispatch(); } };
form.addEventListener('input', disarm); // a changed setup is a different send: ask again
document.addEventListener('click', e => { if (armed && !e.target.closest('#go, #go-queue')) disarm(); });
form.addEventListener('submit', e => {
  e.preventDefault();
  if (S?.running) return send({ type: 'stop' });
  if (typed() && !form.terms.value.trim()) { form.terms.focus(); form.terms.setCustomValidity(t('Escreva os termos da pesquisa')); form.terms.reportValidity(); return; }
  if (needsConfirm('go')) return;
  const ultra = form.pick.value === 'ultra';
  send({ type: 'start', mode: pickMode(), terms: form.terms.value.trim(), max: Math.max(1, +form.max.value || 25), dryRun: isTest(),
    ultra, minScore: minScore() });
});
form.terms.addEventListener('input', () => form.terms.setCustomValidity(''));
$('#go-queue').addEventListener('click', () => {
  const n = S.ledger.filter(e => e.status === 'ready').length;
  if (!n || S.running || needsConfirm('queue')) return;
  send({ type: 'start', mode: 'queue', terms: '', max: Math.min(n, 100), dryRun: isTest(), ultra: false, minScore: 0 });
});

function renderDispatch() {
  const go = $('#go');
  form.querySelectorAll('input').forEach(i => i.disabled = S.running);
  // Ultra ranks by Claude's score: nothing to rank without AI.
  const ultraPick = form.querySelector('[name="pick"][value="ultra"]');
  // Ultra ranks by Claude's score, and only runs on LinkedIn for now.
  ultraPick.disabled ||= S.noAi || pickMode() === 'indeed';
  if (ultraPick.disabled && ultraPick.checked) form.pick.value = 'normal';
  syncTerms();
  go.disabled = S.stopping || (!S.running && (!setupReady() || S.reading)); // reading a profile holds the app's Chrome
  go.classList.toggle('primary', !S.running && armed !== 'go');
  go.classList.toggle('armed', armed === 'go');
  const max = Math.max(1, +form.max.value || 25);
  go.innerHTML = S.stopping ? '<span>Parando…</span>'
    : S.running ? `${icon('i-stop')}<span>Parar</span>`
    : armed === 'go' ? `${icon('i-check')}<span>Confirmar envio</span>`
    : `${icon('i-send')}<span>${isTest() ? 'Candidatar em teste' : 'Candidatar de verdade'}</span>`;
  // Jobs whose avisos were answered: a run can go straight to them, without opening the list.
  const queued = S.ledger.filter(e => e.status === 'ready').length;
  const gq = $('#go-queue');
  gq.hidden = S.running || !queued;
  gq.disabled = !setupReady() || S.reading;
  gq.classList.toggle('armed', armed === 'queue');
  gq.textContent = armed === 'queue' ? 'Confirmar envio'
    : `${isTest() ? 'Testar' : 'Enviar'} só ${queued === 1 ? 'a 1 vaga' : `as ${queued} vagas`} na fila`;
  const ultra = form.pick.value === 'ultra';
  $('#min-wrap').hidden = !ultra;
  const min = minScore();
  const me = S.users.find(u => u.id === S.currentUser)?.name;
  // Armed: restate exactly what goes out, so the second click is a decision about this send.
  const ticket = armed === 'queue' ? `${t('Só a fila')} · ${plural(queued, 'vaga', 'vagas')}`
    : [{ indeed: `Indeed · “${form.terms.value.trim()}”`, search: t('Pesquisa “{q}”', { q: form.terms.value.trim() }), preferences: t('Baseado nas preferências'), profile: t('Combinam com seu perfil') }[pickMode()] ?? t('Recomendadas'),
       ultra ? t('Ultra seleção, nota ≥ {n}', { n: min }) : t('Normal'), t('até {n}', { n: max })].join(' · ');
  $('#go-note').textContent = S.running ? ''
    : armed ? t('{ticket}, em nome de {me}. Clique de novo para confirmar; mude algo acima para cancelar.', { ticket, me: me ?? t('você') })
    : (ultra ? t('Analisa até {a}, {verb} até {max} com nota ≥ {min}.', { a: Math.min(max * 5, 150), verb: t(isTest() ? 'testa' : 'envia'), max, min })
      : isTest() ? t('Preenche e descarta. Nada é enviado.')
      : t('Até {n} em seu nome.', { n: plural(max, 'candidatura enviada', 'candidaturas enviadas') }))
      + (!isTest() && max > 50 ? ' ' + t('Muitos envios num dia podem fazer o LinkedIn restringir a conta.') : '');
  $('#go-note').classList.toggle('armed', !!armed);
  // The one thing that must never be ambiguous while it runs: is this real?
  const mode = $('#run-mode');
  mode.hidden = !S.running;
  mode.className = `run-mode ${S.runDryRun ? 'test' : 'real'}`;
  mode.innerHTML = S.runDryRun
    ? '<span class="stamp dry-run">Teste</span> nada é enviado'
    : '<span class="stamp applied">Enviando de verdade</span> em seu nome';
  if (S.runUltra) mode.insertAdjacentHTML('beforeend', `<span class="run-phase">Ultra seleção · ${esc(S.runPhase ?? 'começando')}
    ${S.canStopScanning ? '<button class="btn" type="button" data-action="stopScanning">Parar análise e aplicar já</button>' : ''}</span>`);
  $('#login').hidden = !S.attention;
  $('#login').textContent = S.attention ?? '';
}
$('#run-mode').addEventListener('click', e => {
  const b = e.target.closest('[data-action="stopScanning"]');
  if (!b) return;
  b.disabled = true; b.textContent = 'Parando a análise…'; // one click: Ultra keeps vetting the job already in flight, then moves on
  send({ type: 'stopScanning' });
});

/* ── the run receipt: what happened while you were away, and why it stopped ── */
const RECEIPT = [['applied', 'enviada', 'enviadas'], ['dry-run', 'testada', 'testadas'], ['waiting', 'vaga aguardando você', 'vagas aguardando você'],
  ['skip', 'pulada', 'puladas'], ['failed', 'falha', 'falhas']];
const secsTo = at => Math.max(0, Math.ceil((new Date(at) - Date.now()) / 1000));
function renderReceipt() {
  const r = S.lastRun?.at > '0001' ? S.lastRun : null;
  const box = $('#receipt');
  box.hidden = !r;
  if (!r) return;
  const c = r.counts ?? {};
  // Attempts count toward Máximo; skips don't (same rule as the runner).
  const tried = ['applied', 'dry-run', 'waiting', 'failed'].reduce((n, k) => n + (c[k] ?? 0), 0);
  const tally = RECEIPT.filter(([k]) => c[k]).map(([k, one, many]) => plural(c[k], one, many)).join(' · ') || 'nenhuma vaga processada ainda';
  const head = S.running
    ? `Desde ${hhmm(r.at)}${r.max ? ` · <b>${tried} de ${r.max}</b>` : ''}`
    : `Última execução${r.dryRun ? ' (teste)' : ''} · ${hhmm(r.at)}${r.endedAt ? `–${hhmm(r.endedAt)}` : ''}, ${ago(r.endedAt ?? r.at)}`;
  const tail = S.running
    ? (S.nextAt && secsTo(S.nextAt) > 0 ? `Próxima vaga em <b data-next>${secsTo(S.nextAt)}</b> s` : S.currentJob ? 'Numa vaga agora' : '')
    : r.end === 'você parou' ? '<b>Parada por você</b>' : r.end ? `Parou: <b>${esc(r.end)}</b>` : '';
  put(box, 'receipt', [r, S.running, S.nextAt, S.currentJob], () =>
    `<p class="receipt-head">${head}</p><p class="receipt-tally">${tally}</p>${tail ? `<p class="receipt-end">${tail}</p>` : ''}`);
}
setInterval(() => { const n = $('#receipt [data-next]'); if (n && S?.nextAt) n.textContent = secsTo(S.nextAt); }, 1000);

/* ── job filters: edited on Perfil, summarized on Candidatar; read at the start of each run ── */
const sieve = $('#sieve-form');
// Same split as Filters.Entries: companies one per line (names carry commas), words also on , and ;
const entries = (t, commas) => [...new Set(t.split(commas ? /[\n,;]/ : /\n/).map(l => l.trim()).filter(Boolean))];
const NO_FILTERS = { companies: [], words: [], useClaude: true, country: '', countryMode: 'off', language: '', languageMode: 'off' };
let sieveTimer = null;
function renderSieve() {
  const f = S.filters ?? NO_FILTERS;
  const list = (items, one, many) => items.length
    ? `${t(items.length === 1 ? one : many)} ${items.slice(0, 4).map(x => `<b translate="no">${esc(x)}</b>`).join(' · ')}${items.length > 4 ? t(' e mais {n}', { n: items.length - 4 }) : ''}`
    : '';
  const parts = [list(f.companies, 'empresa', 'empresas'), list(f.words, 'palavra', 'palavras')].filter(Boolean);
  const claude = t(S.noAi ? 'sem IA' : form.pick.value === 'ultra' ? 'Claude dá nota' : f.useClaude ? 'Claude julga' : 'sem Claude');
  const geo = [[f.country, f.countryMode], [f.language, f.languageMode]]
    .filter(([v, m]) => v?.trim() && m !== 'off')
    .map(([v, m]) => `${t(m === 'only' ? 'só' : 'prioriza')} <b translate="no">${esc(v.trim())}</b>`).join(' · ');
  // The receipt of the last run: what each filter entry skipped, and the Ultra outcome.
  const r = S.lastRun?.at && S.lastRun.at > '0001' ? S.lastRun : null;
  const hits = Object.entries(r?.blocked ?? {}).sort((a, b) => b[1] - a[1]);
  const skipped = hits.reduce((n, [, c]) => n + c, 0);
  const cell = (k, v) => `<span class="ficha-cell"><span class="field-label">${k}</span> ${v}</span>`;
  put($('#ficha'), 'ficha', [f, claude, geo, r, S.running], () => '<span class="ficha-row">' +
    cell(t('Filtros'), `${parts.length ? parts.join('; ') : t('nenhuma empresa ou palavra')}${geo ? ` · ${geo}` : ''} · ${claude}
      <button class="link" type="button" data-go="remetente" data-to="#filtros">Editar</button>`)
    + (r ? cell(t(S.running ? 'Nesta execução' : 'Na última execução'), skipped
      ? `${t('filtros pularam {n}', { n: `<b>${skipped}</b>` })}: ${hits.slice(0, 4).map(([k, c]) => `<span translate="no">${esc(k)}</span> <b>${c}</b>`).join(' · ')}`
      : t('nenhuma vaga pulada pelos filtros')) : '')
    + (r?.ultra ? cell('Ultra', esc(r.ultra)) : '') + '</span>');
  // Never under the cursor, and never over an edit that is still waiting to be saved.
  if (sieveTimer || (sieve.contains(document.activeElement) && document.activeElement.matches('textarea, input[type=text]'))) return;
  sieve.companies.value = f.companies.join('\n');
  sieve.words.value = f.words.join('\n');
  sieve.useClaude.checked = f.useClaude;
  sieve.useClaude.disabled = S.noAi;
  noAi.checked = S.noAi;
  noAi.disabled = S.running;
  sieve.country.value = f.country ?? '';
  sieve.language.value = f.language ?? '';
  sieve.countryMode.value = f.countryMode ?? 'off';
  sieve.languageMode.value = f.languageMode ?? 'off';
}
function saveSieve() {
  clearTimeout(sieveTimer); sieveTimer = null;
  send({ type: 'saveFilters', companies: entries(sieve.companies.value, false), words: entries(sieve.words.value, true), useClaude: sieve.useClaude.checked,
    country: sieve.country.value.trim(), countryMode: sieve.countryMode.value, language: sieve.language.value.trim(), languageMode: sieve.languageMode.value });
  $('#sieve-note').textContent = `Salvo às ${hhmm(Date.now())}.${S?.running ? ' Vale a partir da próxima execução.' : ''}`;
}
sieve.addEventListener('submit', e => e.preventDefault());
const noAi = $('#no-ai');
noAi.addEventListener('input', e => e.stopPropagation());
noAi.addEventListener('change', e => {
  e.stopPropagation();
  send({ type: 'setNoAi', on: noAi.checked });
  announce(noAi.checked ? 'Modo sem IA ligado' : 'Modo sem IA desligado');
});
sieve.addEventListener('input', () => { clearTimeout(sieveTimer); sieveTimer = setTimeout(saveSieve, 800); });
// Blur or checkbox: save now, and say it once (typing pauses save silently).
sieve.addEventListener('change', () => { saveSieve(); announce('Filtros salvos'); });
addEventListener('beforeunload', () => { if (sieveTimer) saveSieve(); });

/* ── user selector ──────────────────────────────────────── */
const who = $('#who-menu');
function renderWho() {
  const me = S.users.find(u => u.id === S.currentUser);
  $('#who-name').textContent = me?.name ?? '';
  const locked = S.running || S.retrying;
  $('#who-note').hidden = !locked;
  $('#who-new').querySelectorAll('input, button').forEach(el => el.disabled = locked);
  put($('#who-list'), 'who', [S.users, S.currentUser, locked], () => S.users.map(u => {
    const cur = u.id === S.currentUser;
    return `<li><button class="who-item" type="button" data-user="${esc(u.id)}"${cur ? ' aria-current="true"' : ''}${locked && !cur ? ' disabled' : ''}>
      <span class="who-check">${cur ? icon('i-check') : ''}</span>
      <span class="who-item-name">${esc(u.name)}</span>
      ${u.avisos ? `<span class="badge" title="${plural(u.avisos, 'aviso esperando', 'avisos esperando')}">${u.avisos}<span class="sr"> ${u.avisos === 1 ? 'aviso' : 'avisos'}</span></span>` : ''}
    </button></li>`;
  }).join(''));
}
// Place the popover under its button (the top layer ignores normal layout).
who.addEventListener('toggle', e => {
  if (e.newState !== 'open') return;
  const r = $('#who').getBoundingClientRect();
  who.style.top = `${r.bottom + 6}px`;
  who.style.right = `${Math.max(16, innerWidth - r.right)}px`;
  who.querySelector('[aria-current], .who-item, input')?.focus();
});
$('#who-list').addEventListener('click', e => {
  const b = e.target.closest('[data-user]');
  if (!b) return;
  who.hidePopover();
  $('#who').focus();
  if (b.dataset.user !== S.currentUser) send({ type: 'switchUser', id: b.dataset.user });
});
$('#who-new').addEventListener('submit', e => {
  e.preventDefault();
  const name = $('#who-new-name').value.trim();
  if (!name) return $('#who-new-name').focus();
  $('#who-new-name').value = '';
  who.hidePopover();
  $('#who').focus();
  send({ type: 'createUser', name });
});

/* ── setup: what must be true before the first run ──────── */
const setupReady = () => (S.claudeOk === true || S.noAi) && S.chromeOk === true && S.riskAccepted && S.dataNoticeAccepted;

// What leaves this computer, and to whom. Shown in the setup checklist and on Perfil.
const DATA_NOTICE = `<p>Seus dados ficam só neste computador; o desenvolvedor do app não recebe nada. Para funcionar, o app envia:</p>
  <ul>
    <li><b>à Anthropic (Claude)</b>, pela conta Claude logada neste computador: o seu perfil, as perguntas dos formulários, as descrições das vagas e textos que você importar;</li>
    <li><b>ao LinkedIn e ao Indeed</b>: as respostas das candidaturas.</li>
  </ul>
  <p>Numa conta pessoal da Anthropic, o uso das conversas para treinar modelos depende da configuração de privacidade dessa conta. Usuários do app separam os dados por organização, não por segurança: quem usa este usuário do Windows consegue abrir as pastas de todos. Para separar de verdade, use um usuário do Windows por pessoa.</p>`;

function renderSetup() {
  // Only hard requirements live here; the panel leaves for good once they pass. Profile gaps are a nudge.
  const items = [
    S.noAi
      ? ['applied', 'Sem IA', 'Modo sem IA: o Claude não é usado.', '']
    : S.claudeOk === null
      ? ['ready', 'Verificando', 'Testando o Claude pela bridge…', '']
      : S.claudeOk
        ? ['applied', 'Ok', 'O Claude está respondendo.', '']
        : ['waiting', 'Sem resposta', `<p class="setup-lead">Claude Code: a IA que lê as vagas e responde os formulários</p>
          <p>Precisa de uma conta Claude <b>paga, Pro ou Max</b> (assine em claude.ai). A conta grátis não dá acesso ao Claude Code. O uso sai do limite do seu plano; o app não cobra nada.</p>
          <ol class="setup-steps">
            <li><b>Instalar o Claude Code.</b> Abre uma janela do PowerShell que roda o instalador oficial da Anthropic (<code>irm https://claude.ai/install.ps1 | iex</code>) e, se faltar, o Git.</li>
            <li><b>Entrar na conta.</b> Abre o Claude numa janela; faça login no navegador com a conta Pro ou Max e, na caixa de conversa, digite <code>/exit</code>.</li>
            <li>Ao fechar cada janela, o app testa sozinho. Se já estava tudo instalado e logado e ainda falha, pode ser o limite de uso do plano.</li>
          </ol>
          <p>Sem conta paga? Siga <b>sem IA</b>: as perguntas novas viram avisos para você responder uma vez, e as respostas salvas preenchem o resto.</p>`,
          `<span class="setup-actions">
            <button class="btn primary" type="button" data-setup="installClaude">1. Instalar o Claude Code</button>
            <button class="btn" type="button" data-setup="loginClaude">2. Entrar na conta</button>
            <button class="btn" type="button" data-setup="recheckClaude">Testar de novo</button>
            <button class="btn" type="button" data-setup="noAi">Seguir sem IA</button>
          </span>`],
    S.chromeOk === null
      ? ['ready', 'Verificando', 'Abrindo o Google Chrome em segundo plano para testar…', '']
      : S.chromeOk
        ? ['applied', 'Ok', 'O Google Chrome abre e responde ao app.', '']
        : ['waiting', 'Faltando', 'O app não conseguiu abrir o Google Chrome, e é nele que as candidaturas são feitas. <b>Instalar o Chrome</b> abre uma janela do PowerShell que instala pelo winget (<code>winget install Google.Chrome</code>).',
          `<span class="setup-actions">
            <button class="btn primary" type="button" data-setup="installChrome">Instalar o Chrome</button>
            <button class="btn" type="button" data-setup="recheckChrome">Testar de novo</button>
          </span>`],
    S.dataNoticeAccepted
      ? ['applied', 'Lido', 'Você sabe para onde seus dados vão.', '']
      : ['waiting', 'Pendente', `<p class="setup-lead">Seus dados: o que fica aqui e o que é enviado</p>${DATA_NOTICE}`, '<button class="btn primary" type="button" data-setup="acceptDataNotice">Li e entendi</button>'],
    S.riskAccepted
      ? ['applied', 'Aceito', 'Risco da automação entendido.', '']
      : ['waiting', 'Pendente', 'Automatizar candidaturas vai contra os termos de uso do LinkedIn e a conta pode ser restringida. O app usa intervalos humanos e para no limite diário, mas o risco é seu.', '<button class="btn primary" type="button" data-setup="acceptRisk">Entendi e aceito</button>'],
  ];
  const ready = setupReady();
  $('#setup').hidden = ready;
  form.hidden = $('#ficha').hidden = $('#desk').hidden = !ready; // nothing to set up a run with, or to look at, until these pass
  let lead = true;
  for (const it of items) if (it[0] === 'waiting') { if (!lead) it[3] = it[3].replaceAll('btn primary', 'btn'); lead = false; }
  put($('#setup-list'), 'setup', items, () => items.map(([k, label, text, action]) =>
    `<li><span class="stamp ${k}">${label}</span><div class="setup-text">${text}</div>${action}</li>`).join(''));

  const blanks = profileGaps(S.profile).length;
  const nudge = $('#nudge');
  nudge.hidden = !blanks;
  put(nudge, 'nudge', blanks, () =>
    `${t('{n} no perfil; cada uma pode virar aviso.', { n: plural(blanks, 'pergunta em branco', 'perguntas em branco') })} <button class="link" type="button" data-go="remetente">Abrir perfil</button>`);
}
$('#setup').addEventListener('click', e => {
  const b = e.target.closest('[data-setup]');
  if (b) send(b.dataset.setup === 'noAi' ? { type: 'setNoAi', on: true } : { type: b.dataset.setup });
});

/* ── avisos ─────────────────────────────────────────────── */
function slip(a) {
  const pending = pendingAnswers.get(a.id);
  const pre = pending?.answer ?? a.suggestion ?? '';
  // Options whose text couldn't be read come back blank: fall back to typing the answer.
  const opts = a.options?.length && a.options.every(Boolean) ? a.options : null;
  const best = opts && pre && (opts.find(o => o.toLowerCase() === pre.toLowerCase()) ?? opts.find(o => o.toLowerCase().includes(pre.toLowerCase())));
  const numeric = a.type === 'number' || /inteir|integer|número|number/i.test(a.error ?? '');
  const control = opts
    ? `<div class="choices" translate="no" role="radiogroup" aria-label="${esc(a.label)}">${opts.map(o =>
        `<label class="choice"><input type="radio" name="c-${a.id}" value="${esc(o)}"${o === best ? ' checked' : ''}><span>${esc(o)}</span></label>`).join('')}</div>`
    : a.type === 'textarea'
      ? `<textarea class="grow" name="v" aria-label="${esc(a.label)}">${esc(pre)}</textarea>`
      : `<input class="grow" name="v" type="${numeric ? 'number' : 'text'}"${numeric ? ' inputmode="numeric"' : ''} value="${esc(pre)}" aria-label="${esc(a.label)}" autocomplete="off">`;
  // Radio/checkbox whose option text LinkedIn hid: the answer must match the form's own label, so say so and offer the usual pairs.
  const blind = !opts && a.options?.length && !pending;
  const quick = blind && a.options.length === 2
    ? `<div class="quick" translate="no">${['Sim', 'Não', 'Yes', 'No'].map(o => `<button class="chip" type="button" data-fill="${o}">${o}</button>`).join('')}</div>` : '';
  const note = pending ? ''
    : (pre ? `<p class="slip-note">Sugestão do Claude: <b>${esc(pre)}</b>. Ele não teve certeza pelo seu perfil.</p>`
      : `<p class="slip-note">O Claude não encontrou isso no seu perfil.</p>`)
      + (blind ? `<p class="slip-note">O LinkedIn não mostrou o texto das ${a.options.length} opções. Digite como aparece no formulário, no idioma da vaga (ex.: Yes ou Sim).</p>${quick}` : '');
  return `<form class="slip${pending ? ' taken' : ''}" data-id="${esc(a.id)}">
    <div class="slip-top">${code(a.jobId)}<span class="slip-job" translate="no">${esc(splitJob(a.job).join(' · '))}</span><span class="slip-when">${ago(a.at)}</span></div>
    <p class="slip-q" translate="no">${esc(a.label)}</p>
    ${note}
    ${a.error ? `<p class="slip-error">O LinkedIn recusou a resposta anterior: ${esc(a.error)}</p>` : ''}
    <div class="slip-form">${control}<button class="btn primary" type="submit">${icon('i-check')}<span>Responder</span></button></div>
    <div class="slip-undo" role="status">${pending ? `<span>Respondido: <b>${esc(pending.answer)}</b></span><button class="btn" type="button" data-undo="${esc(a.id)}">${icon('i-undo')}<span>Desfazer</span></button>` : ''}</div>
    <span class="stamp big taken" aria-hidden="true">Respondido</span>
  </form>`;
}

const EMPTY_AVISOS = `<p class="empty"><b>Nenhum aviso.</b> Quando o Claude não tiver certeza de uma resposta, a pergunta fica aqui esperando você, e a execução segue com as outras vagas.</p>`;

function renderPile() {
  const avisos = S.avisos;
  const n = avisos.length;
  const count = $('#pile-count');
  count.textContent = n;
  count.classList.toggle('zero', n === 0);
  $('#pile-caption').textContent = n === 1 ? 'aviso esperando você' : 'avisos esperando você';
  const badge = $('#badge');
  badge.hidden = n === 0;
  badge.textContent = n;
  const pend = [...pendingAnswers.keys()];

  put($('#pile-list'), 'pile', [avisos, pend], () => n === 0 ? EMPTY_AVISOS
    : avisos.slice(0, 3).map(slip).join('') + (n > 3 ? `<button class="link pile-more" type="button" data-go="avisos">Ver todos os ${n} avisos</button>` : ''));
  const retry = $('#retry');
  retry.hidden = n === 0 || S.noAi;
  retry.disabled = S.retrying;
  retry.lastElementChild.textContent = S.retrying ? 'Claude revisando…' : 'Pedir ao Claude de novo';
  put($('#avisos-list'), 'avisos', [avisos, pend], () => n === 0 ? EMPTY_AVISOS : avisos.map(slip).join(''));
}
$('#retry').addEventListener('click', () => send({ type: 'retryAvisos' }));

document.addEventListener('submit', e => {
  const f = e.target.closest('.slip');
  if (!f) return;
  e.preventDefault();
  const picked = f.querySelector('input[type=radio]:checked');
  const field = f.elements.v;
  const answer = (picked?.value ?? field?.value ?? '').trim();
  if (!answer) { (field ?? f.querySelector('input[type=radio]'))?.focus(); return; }
  const id = f.dataset.id;
  // Held for a few seconds so it can be taken back; then it is sent.
  const timer = setTimeout(() => {
    pendingAnswers.delete(id);
    document.querySelectorAll(`.slip[data-id="${CSS.escape(id)}"]`).forEach(el => el.remove());
    send({ type: 'answer', id, answer });
  }, UNDO_MS);
  pendingAnswers.set(id, { answer, timer });
  const next = f.nextElementSibling?.closest?.('.slip') ?? [...f.parentElement.querySelectorAll('.slip:not(.taken)')].find(s => s !== f);
  document.activeElement?.blur();
  renderPile();
  (next && document.querySelector(`.slip[data-id="${CSS.escape(next.dataset.id)}"]`))?.querySelector('input:not([type=radio]), input[type=radio]:checked, input[type=radio], textarea')?.focus();
});
document.addEventListener('click', e => {
  const b = e.target.closest('[data-fill]');
  const v = b?.closest('.slip')?.elements.v;
  if (v) { v.value = b.dataset.fill; v.focus(); }
});
document.addEventListener('click', e => {
  const b = e.target.closest('[data-undo]');
  if (!b) return;
  const p = pendingAnswers.get(b.dataset.undo);
  if (!p) return;
  clearTimeout(p.timer);
  pendingAnswers.delete(b.dataset.undo);
  renderPile();
  document.querySelector(`.slip[data-id="${CSS.escape(b.dataset.undo)}"] input, .slip[data-id="${CSS.escape(b.dataset.undo)}"] textarea`)?.focus();
});

/* ── counters (built once so the reels can roll) ────────── */
const COUNTERS = [['applied', 'Enviadas'], ['ready', 'Na fila'], ['skip', 'Puladas'], ['failed', 'Falhas']];
$('#franking').innerHTML = COUNTERS.map(([k, label]) => `<div data-k="${k}">
  <span class="counter-label">${label}</span>
  <span class="digits" role="img" aria-label="${label}: 0">${[0, 1, 2, 3].map(i =>
    `<span class="digit"><span class="reel" style="--n:0;--i:${3 - i}">${[...'0123456789'].map(d => `<b>${d}</b>`).join('')}</span></span>`).join('')}</span>
</div>`).join('');

function renderFranking() {
  const counts = { applied: 0, ready: 0, skip: 0, failed: 0 };
  for (const e of S.ledger) { const k = kind(e.status); if (k in counts) counts[k]++; }
  for (const [k, label] of COUNTERS) {
    const box = $(`#franking [data-k="${k}"]`);
    const s = String(Math.min(counts[k], 9999)).padStart(4, '0');
    box.querySelector('.digits').setAttribute('aria-label', `${label}: ${counts[k]}`);
    let lead = true;
    box.querySelectorAll('.digit').forEach((el, i) => {
      if (s[i] !== '0' || i === 3) lead = false;
      el.classList.toggle('lead', lead);
      el.firstElementChild.style.setProperty('--n', s[i]);
    });
  }
}

/* ── in progress + activity ─────────────────────────────── */
function trail(lines, status) {
  return `<ol class="trail" data-end="${status ? kind(status) : ''}">${lines.map(l =>
    `<li><time>${hhmm(l.at)}</time>${esc(l.text.startsWith('Vaga aberta: ') ? 'Vaga aberta' : l.text)}</li>`).join('')}</ol>`;
}

function renderTransit() {
  const withJob = S.log.filter(l => l.jobId);
  const id = S.currentJob ?? withJob.at(-1)?.jobId;
  const lines = withJob.filter(l => l.jobId === id);
  const opened = lines.find(l => l.text.startsWith('Vaga aberta: '));
  const entry = S.ledger.find(e => e.id === id);
  const [title, company] = splitJob(opened?.text.slice(13) ?? entry?.job ?? '');
  $('#transit-title').textContent = S.currentJob || !id ? 'Em andamento' : S.running ? 'Vaga anterior · aguardando a próxima' : 'Última vaga';
  put($('#transit'), 'transit', { id, lines, entry, cur: S.currentJob }, () => !id
    ? `<p class="idle">Nenhuma vaga em andamento. Escolha a origem das vagas acima e clique em Candidatar.</p>`
    : `<div class="transit-head">
        <div>${code(id)} ${!S.currentJob && entry ? stamp(entry.status) : ''}</div>
        <span class="transit-job" translate="no">${esc(title)}${company ? ` · <span class="company">${esc(company)}</span>` : ''}</span>
      </div>
      ${trail(lines, S.currentJob ? '' : entry?.status)}`);
}

const MILESTONE = /^(Vaga aberta|Enviada|Entregue|Teste concluído|Aguardando você|Pulada|Falhou|Claude falhou)/;
function renderTrack() {
  const lines = S.log.filter(l => !l.jobId || MILESTONE.test(l.text)).slice(-150).reverse();
  const titles = {};
  for (const l of S.log) if (l.jobId && l.text.startsWith('Vaga aberta: ')) titles[l.jobId] = splitJob(l.text.slice(13))[0];
  for (const e of S.ledger) titles[e.id] ??= splitJob(e.job)[0];
  const text = l => l.text.startsWith('Vaga aberta: ') ? `Vaga aberta: ${splitJob(l.text.slice(13)).filter(Boolean).join(' · ')}`
    : l.jobId && titles[l.jobId] ? `${l.text} · ${titles[l.jobId]}` : l.text;
  put($('#track'), 'track', [lines, titles], () => lines.length
    ? lines.map(l => `<li class="${l.jobId ? '' : 'sys'}"><time>${hhmmss(l.at)}</time><span>${esc(text(l))}</span></li>`).join('')
    : `<li><span class="idle">A atividade aparece aqui durante a execução.</span></li>`);
}

/* ── histórico ──────────────────────────────────────────── */
const FILTERS = [['all', 'Todas'], ['applied', 'Enviadas'], ['waiting', 'Aguardando você'], ['ready', 'Na fila'], ['skip', 'Puladas'], ['failed', 'Falhas']];
const ledgerSearch = $('#ledger-search');

function renderLedger() {
  const count = k => k === 'all' ? S.ledger.length : S.ledger.filter(e => kind(e.status) === k).length;
  put($('#filters'), 'filters', [S.ledger.map(e => e.status), filter], () => FILTERS.map(([k, label]) =>
    `<button class="filter" type="button" data-filter="${k}" aria-pressed="${k === filter}">${label} <b>${count(k)}</b></button>`).join(''));

  const q = ledgerSearch.value.trim().toLowerCase();
  const rows = S.ledger.filter(e => (filter === 'all' || kind(e.status) === filter) && (!q || e.job.toLowerCase().includes(q)));
  put($('#ledger'), 'ledger', [rows, filter, openRow, q, S.avisos.map(a => a.jobId)], () => rows.length ? rows.map(e => {
    const [title, company] = splitJob(e.job);
    const r = [e.score != null ? `nota ${e.score}` : '', reason(e.status)].filter(Boolean).join(' · ');
    const open = openRow === e.id;
    return `<tr class="obj${open ? ' open' : ''}" data-row="${esc(e.id)}">
      <td><button class="row-toggle" type="button" aria-expanded="${open}" aria-controls="d-${esc(e.id)}" aria-label="${open ? 'Fechar' : 'Ver'} detalhes: ${esc(title)}">${icon('i-down', 'ic chev')}${code(e.id)}</button></td>
      <td class="job" translate="no">${esc(title)}</td>
      <td class="company" translate="no">${esc(company)}</td>
      <td class="num">${ddmm(e.at)}</td>
      <td>${stamp(e.status)}${r ? `<span class="reason">${esc(r)}</span>` : ''}</td>
      <td><button class="icon-btn" type="button" data-open="${esc(e.id)}" aria-label="Abrir vaga no ${isIndeed(e.id) ? 'Indeed' : 'LinkedIn'}" title="Abrir vaga no ${isIndeed(e.id) ? 'Indeed' : 'LinkedIn'}">${icon('i-open')}</button></td>
    </tr>${open ? `<tr class="detail" id="d-${esc(e.id)}"><td colspan="6">${jobDetail(e)}</td></tr>` : ''}`;
  }).join('') : `<tr><td colspan="6"><p class="empty">${q ? 'Nenhuma vaga com esse termo.' : filter === 'all'
      ? '<b>Nenhuma vaga ainda.</b> Cada vaga processada entra aqui com a data e a situação.'
      : 'Nenhuma vaga com essa situação.'}</p></td></tr>`);
}
ledgerSearch.addEventListener('input', renderLedger);
$('#filters').addEventListener('click', e => {
  const b = e.target.closest('[data-filter]');
  if (b) { filter = b.dataset.filter; renderLedger(); }
});
$('#ledger').addEventListener('click', e => {
  const b = e.target.closest('[data-open]');
  if (b) return send({ type: 'openJob', id: b.dataset.open });
  const row = e.target.closest('[data-row]');
  if (!row) return;
  const id = row.dataset.row;
  openRow = openRow === id ? null : id;
  renderLedger();
  $(`[data-row="${CSS.escape(id)}"] .row-toggle`)?.focus();
});

// The receipt says only what really happened: "enviadas em seu nome" is reserved for applications that went out.
function jobDetail(e) {
  const sent = e.sent ?? [];
  const k = kind(e.status);
  const n = S.avisos.filter(a => a.jobId === e.id).length;
  const [title, empty] = {
    applied: ['Respostas enviadas em seu nome', 'Nenhum campo preenchido pelo app: o LinkedIn já tinha tudo.'],
    'dry-run': ['Preenchida em teste (descartada)', 'Nenhum campo preenchido pelo app. Nada foi enviado.'],
    waiting: [n ? `Não enviada: ${plural(n, 'aviso esperando', 'avisos esperando')} você` : 'Não enviada: aguardando você', 'Nada foi enviado.'],
    ready: ['Não enviada: na fila da próxima execução', 'Nada foi enviado ainda.'],
  }[k] ?? ['Nada foi enviado', 'O app não preencheu nenhum campo desta vaga.'];
  const lead = sent.length && k !== 'applied' ? '<p class="idle">O que o app tinha preenchido antes de descartar:</p>' : '';
  return `<div class="obj-detail">
    <div><h2 class="detail-title">O que aconteceu</h2>${e.trail?.length ? trail(e.trail, e.status) : '<p class="idle">Sem registro das etapas desta vaga.</p>'}</div>
    <div><h2 class="detail-title">${title}</h2>${lead}${sent.length
      ? `<dl class="receipt" translate="no">${sent.map(([q, a]) => `<dt>${esc(q)}</dt><dd>${esc(a)}</dd>`).join('')}</dl>`
      : `<p class="idle">${empty}</p>`}
      ${k === 'waiting' && n ? '<button class="btn primary" type="button" data-go="avisos">Responder os avisos</button>' : ''}
    </div>
  </div>`;
}

/* ── respostas salvas: one filter per column ───────────── */
const needsReview = a => a.by !== 'você' && !a.reviewed;
const origin = a => a.by === 'você' ? 'you' : needsReview(a) ? 'review' : 'ok';
const ORIGIN_LABEL = { you: 'Você', review: 'Claude · a conferir', ok: 'Claude · conferida' };
const colF = () => ({ q: $('#f-q').value.trim(), a: $('#f-a').value.trim(), o: $('#f-o').value });
function clearColFilters() { $('#f-q').value = $('#f-a').value = $('#f-o').value = ''; }

function renderAnswers() {
  const toReview = S.answers.filter(needsReview);
  const badge = $('#review-badge');
  badge.hidden = toReview.length === 0;
  badge.innerHTML = `${toReview.length}<span class="sr"> a conferir</span>`;

  const f = colF();
  const has = (text, term) => !term || norm(text).includes(norm(term)); // accent- and case-insensitive
  const rows = S.answers.filter(a => has(a.label, f.q) && has(a.answer, f.a) && (!f.o || origin(a) === f.o));
  const active = !!(f.q || f.a || f.o);
  $('#answer-count').textContent = active
    ? t('{a} de {b}', { a: rows.length, b: plural(S.answers.length, 'resposta', 'respostas') })
    : plural(S.answers.length, 'resposta', 'respostas');
  $('#answer-clear').hidden = !active;
  $('#review-all').hidden = f.o !== 'review' || rows.length < 2;
  $('#answers-intro').textContent = f.o === 'review'
    ? 'Respostas que o Claude deu sozinho, sem te interromper. Se estiver certa, aprove; se não, corrija e ela vale para as próximas vagas.'
    : 'Toda resposta daqui é reusada sem perguntar de novo quando outra vaga fizer a mesma pergunta.';

  const del = [...pendingDeletes.keys()];
  put($('#answers'), 'answers', [rows, f, del], () => rows.length ? rows.map(a => {
    if (pendingDeletes.has(a.key)) return `<tr class="removed"><td colspan="4"><span>Excluída: ${esc(a.label)}</span>
      <button class="btn" type="button" data-undel="${esc(a.key)}">${icon('i-undo')}<span>Desfazer</span></button></td></tr>`;
    const o = origin(a);
    return `<tr>
      <td><span translate="no">${esc(a.label)}</span>${a.job ? `<span class="reason">usada em ${esc(splitJob(a.job).join(' · '))}</span>` : ''}</td>
      <td><input type="text" value="${esc(a.answer)}" data-key="${esc(a.key)}" aria-label="Resposta para: ${esc(a.label)}"></td>
      <td><span class="by ${o}">${ORIGIN_LABEL[o]}</span></td>
      <td class="actions">${o === 'review' ? `<button class="btn" type="button" data-ok="${esc(a.key)}">${icon('i-check')}<span>Está certa</span></button>` : ''}
        <button class="icon-btn" type="button" data-del="${esc(a.key)}" aria-label="Excluir resposta: ${esc(a.label)}" title="Excluir resposta">${icon('i-trash')}</button></td>
    </tr>`;
  }).join('')
    : `<tr><td colspan="4"><p class="empty">${active
      ? 'Nenhuma resposta com esses filtros. <button class="link" type="button" data-clear>Limpar filtros</button>'
      : '<b>Nenhuma resposta salva ainda.</b> As respostas do Claude e as suas entram aqui e são reusadas sem perguntar de novo.'}</p></td></tr>`);
}
['#f-q', '#f-a'].forEach(sel => $(sel).addEventListener('input', renderAnswers));
$('#f-o').addEventListener('change', renderAnswers);
$('#answer-clear').addEventListener('click', () => { clearColFilters(); renderAnswers(); $('#f-q').focus(); });
$('#review-all').addEventListener('click', () => send({ type: 'reviewAll' }));
$('#answers').addEventListener('change', e => {
  const i = e.target.closest('[data-key]');
  if (!i) return;
  const orig = S.answers.find(a => a.key === i.dataset.key)?.answer ?? '';
  if (!i.value.trim()) { i.value = orig; return; }
  if (i.value.trim() !== orig) { send({ type: 'editAnswer', key: i.dataset.key, answer: i.value.trim() }); announce('Resposta salva'); }
});
$('#answers').addEventListener('keydown', e => { if (e.key === 'Enter' && e.target.matches('[data-key]')) e.target.blur(); });
$('#answers').addEventListener('click', e => {
  if (e.target.closest('[data-clear]')) { clearColFilters(); renderAnswers(); $('#f-q').focus(); return; }
  const ok = e.target.closest('[data-ok]');
  if (ok) { send({ type: 'reviewAnswer', key: ok.dataset.ok }); announce('Resposta aprovada'); return; }
  const del = e.target.closest('[data-del]');
  if (del) {
    const key = del.dataset.del;
    pendingDeletes.set(key, setTimeout(() => { pendingDeletes.delete(key); send({ type: 'deleteAnswer', key }); }, UNDO_MS));
    renderAnswers();
    $(`[data-undel="${CSS.escape(key)}"]`)?.focus();
    return;
  }
  const undel = e.target.closest('[data-undel]');
  if (undel) { clearTimeout(pendingDeletes.get(undel.dataset.undel)); pendingDeletes.delete(undel.dataset.undel); renderAnswers(); }
});

/* ── perfil: the questionnaire from profile.js ──────────── */
const pform = $('#profile-form');
const FIELDS = PROFILE_TEMPLATE.flatMap(s => s.fields);

function techRow(t = '', y = '') {
  return `<div class="tech-row">
    <input type="text" value="${esc(t)}" placeholder="Competência ou ferramenta" aria-label="Competência ou ferramenta">
    <input type="number" min="0" max="50" value="${esc(y)}" placeholder="Anos" aria-label="Anos">
    <button class="icon-btn" type="button" data-rmrow aria-label="Remover competência" title="Remover competência">${icon('i-trash')}</button>
  </div>`;
}

pform.innerHTML = PROFILE_TEMPLATE.map(s => `<fieldset class="sheet-section">
  <legend>${esc(s.title)}</legend>
  ${s.fields.map(f => {
    const id = `p-${f.id}`;
    const hint = f.hint ? `<small class="q-hint">${esc(f.hint)}</small>` : '';
    const wide = f.type === 'textarea' || f.type === 'table' ? ' wide' : '';
    const control =
      f.type === 'select' ? `<select id="${id}" name="${f.id}"><option value="">Não informado</option>${f.options.map(o => `<option value="${esc(o)}">${esc(o)}</option>`).join('')}</select>`
      : f.type === 'textarea' ? `<textarea id="${id}" name="${f.id}" rows="3"></textarea>`
      : f.type === 'table' ? `<div class="tech" id="${id}" data-table="${f.id}"></div><button class="btn" type="button" data-addrow="${f.id}">Adicionar competência</button>`
      : `<input id="${id}" name="${f.id}" type="${f.type === 'number' ? 'number' : 'text'}" autocomplete="off">`;
    return `<div class="q${wide}" data-q="${f.id}"><label class="q-label" for="${id}">${esc(f.label)}<span class="q-blank" hidden>em branco</span></label>${control}${hint}</div>`;
  }).join('')}
</fieldset>`).join('');

function readProfile() {
  const v = {};
  for (const f of FIELDS) {
    if (f.type === 'table') {
      v[f.id] = [...pform.querySelectorAll(`[data-table="${f.id}"] .tech-row`)]
        .map(r => [...r.querySelectorAll('input')].map(i => i.value.trim()))
        .filter(([t]) => t).map(([t, y]) => y ? `${t} = ${y}` : t).join('\n');
    } else v[f.id] = pform.elements[f.id].value.trim();
  }
  return v;
}

function fillProfile(values) {
  for (const f of FIELDS) {
    const val = values?.[f.id] ?? '';
    if (f.type === 'table') {
      const rows = tableRows(val);
      pform.querySelector(`[data-table="${f.id}"]`).innerHTML = (rows.length ? rows : [['', '']]).map(([t, y]) => techRow(t, y)).join('');
    } else pform.elements[f.id].value = val;
  }
}

function renderGauge() {
  const v = readProfile();
  const open = profileGaps(v);
  const pct = Math.round((FIELDS.length - open.length) / FIELDS.length * 100);
  $('#gauge').innerHTML = `<div class="gauge-num ${open.length ? 'open' : 'done'}">${open.length || 'Completo'}</div>
    <div class="gauge-bar"><i style="--p:${pct / 100}"></i></div>
    <p>${open.length ? `${open.length === 1 ? 'pergunta em branco' : 'perguntas em branco'}. Cada uma vira aviso quando uma vaga perguntar.` : 'Todas as perguntas respondidas.'}</p>`;
  $('#todo').innerHTML = open.map(f => `<li><button type="button" class="todo-link" data-focus="${f.id}">${esc(f.label)}</button></li>`).join('');
  pform.querySelectorAll('.q').forEach(q => {
    const blank = open.some(f => f.id === q.dataset.q);
    q.classList.toggle('blank', blank);
    q.querySelector('.q-blank').hidden = !blank;
  });
}

let profileSeen = null;
function renderSender() {
  const json = JSON.stringify(S.profile ?? {});
  if (profileDirty || json === profileSeen) return;
  profileSeen = json;
  // The questionnaire's wording changed since this profile was saved: rewrite what Claude reads, same answers.
  if (S.profile && S.profileMd != null && renderProfile(S.profile).trim() !== S.profileMd.trim())
    send({ type: 'saveProfile', values: S.profile, markdown: renderProfile(S.profile) });
  fillProfile(S.profile ?? {});
  renderGauge();
}

function dirty() {
  profileDirty = true;
  $('#save-profile').disabled = false;
  $('#save-note').textContent = 'Alterações não salvas';
  renderGauge();
}
pform.addEventListener('input', dirty);
pform.addEventListener('click', e => {
  const add = e.target.closest('[data-addrow]');
  if (add) {
    const box = pform.querySelector(`[data-table="${add.dataset.addrow}"]`);
    box.insertAdjacentHTML('beforeend', techRow());
    box.lastElementChild.querySelector('input').focus();
    return dirty();
  }
  const rm = e.target.closest('[data-rmrow]');
  if (rm) { rm.closest('.tech-row').remove(); dirty(); }
});
$('#todo').addEventListener('click', e => {
  const b = e.target.closest('[data-focus]');
  if (!b) return;
  const q = pform.querySelector(`[data-q="${b.dataset.focus}"]`);
  q.scrollIntoView({ block: 'center' });
  q.querySelector('input, select, textarea')?.focus({ preventScroll: true });
});
$('#save-profile').addEventListener('click', () => {
  const values = readProfile();
  send({ type: 'saveProfile', values, markdown: renderProfile(values) });
  profileDirty = false;
  profileSeen = JSON.stringify(values);
  $('#save-profile').disabled = true;
  $('#save-note').innerHTML = `Salvo às ${hhmm(Date.now())}. <button class="link" type="button" data-go="expedir">Ir para Candidatar</button>`;
});

/* ── privacy + erase (typed confirmation, no timer) ─────── */
$('#privacy-body').innerHTML = DATA_NOTICE;
const myName = () => S.users.find(u => u.id === S.currentUser)?.name ?? '';
function closeErase() {
  $('#erase-panel').hidden = true;
  $('#erase').setAttribute('aria-expanded', 'false');
  $('#erase-name').value = '';
  $('#erase-go').disabled = true;
}
$('#erase').addEventListener('click', () => {
  if (S.running || S.retrying) return;
  if (!$('#erase-panel').hidden) return closeErase();
  const name = myName();
  $('#erase-what').innerHTML = t('Apaga deste computador tudo de {name}: {list}, o perfil, a atividade e o login do LinkedIn no app. Não dá para desfazer.', {
    name: `<b translate="no">${esc(name)}</b>`,
    list: [plural(S.ledger.length, 'vaga', 'vagas'), plural(S.answers.length, 'resposta salva', 'respostas salvas'), plural(S.avisos.length, 'aviso', 'avisos')].join(', ') });
  $('#erase-label').textContent = `Digite ${name} para confirmar`;
  $('#erase-panel').hidden = false;
  $('#erase').setAttribute('aria-expanded', 'true');
  $('#erase-name').focus();
});
$('#erase-name').addEventListener('input', () => {
  $('#erase-go').disabled = $('#erase-name').value.trim().toLowerCase() !== myName().toLowerCase();
});
$('#erase-cancel').addEventListener('click', () => { closeErase(); $('#erase').focus(); });
$('#erase-go').addEventListener('click', () => {
  if ($('#erase-name').value.trim().toLowerCase() !== myName().toLowerCase()) return;
  closeErase();
  send({ type: 'deleteUser', id: S.currentUser });
  announce('Usuário apagado');
});

/* ── currículos: one file per language, picked through the host's file dialog ── */
const RESUME_LANGS = [['pt', 'Português'], ['en', 'Inglês']];
function renderResumes() {
  put($('#resumes'), 'resumes', [S.resumes, S.running], () => RESUME_LANGS.map(([k, label]) => {
    const f = S.resumes?.[k];
    return `<li>
      <span class="field-label">${label}</span>
      <span class="resume-file">${f ? esc(f) : '<span class="idle">Nenhum arquivo</span>'}</span>
      <span class="resume-actions">
        <button class="btn" type="button" data-resume="${k}"${S.running ? ' disabled' : ''}>${f ? 'Trocar arquivo…' : 'Escolher arquivo…'}</button>
        ${f ? `<button class="icon-btn" type="button" data-unresume="${k}" aria-label="Remover currículo em ${label.toLowerCase()}" title="Remover"${S.running ? ' disabled' : ''}>${icon('i-trash')}</button>` : ''}
      </span>
    </li>`;
  }).join(''));
}
$('#resumes').addEventListener('click', e => {
  const pick = e.target.closest('[data-resume]'), drop = e.target.closest('[data-unresume]');
  if (pick) send({ type: 'pickResume', lang: pick.dataset.resume });
  if (drop) send({ type: 'removeResume', lang: drop.dataset.unresume });
});

/* ── export / import a user as .zip (the native file dialogs live in the host) ── */
$('#export-user').addEventListener('click', () => send({ type: 'exportUser' }));
$('#import-user').addEventListener('click', () => send({ type: 'importUser' }));

/* ── import profile from text ────────────────────────────── */
const importFields = () => FIELDS.map(({ id, label, type, options }) => ({ id, label, type, options }));
const importBusy = on => { $('#import-go').disabled = $('#import-url-go').disabled = on; };
let importNote = '#import-note'; // the result shows next to whichever button started the import
$('#import-go').addEventListener('click', () => {
  const text = $('#import-text').value.trim();
  if (!text) return $('#import-text').focus();
  importBusy(true);
  importNote = '#import-note';
  $('#import-note').textContent = 'O Claude está lendo o texto…';
  send({ type: 'importProfile', text, fields: importFields() });
});

/* ── import profile from a LinkedIn link (read in the app's Chrome, then the same Claude import) ── */
const PROFILE_URL = /^(?:https?:\/\/)?(?:[a-z]{2,3}\.)?linkedin\.com\/in\/[A-Za-z0-9\-_%]{3,100}\/?(?:[?#].*)?$/i; // same rule as Runner.ProfileUrl
$('#import-link').addEventListener('submit', e => {
  e.preventDefault();
  const input = $('#import-url');
  const url = input.value.trim();
  if (!PROFILE_URL.test(url)) {
    input.setCustomValidity(t('Use o link de um perfil, por exemplo linkedin.com/in/seu-nome'));
    input.reportValidity();
    return;
  }
  importNote = '#import-url-note';
  if (S?.running) { $(importNote).textContent = 'Pare a execução antes: o Chrome do app está em uso.'; return; }
  importBusy(true);
  $(importNote).textContent = 'Lendo o perfil no Chrome… Se pedir login, entre na janela do Chrome. Depois o Claude preenche (pode levar 1 a 2 minutos).';
  send({ type: 'importLinkedIn', url, fields: importFields() });
});
$('#import-url').addEventListener('input', e => e.target.setCustomValidity(''));// Opening the import suggests the link already in the profile, if it is a LinkedIn one.
$('#import').addEventListener('toggle', () => {
  const input = $('#import-url');
  if (!$('#import').open || input.value) return;
  const link = (readProfile().links ?? '').split(/[\s,;]+/).find(l => PROFILE_URL.test(l));
  if (link) input.value = link;
});

// Import rules: blank questions get filled; list questions (tech table, list textareas) get only the
// entries they lack; answered single-value questions are never touched.
const norm = t => String(t).toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-z0-9#+]+/g, ' ').trim();

function mergeImport(f, mine, theirs) {
  if (!mine) return theirs;
  if (f.type === 'table') {
    const have = new Set(tableRows(mine).map(([t]) => norm(t)));
    const extra = tableRows(theirs).filter(([t]) => t && !have.has(norm(t)));
    return extra.length ? mine + '\n' + extra.map(([t, y]) => y ? `${t} = ${y}` : t).join('\n') : mine;
  }
  if (f.list) {
    const have = new Set(mine.split('\n').map(norm));
    const extra = theirs.split('\n').map(l => l.trim()).filter(l => l && !have.has(norm(l)));
    return extra.length ? mine + '\n' + extra.join('\n') : mine;
  }
  return mine;
}

function onImport(r) {
  importBusy(false);
  if (r.error) { $(importNote).textContent = r.error; return; }
  const current = readProfile();
  let filled = 0, grown = 0;
  for (const f of FIELDS) {
    const v = String(r.values?.[f.id] ?? '').trim();
    if (!v) continue;
    const merged = mergeImport(f, current[f.id], v);
    if (merged === current[f.id]) continue;
    current[f.id] ? grown++ : filled++;
    current[f.id] = merged;
  }
  fillProfile(current);
  if (filled + grown) dirty();
  const parts = [];
  if (filled) parts.push(plural(filled, 'pergunta preenchida', 'perguntas preenchidas'));
  if (grown) parts.push(plural(grown, 'lista ganhou itens novos', 'listas ganharam itens novos'));
  $(importNote).textContent = parts.length
    ? t('{x}. Revise e clique em Salvar perfil.', { x: parts.join(', ') })
    : 'Nada novo: o texto não traz nada que ainda falte no perfil.';
}

/* ── perguntar: pasted questions answered from the profile + saved answers ── */
let asked = [];          // [{ question, answer, confident }] from the last ask, answers as edited here
const answerKey = t => String(t).toLowerCase().replace(/[^\p{L}\p{N}]+/gu, ' ').trim(); // same as Answers.Key
const askForm = $('#ask-form');

function askFoot(a) {
  const saved = S.answers.find(x => x.key === answerKey(a.question));
  const v = a.answer.trim();
  const note = !v ? '<span class="ask-flag">Não está no seu perfil nem nas respostas salvas. Escreva a resposta.</span>'
    : !a.confident ? '<span class="ask-flag">Sem certeza pelo seu perfil. Confira antes de usar.</span>' : '';
  const keep = saved?.answer === v ? `<span class="ask-saved">${icon('i-check')}Nas respostas salvas</span>`
    : `<button class="btn" type="button" data-keep${v ? '' : ' disabled'}${saved ? ` title="Resposta salva hoje: ${esc(saved.answer)}"` : ''}>${saved ? 'Trocar a resposta salva' : 'Salvar nas respostas'}</button>`;
  return `${note}<span class="ask-buttons"><button class="btn" type="button" data-copy${v ? '' : ' disabled'}>Copiar</button>${keep}</span>`;
}

function renderAsk() {
  $('#ask-intro').textContent = S.noAi
    ? 'Modo sem IA: cada linha é uma pergunta, procurada nas respostas salvas. O que não estiver lá, você responde e salva para as próximas vagas.'
    : 'Cole perguntas de um formulário fora do Easy Apply ou da mensagem de um recrutador. O Claude responde pelo seu perfil e pelas respostas salvas; confira, copie e salve o que quiser reusar.';
  const go = $('#ask-go');
  go.disabled = !!S.asking;
  go.innerHTML = S.noAi ? `${icon('i-search')}<span>Procurar nas respostas salvas</span>`
    : `${icon('i-send')}<span>${S.asking ? 'Claude respondendo…' : 'Responder com o Claude'}</span>`;
  if (S.asking) $('#ask-note').textContent = 'O Claude está lendo as perguntas (pode levar até 1 minuto).';
  $('#ask-copy-all').hidden = !asked.some(a => a.answer.trim());
  put($('#ask-list'), 'ask', [asked, S.answers], () => asked.length ? asked.map((a, i) => `<li class="ask-item${a.confident && a.answer.trim() ? '' : ' unsure'}" data-i="${i}">
      <label class="ask-q" translate="no" for="ask-a-${i}">${esc(a.question)}</label>
      <textarea id="ask-a-${i}" class="ask-a" rows="${Math.min(8, Math.max(1, Math.ceil(a.answer.length / 70)))}" placeholder="Sua resposta">${esc(a.answer)}</textarea>
      <div class="ask-foot">${askFoot(a)}</div>
    </li>`).join('')
    : `<li><p class="empty"><b>Nenhuma pergunta ainda.</b> As respostas aparecem aqui, uma por pergunta, para você conferir, copiar e salvar.</p></li>`);
}

window.onAsk = r => {
  $('#ask-note').textContent = r.error ?? '';
  if (r.error) return;
  asked = r.items.map(a => ({ question: a.question, answer: a.answer ?? '', confident: !!a.confident }));
  // Sem IA there is no written reply: a plain numbered list, gaps as [placeholders] like Claude's.
  $('#ask-msg-text').value = r.message?.trim()
    || asked.map((a, i) => `${i + 1}) ${a.question}\n${a.answer.trim() || t('[sua resposta]')}`).join('\n\n');
  $('#ask-msg').hidden = false;
  msgGaps();
  const unsure = asked.filter(a => !a.confident || !a.answer.trim()).length;
  $('#ask-note').textContent = unsure ? t('{n}, {m} para conferir.', { n: plural(asked.length, 'pergunta', 'perguntas'), m: unsure }) : t('{n}, todas respondidas.', { n: plural(asked.length, 'pergunta', 'perguntas') });
  announce($('#ask-note').textContent);
  renderAsk();
  $('#ask-msg-text').focus({ preventScroll: true });
};

askForm.addEventListener('submit', e => {
  e.preventDefault();
  const text = $('#ask-text').value.trim();
  if (!text) return $('#ask-text').focus();
  $('#ask-note').textContent = '';
  send({ type: 'ask', text });
});
$('#ask-text').addEventListener('keydown', e => { if (e.key === 'Enter' && e.ctrlKey) askForm.requestSubmit(); });

async function copyText(text) {
  try { await navigator.clipboard.writeText(text); return true; }
  catch { // the clipboard API can be refused in the host's WebView
    const t = Object.assign(document.createElement('textarea'), { value: text });
    document.body.append(t); t.select();
    const ok = document.execCommand('copy');
    t.remove(); return ok;
  }
}
async function copied(btn, text, label) {
  const ok = await copyText(text);
  btn.textContent = ok ? 'Copiada' : 'Não deu para copiar';
  announce(ok ? t('{label} copiada', { label: t(label) }) : t('Não deu para copiar'));
  setTimeout(() => btn.textContent = label, 1500);
}

$('#ask-list').addEventListener('input', e => {
  const li = e.target.closest('[data-i]');
  if (!li) return;
  const a = asked[li.dataset.i];
  a.answer = e.target.value;
  a.confident = true; // what the user typed is theirs
  li.classList.toggle('unsure', !a.answer.trim());
  li.querySelector('.ask-foot').innerHTML = askFoot(a);
  $('#ask-copy-all').hidden = !asked.some(x => x.answer.trim());
});
$('#ask-list').addEventListener('click', e => {
  const li = e.target.closest('[data-i]');
  if (!li) return;
  const a = asked[li.dataset.i];
  const b = e.target.closest('[data-copy]');
  if (b) return copied(b, a.answer.trim(), 'Copiar');
  if (e.target.closest('[data-keep]')) {
    send({ type: 'saveAnswer', label: a.question, answer: a.answer.trim() });
    announce('Resposta salva');
  }
});
// The reply says what is still missing: every [placeholder] is something only the user knows.
function msgGaps() {
  const n = ($('#ask-msg-text').value.match(/\[[^\]\n]+\]/g) ?? []).length;
  $('#ask-msg-gaps').hidden = !n;
  $('#ask-msg-gaps').textContent = t('{n} entre colchetes para você completar antes de enviar.', { n: plural(n, 'trecho', 'trechos') });
}
$('#ask-msg-text').addEventListener('input', msgGaps);
$('#ask-msg-copy').addEventListener('click', e => copied(e.currentTarget, $('#ask-msg-text').value.trim(), 'Copiar mensagem'));
$('#ask-copy-all').addEventListener('click', e => copied(e.currentTarget,
  asked.filter(a => a.answer.trim()).map(a => `${a.question}\n${a.answer.trim()}`).join('\n\n'), 'Copiar todas'));

document.querySelectorAll('[name=lang]').forEach(r => r.addEventListener('change', () => send({ type: 'setLang', en: r.value === 'en' })));

/* ── boot ───────────────────────────────────────────────── */
if (bridge) {
  window.external.receiveMessage(m => {
    const msg = JSON.parse(m);
    if (msg.reply?.type === 'askResult') { if (msg.user === S?.currentUser) window.onAsk(msg.reply); }
    else if (msg.reply?.type === 'importResult') { if (msg.user === S?.currentUser) onImport(msg.reply); else importBusy(false); }
    else window.onState(msg);
  });
  send({ type: 'init' });
} else {
  $('#demo-flag').hidden = false;
  const s = document.createElement('script');
  s.src = 'demo.js';
  document.body.append(s);
}
