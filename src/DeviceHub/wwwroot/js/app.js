// DeviceHub front end: one screen. Device cards on top (battery, status, laptop sensors), sound devices below
// (press one to make it the default). Settings and Exit are dialogs.

import { setLang, t, locale } from './i18n.js';
import { on, send } from './bridge.js';
import { $, $$, esc, icon, toast } from './ui.js';
import { initNav, focusFirst, focusEl, renderHints } from './nav.js';
import { configure, sfx } from './sound.js';
import { openDialog, closeDialog, dialogOpen } from './dialog.js';

const ORDER = ['mouse', 'headset', 'controller', 'laptop'];
const DEVICE_ICON = { mouse: 'mouse', headset: 'headset', controller: 'gamepad', laptop: 'laptop' };
const SOUND_ICON = { headset: 'headset', speakers: 'speakers', monitor: 'monitor', controller: 'gamepad', laptop: 'laptop', earbuds: 'earbuds', other: 'sound' };

const app = {
  settings: { language: 'he', sounds: true, startFullscreen: false, switchCalls: true },
  version: '',
  devices: {},      // id → latest reading
  readAt: null,     // when the wireless devices were last read
  reading: false,
  audio: null,
  pending: null,    // the sound device just pressed, until Windows confirms it
  started: false,
};

// ---- Static parts (rebuilt when the language changes) ----

function buildChrome() {
  $('#dev-title').textContent = t('dev.title');
  $('#snd-title').textContent = t('snd.title');
  $('#snd-meta').textContent = t('snd.meta');
  $('#out-title').textContent = t('snd.out');
  $('#in-title').textContent = t('snd.in');
  $('#settings-btn').innerHTML = icon('gear');
  $('#settings-btn').setAttribute('aria-label', t('settings'));
  $('#power-btn').innerHTML = icon('power');
  $('#power-btn').setAttribute('aria-label', t('exit'));
  $('#devices').innerHTML = ORDER.map((id, i) => `<article class="dev-card away" data-nav tabindex="0" data-dev="${id}"${i === 0 ? ' data-default' : ''}></article>`).join('');
  renderDevices();
  renderAudio();
  tickClock();
}

// ---- Device cards ----

function renderDevices() {
  for (const id of ORDER) {
    const card = $(`[data-dev="${id}"]`);
    if (!card) continue;
    const d = app.devices[id] ?? { id, model: '', state: 'missing', pending: true };
    const { html, tone } = id === 'laptop' ? laptopCard(d) : batteryCard(d);
    // Only touch the card when something changed, so the focus ring and transitions stay put.
    if (card.dataset.html !== html) { card.innerHTML = html; card.dataset.html = html; }
    card.className = `dev-card ${tone}`;
  }
  const refresh = $('#refresh-btn');
  refresh.innerHTML = app.reading ? `<span class="spinner"></span>${esc(t('dev.refresh'))}` : `${icon('refresh')}${esc(t('dev.refresh'))}`;
  $('#dev-meta').textContent = app.reading && !app.readAt ? t('dev.reading')
    : app.readAt ? t('dev.updated', { time: app.readAt.toLocaleTimeString(locale(), { hour: '2-digit', minute: '2-digit' }) }) : '';
}

const tag = (cls, text) => `<span class="dc-tag ${cls}">${text}</span>`;
const facts = list => list.length ? `<span class="dc-facts">${list.map(f => `<bdi>${esc(f)}</bdi>`).join('<span class="sep" aria-hidden="true"></span>')}</span>` : '';

function stateOf(d) {
  if (d.pending) return { label: '', note: t('dev.reading') };
  switch (d.state) {
    case 'asleep': return { label: t(`st.asleep.${d.id}`), note: t(`note.asleep.${d.id}`) };
    case 'limited': return { label: t('st.limited'), note: t('note.limited') };
    case 'error': return { label: t('st.error'), note: t('note.error', { msg: d.detail ?? '' }) };
    default: return { label: t('st.missing'), note: t(`note.missing.${d.id}`) };
  }
}

function batteryCard(d) {
  const head = label => `<div class="dc-head">${icon(DEVICE_ICON[d.id])}<span class="dc-kind">${esc(t(`kind.${d.id}`))}</span>${label}</div>`;
  // <bdi>: an English name keeps its own order inside Hebrew ("Arctis 7+", not "+Arctis 7").
  const model = `<span class="dc-model"><bdi>${esc(d.model)}</bdi></span>`;
  if (d.state !== 'ready' || d.battery == null) {
    const s = stateOf(d);
    return {
      tone: 'away',
      html: `${head(s.label ? tag('', esc(s.label)) : '')}<div class="dc-big"><span class="dc-dash">--</span></div><div class="dc-bar"></div>
        <div class="dc-foot">${model}<span class="dc-note">${esc(s.note)}</span></div>`,
    };
  }
  const b = d.battery;
  const status = d.full ? tag('full', esc(t('st.full'))) : d.charging ? tag('charge', `${icon('bolt')}${esc(t('st.charging'))}`) : '';
  const list = [];
  if (d.pollingHz) list.push(`${d.pollingHz} Hz`);
  if (d.dpi) list.push(d.dpiY && d.dpiY !== d.dpi ? `${d.dpi}×${d.dpiY} DPI` : `${d.dpi} DPI`);
  if (d.chatGame != null && d.chatChat != null) list.push(t('fact.game', { n: d.chatGame }), t('fact.chat', { n: d.chatChat }));
  if (d.link && d.id === 'controller') list.push(t(`link.${d.link}`));
  return {
    tone: d.charging && !d.full ? 'charging' : b < 15 ? 'critical' : b <= 40 ? 'low' : '',
    html: `${head(status)}
      <div class="dc-big"><span class="dc-num">${b}</span><span class="dc-unit">%</span></div>
      <div class="dc-bar"><i style="--v:${b}%"></i></div>
      <div class="dc-foot"${d.chatGame != null ? ` title="${esc(t('fact.chatmix'))}"` : ''}>${model}${facts(list)}</div>`,
  };
}

function laptopCard(d) {
  const head = label => `<div class="dc-head">${icon('laptop')}<span class="dc-kind">${esc(t('kind.laptop'))}</span>${label}</div>`;
  if (d.state !== 'ready' || d.temperature == null) {
    const s = stateOf(d);
    return {
      tone: 'away',
      html: `${head(s.label ? tag('', esc(s.label)) : '')}<div class="dc-big"><span class="dc-dash">--</span></div><div class="dc-bar"></div>
        <div class="dc-foot"><span class="dc-model"><bdi>${esc(d.model)}</bdi></span><span class="dc-note">${esc(s.note)}</span></div>`,
    };
  }
  const temp = d.temperature, fan = d.fanDuty ?? 0;
  return {
    tone: temp >= 90 ? 'very-hot' : temp >= 80 ? 'hot' : '',
    html: `${head('')}
      <div class="dc-big"><span class="dc-num">${temp}</span><span class="dc-unit">°C</span></div>
      <div class="dc-bar"><i style="--v:${Math.min(100, temp)}%"></i></div>
      <div class="dc-meter"><span>${esc(t('lap.fan'))}</span><div class="dc-bar thin"><i style="--v:${fan}%"></i></div><b>${fan}%</b></div>
      <div class="dc-foot" title="${esc(t('lap.noteLong'))}"><span class="dc-model"><bdi>${esc(d.model)}</bdi></span><span class="dc-note">${esc(t('lap.note'))}</span></div>`,
  };
}

// ---- Sound devices ----

function renderAudio() {
  if (!app.audio) return;
  const focusedId = document.activeElement?.dataset?.audio;
  for (const [key, list] of [['outputs', app.audio.outputs], ['inputs', app.audio.inputs]]) {
    const row = $(`#${key}`);
    const html = list.length ? list.map(soundTile).join('') : `<p class="snd-empty">${esc(t('snd.none'))}</p>`;
    if (row.dataset.html === html) continue;
    row.innerHTML = html;
    row.dataset.html = html;
  }
  // A redraw keeps the focus on the same device (Windows can reorder or drop endpoints at any time).
  if (focusedId && !document.activeElement?.isConnected) {
    const again = $(`[data-audio="${CSS.escape(focusedId)}"]`);
    if (again && !again.disabled) again.focus({ preventScroll: true });
    else if (!dialogOpen()) focusFirst();
  }
}

function soundTile(e) {
  const ic = e.flow === 'input' && (e.kind === 'laptop' || e.kind === 'other') ? 'mic' : SOUND_ICON[e.kind] ?? 'sound';
  const badges = !e.active ? `<span class="badge">${esc(t('snd.away'))}</span>`
    : `${e.default ? `<span class="badge now">${esc(t('snd.now'))}</span>` : ''}${e.calls && !e.default ? `<span class="badge">${esc(t('snd.calls'))}</span>` : ''}`;
  const busy = app.pending?.id === e.id ? ' busy' : '';
  return `<button class="snd${e.default ? ' on' : ''}${busy}" data-nav data-audio="${esc(e.id)}"${e.active ? '' : ' disabled'} aria-pressed="${!!e.default}" title="${esc(e.windows ?? '')}">
    ${icon(ic)}<span class="snd-text"><b><bdi>${esc(e.name)}</bdi></b>${e.sub ? `<small><bdi>${esc(e.sub)}</bdi></small>` : ''}</span>
    <span class="snd-badges">${badges}</span></button>`;
}

function pressSound(id) {
  const all = [...(app.audio?.outputs ?? []), ...(app.audio?.inputs ?? [])];
  const e = all.find(x => x.id === id);
  if (!e || !e.active) return;
  app.pending = { id, flow: e.flow, name: e.name };
  sfx.on();
  send('setAudio', { id });
  renderAudio();
}

// ---- Settings and Exit ----

function settingsBody() {
  const s = app.settings;
  const sw = on => `<span class="switch${on ? ' on' : ''}"></span>`;
  const row = (key, label, sub, value) => `<button class="row" data-nav data-act="set:${key}"><span><b>${esc(label)}</b>${sub ? `<small>${esc(sub)}</small>` : ''}</span>${value}</button>`;
  return `<div class="rows">
    ${row('language', t('set.language'), '', `<span class="choice"><span class="${s.language === 'he' ? 'on' : ''}">עברית</span><span class="${s.language === 'en' ? 'on' : ''}">English</span></span>`)}
    ${row('switchCalls', t('set.calls'), t('set.calls.sub'), sw(s.switchCalls))}
    ${row('sounds', t('set.sounds'), t('set.sounds.sub'), sw(s.sounds))}
    ${row('startFullscreen', t('set.fullscreen'), t('set.fullscreen.sub'), sw(s.startFullscreen))}
  </div><p class="about">${esc(t('set.about', { v: app.version }))}</p>`;
}

function openSettings() {
  if (dialogOpen()) return;
  openDialog({
    title: t('set.title'),
    body: settingsBody(),
    actions: [{ id: 'close', label: t('set.close') }],
    onAction: (id, button) => {
      if (!id.startsWith('set:')) return true;
      const key = id.slice(4);
      const next = { ...app.settings, [key]: key === 'language' ? (app.settings.language === 'he' ? 'en' : 'he') : !app.settings[key] };
      saveSettings(next);
      if (key === 'language') {
        // Everything is in the other language now, the dialog too: draw it again with the focus on the same row.
        closeDialog('lang');
        openSettings();
        focusEl($('[data-act="set:language"]'));
      } else {
        button.querySelector('.switch')?.classList.toggle('on', !!next[key]);
        sfx[next[key] ? 'on' : 'off']();
      }
      return false;
    },
  });
}

async function powerMenu() {
  const choice = await openDialog({
    title: t('power.title'),
    actions: [
      { id: 'cancel', label: t('power.cancel'), icon: 'cross' },
      { id: 'minimize', label: t('power.minimize'), icon: 'minimize' },
      { id: 'exit', label: t('power.exit'), icon: 'power', danger: true },
    ],
    focus: 'cancel',
  });
  if (choice === 'minimize') send('minimize');
  if (choice === 'exit') send('exit');
}

function saveSettings(next) {
  const langChanged = next.language !== app.settings.language;
  app.settings = next;
  configure(next);
  send('saveSettings', { settings: next });
  if (langChanged) { setLang(next.language); buildChrome(); renderHints(); }
}

// ---- Clock ----

let clockTimer = 0;
function tickClock() {
  $('#clock').textContent = new Date().toLocaleTimeString(locale(), { hour: '2-digit', minute: '2-digit' });
  clearTimeout(clockTimer);
  clockTimer = setTimeout(tickClock, 60_000 - (Date.now() % 60_000) + 50); // wake once a minute, on the minute
}

// ---- Input ----

document.addEventListener('click', e => {
  const snd = e.target.closest('[data-audio]');
  if (snd) { pressSound(snd.dataset.audio); return; }
  if (e.target.closest('[data-dev]') || e.target.closest('#refresh-btn')) { send('refresh'); return; }
  if (e.target.closest('#settings-btn')) { sfx.open(); openSettings(); return; }
  if (e.target.closest('#power-btn')) powerMenu();
});
document.addEventListener('keydown', e => {
  if (e.key === 'F11') { e.preventDefault(); send('toggleFullscreen'); }
});
// The hints follow the focus: a sound device says "switch here", a card says "read again".
document.addEventListener('focusin', () => renderHints());
document.addEventListener('visibilitychange', () => document.body.classList.toggle('paused', document.hidden));

function hints() {
  if (dialogOpen()) return [['select', t('hint.select')], ['back', t('hint.cancel')]];
  const el = document.activeElement;
  const select = el?.matches?.('[data-audio]') ? t('hint.switch') : el?.matches?.('[data-dev], #refresh-btn') ? t('hint.refresh') : t('hint.select');
  return [['select', select], ['options', t('hint.settings')], ['full', t('hint.full')]];
}

// ---- Messages from the host ----

on('hello', h => {
  app.version = h.version;
  app.settings = { ...app.settings, ...h.settings };
  configure(app.settings);
  setLang(app.settings.language);
  buildChrome();
  if (!app.started) {
    app.started = true;
    initNav({ back: () => { if (dialogOpen()) closeDialog(null); }, options: openSettings, hints });
    focusFirst();
  }
});
on('settings', s => { app.settings = { ...app.settings, ...s }; configure(app.settings); });
on('devicesBusy', () => { app.reading = true; renderDevices(); });
on('devices', list => {
  app.reading = false;
  app.readAt = new Date();
  for (const d of list ?? []) app.devices[d.id] = d;
  renderDevices();
});
on('laptop', d => { app.devices.laptop = d; renderDevices(); });
on('audio', state => {
  app.audio = state;
  const p = app.pending;
  if (p) {
    const list = p.flow === 'input' ? state.inputs : state.outputs;
    if (list.find(x => x.id === p.id)?.default) {
      app.pending = null;
      toast(t(p.flow === 'input' ? 'snd.switchedIn' : 'snd.switched', { name: p.name }));
    }
  }
  renderAudio();
});
on('error', e => {
  if (e.cmd === 'setAudio') { app.pending = null; renderAudio(); }
  if (e.cmd === 'devices') { app.reading = false; renderDevices(); }
  sfx.error();
  toast(t('error', { msg: e.message }));
});
on('preview', () => toast(t('preview')));

send('hello');
