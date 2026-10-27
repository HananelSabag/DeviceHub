// What each page draws, from the app state. Pure functions: state in, HTML out.

import { t, locale } from './i18n.js';
import { esc, icon, hex } from './ui.js';
import { art } from './art.js';
import { keyboardPreview, effectSwatch } from './effects.js';

export const DEVICES = ['mouse', 'headset', 'keyboard', 'controller', 'laptop'];
export const BRAND = { overview: 'hub', mouse: 'razer', headset: 'steel', keyboard: 'steel', controller: 'ps', laptop: 'heat', sound: 'sound', settings: 'hub' };
const ICON = { mouse: 'mouse', headset: 'headset', keyboard: 'keyboard', controller: 'gamepad', laptop: 'laptop' };
const MODEL = { mouse: 'Razer Basilisk X HyperSpeed', headset: 'SteelSeries Arctis 7+', keyboard: 'SteelSeries Apex 5', controller: 'DualSense Edge', laptop: '' };
const MAKER = { mouse: 'Razer', headset: 'SteelSeries', keyboard: 'SteelSeries', controller: 'Sony' };
const SOUND_ICON = { headset: 'headset', speakers: 'speakers', monitor: 'monitor', controller: 'gamepad', laptop: 'laptop', earbuds: 'earbuds', other: 'sound' };

export const SWATCHES = ['#FFFFFF', '#FF6A1A', '#FF2A2A', '#FF2D95', '#8B3DFF', '#1E5BFF', '#00C8FF', '#2BEA5A', '#FFD000'];
export const BANDS = ['31', '62', '125', '250', '500', '1k', '2k', '4k', '8k', '16k'];

const bdi = s => `<bdi>${esc(s)}</bdi>`;

/** The model without its maker, which the page already says ("Razer Basilisk X HyperSpeed" → "Basilisk X HyperSpeed"). */
export function shortName(id, d) {
  const model = d?.model || MODEL[id] || t(`kind.${id}`);
  const maker = MAKER[id];
  return maker && model.startsWith(maker + ' ') ? model.slice(maker.length + 1) : model;
}

/** One device's state, ready to draw: level class, meter value, the big number and a line of facts. */
export function summary(id, d) {
  if (!d) return { cls: 'away', pct: null, state: t('st.reading'), facts: [] };
  if (id === 'laptop') {
    if (d.state !== 'ready' || d.temperature == null) return { cls: 'away', pct: null, state: stateLabel(id, d), note: stateNote(id, d), facts: [] };
    const temp = d.temperature;
    return { cls: temp >= 90 ? 'crit' : temp >= 80 ? 'low' : '', pct: Math.min(100, temp), num: temp, unit: '°C', facts: [t('fact.fan', { n: d.fanDuty ?? 0 })] };
  }
  if (id === 'keyboard') {
    if (d.state !== 'ready') return { cls: 'away', pct: null, state: stateLabel(id, d), note: stateNote(id, d), facts: [] };
    return { cls: '', pct: null, state: t('st.connected'), facts: [t('link.usb')] };
  }
  if (d.state !== 'ready' || d.battery == null) return { cls: 'away', pct: null, state: stateLabel(id, d), note: stateNote(id, d), facts: d.link ? [t(`link.${d.link}`)] : [] };
  const b = d.battery;
  const facts = [];
  if (d.pollingHz) facts.push(`${d.pollingHz} Hz`);
  if (d.dpi) facts.push(`${d.dpi} DPI`);
  if (d.chatGame != null && d.chatChat != null) facts.push(t('fact.chatmix', { g: d.chatGame, c: d.chatChat }));
  if (id === 'controller' && d.link) facts.push(t(`link.${d.link}`));
  return {
    cls: d.charging && !d.full ? '' : b < 15 ? 'crit' : b <= 30 ? 'low' : '',
    pct: b, num: b, unit: '%', facts,
    chip: d.full ? t('st.full') : d.charging ? t('st.charging') : null,
  };
}

function stateLabel(id, d) {
  if (d.state === 'asleep') return t(`st.asleep.${id}`);
  if (d.state === 'limited') return t('st.limited');
  if (d.state === 'error') return t('st.error');
  return t('st.missing');
}

function stateNote(id, d) {
  if (d.state === 'asleep') return t(`note.asleep.${id}`);
  if (d.state === 'limited') return t('note.limited');
  if (d.state === 'error') return t('note.error', { msg: d.detail ?? '' });
  return t(`note.missing.${id}`);
}

// ---- The rail ----

export function railHtml(app) {
  const item = (page, ic, title, small, meter = '', cls = '') =>
    `<button class="nav-item ${cls}" data-page="${page}" data-brand="${BRAND[page]}"${app.page === page ? ' aria-current="page"' : ''}>
      ${icon(ic)}<span class="nav-text"><b>${title}</b>${small ? `<small>${small}</small>` : ''}</span>${meter}</button>`;
  const devices = DEVICES.map(id => {
    const d = app.devices[id];
    const s = summary(id, d);
    let meter = '';
    if (s.num != null && s.unit === '%') meter = `<span class="nav-meter ${s.cls}"><span class="cell"><i style="--v:${s.pct}%"></i></span><span class="num">${s.num}</span></span>`;
    else if (s.num != null) meter = `<span class="nav-meter ${s.cls}"><span class="num">${s.num}°</span></span>`;
    // The kind leads (it never needs cutting); the model, or what is wrong, goes underneath.
    return item(id, ICON[id], esc(t(`kind.${id}`)), s.num == null && s.state ? esc(s.state) : bdi(shortName(id, d)), meter, s.cls);
  }).join('');
  const out = app.audio?.outputs.find(o => o.default);
  return `${item('overview', 'overview', esc(t('nav.overview')), '')}
    <div class="rail-label">${esc(t('nav.devices'))}</div>${devices}
    <div class="rail-label"></div>
    ${item('sound', 'sound', esc(t('nav.sound')), out ? bdi(out.name) : '')}
    <div class="rail-foot">${item('settings', 'gear', esc(t('nav.settings')), '')}</div>`;
}

// ---- Overview ----

export function overviewHtml(app) {
  const time = app.readAt?.toLocaleTimeString(locale(), { hour: '2-digit', minute: '2-digit' });
  // The desk: every device drawn, with the one number that matters under it.
  const desk = DEVICES.map(id => {
    const d = app.devices[id];
    const s = summary(id, d);
    const value = s.num != null
      ? `<span class="desk-num">${s.num}<small>${s.unit}</small></span>${s.pct != null ? `<span class="meter"><i style="--v:${s.pct}%"></i></span>` : ''}`
      : `<span class="desk-state">${esc(s.state ?? '')}</span>`;
    return `<button class="desk-card ${s.cls}" data-page="${id}" data-brand="${BRAND[id]}">
      <span class="desk-art">${art(id)}</span>
      <span class="desk-name"><b>${bdi(shortName(id, d))}</b><small>${esc(t(`kind.${id}`))}</small></span>
      <span class="desk-value">${value}</span>${s.chip ? `<span class="chip desk-chip">${esc(s.chip)}</span>` : ''}</button>`;
  }).join('');
  const screenMode = app.deviceSettings.keyboard?.screen?.mode ?? 'off';
  return `<header class="page-head"><h1>${esc(t('nav.overview'))}</h1>
      <p class="sub">${esc(app.reading && !time ? t('ov.reading') : time ? t('ov.sub', { time }) : '')}</p>
      ${refreshButton(app)}</header>
    <div class="desk">${desk}</div>
    <div class="overview">
      <section class="panel quick" data-brand="sound"><h2>${esc(t('ov.sound'))}</h2>${soundLists(app, true)}</section>
      <section class="panel screen-panel" data-brand="steel">
        <h2>${esc(t('ov.screen'))}</h2>
        <div class="oled"><canvas class="oled-canvas" width="128" height="40"></canvas>${screenMode === 'off' ? `<span class="oled-off">${esc(t('scr.offNote'))}</span>` : ''}</div>
        <button class="btn" data-page="keyboard">${icon('keyboard')}${esc(t('ov.screenEdit'))}</button>
      </section>
    </div>`;
}

const refreshButton = app => `<button class="btn" data-refresh>${app.reading ? '<span class="spinner"></span>' : icon('refresh')}${esc(t('ov.refresh'))}</button>`;

// ---- Sound ----

/** One list of sound devices as radio choices. compact (the overview) leaves out the ones that are away. */
function soundList(app, flow, compact) {
  if (!app.audio) return `<p class="note">${esc(t('st.reading'))}</p>`;
  const items = (flow === 'input' ? app.audio.inputs : app.audio.outputs).filter(e => !compact || e.active);
  if (!items.length) return `<p class="note">${esc(t('s.none'))}</p>`;
  return `<div class="choices" role="radiogroup">${items.map(e => {
    const ic = flow === 'input' && (e.kind === 'laptop' || e.kind === 'other') ? 'mic' : SOUND_ICON[e.kind] ?? 'sound';
    const tags = !e.active ? `<span class="tag">${esc(t('s.away'))}</span>` : e.calls && !e.default ? `<span class="tag">${esc(t('s.calls'))}</span>` : '';
    const busy = app.pendingAudio?.id === e.id ? ' busy' : '';
    return `<button class="choice${busy}" role="radio" aria-checked="${!!e.default}" data-audio="${esc(e.id)}"${e.active ? '' : ' disabled'} title="${esc(e.windows ?? '')}">
      ${icon(ic)}<span><b>${bdi(e.name)}</b>${e.sub ? `<small>${bdi(e.sub)}</small>` : ''}</span>
      <span class="choice-end">${tags}<span class="radio"></span></span></button>`;
  }).join('')}</div>`;
}

const soundLists = (app, compact) =>
  `<h3>${esc(t('s.out'))}</h3>${soundList(app, 'output', compact)}<h3>${esc(t('s.in'))}</h3>${soundList(app, 'input', compact)}`;

export function soundHtml(app) {
  return `<header class="page-head"><h1>${esc(t('s.title'))}</h1><p class="sub">${esc(t('s.sub'))}</p></header>
    <div class="overview" data-brand="sound">
      <section class="panel quick"><h2>${esc(t('s.out'))}</h2>${soundList(app, 'output', false)}</section>
      <section class="panel quick"><h2>${esc(t('s.in'))}</h2>${soundList(app, 'input', false)}</section>
    </div>
    <div class="groups" style="margin-top:16px" data-brand="sound">${callsGroup(app)}</div>`;
}

const toggle = (key, on) => `<button class="toggle" role="switch" aria-checked="${!!on}" data-setting="${key}"></button>`;
const callsGroup = app => `<section class="group"><div class="row"><div class="row-text"><b>${esc(t('set.calls'))}</b><small>${esc(t('set.calls.sub'))}</small></div>${toggle('switchCalls', app.settings.switchCalls)}</div></section>`;

// ---- Settings ----

export function settingsHtml(app) {
  const lang = app.settings.language;
  return `<header class="page-head"><h1>${esc(t('set.title'))}</h1></header>
    <div class="groups" data-brand="hub">
      <section class="group">
        <div class="row"><div class="row-text"><b>${esc(t('set.language'))}</b></div>
          <div class="seg" role="radiogroup"><button role="radio" data-setting="language" data-v="he" aria-checked="${lang === 'he'}">עברית</button><button role="radio" data-setting="language" data-v="en" aria-checked="${lang === 'en'}">English</button></div></div>
        <div class="row"><div class="row-text"><b>${esc(t('set.calls'))}</b><small>${esc(t('set.calls.sub'))}</small></div>${toggle('switchCalls', app.settings.switchCalls)}</div>
      </section>
      <section class="group"><h2>${esc(t('set.background'))}</h2>
        <div class="row"><div class="row-text"><b>${esc(t('set.tray'))}</b><small>${esc(t('set.tray.sub'))}</small></div>${toggle('keepInTray', app.settings.keepInTray)}</div>
        <div class="row"><div class="row-text"><b>${esc(t('set.startup'))}</b><small>${esc(t('set.startup.sub'))}</small></div>${toggle('startWithWindows', app.startWithWindows)}</div>
        <div class="row"><div class="row-text"><b>${esc(t('set.alerts'))}</b><small>${esc(t('set.alerts.sub'))}</small></div>${toggle('batteryAlerts', app.settings.batteryAlerts)}</div>
      </section>
      <p class="note">${esc(t('set.about', { v: app.version }))}</p>
    </div>`;
}

// ---- Device pages ----

/** The coloured head of a device page: its icon, name, connection and the one number that matters. */
export function deviceHeadHtml(app, id) {
  const d = app.devices[id];
  const s = summary(id, d);
  const link = d?.link ? t(`link.${d.link}`) : '';
  const facts = [...new Set([MAKER[id] ?? '', link, ...(id === 'laptop' ? [] : s.facts.filter(f => !/Hz|DPI/.test(f)))])].filter(Boolean);
  const meter = s.num != null
    ? `<div class="dh-meter ${s.cls}"><span class="dh-big">${s.num}<small>${s.unit}</small></span>${s.pct != null ? `<span class="meter"><i style="--v:${s.pct}%"></i></span>` : ''}${s.chip ? `<span class="chip">${esc(s.chip)}</span>` : ''}</div>`
    : `<div class="dh-meter"><span class="dh-state">${esc(s.state ?? '')}</span></div>`;
  return `<header class="dev-head ${s.cls}" id="dev-head" data-brand="${BRAND[id]}">
    <span class="dh-art">${art(id)}</span>
    <div class="dh-title"><h1>${bdi(shortName(id, d))}</h1><p>${facts.map(f => `<span>${bdi(f)}</span>`).join('')}</p></div>
    ${meter}</header>`;
}

export function devicePageHtml(app, id) {
  const body = { mouse: mouseBody, headset: headsetBody, keyboard: keyboardBody, controller: controllerBody, laptop: laptopBody }[id](app);
  return deviceHeadHtml(app, id) + `<div class="groups" data-brand="${BRAND[id]}">${body}</div>`;
}

const row = (title, sub, control, cls = '') =>
  `<div class="row ${cls}"><div class="row-text"><b>${esc(title)}</b>${sub ? `<small>${esc(sub)}</small>` : ''}</div>${control}</div>`;

/** A segmented control: [value, label] pairs, the current one checked. data-ctl names the device action. */
const seg = (ctl, options, current, disabled = false) =>
  `<div class="seg${disabled ? ' busy' : ''}" role="radiogroup" data-ctl="${ctl}">${options.map(([v, label]) =>
    `<button role="radio" data-v="${esc(v)}" aria-checked="${current != null && String(current) === String(v)}">${esc(label)}</button>`).join('')}</div>`;

const empty = (title, sub, action = '') => `<div class="empty-state"><b>${esc(title)}</b><span>${esc(sub)}</span>${action}</div>`;

function mouseBody(app) {
  const d = app.devices.mouse;
  const s = app.deviceSettings.mouse;
  if (!d || d.state === 'missing') return empty(t('st.missing'), t('note.missing.mouse'));
  if (!s) return d.state === 'asleep'
    ? empty(t('m.asleep'), t('m.asleep.sub'), `<button class="btn" data-load="mouse">${icon('refresh')}${esc(t('retry'))}</button>`)
    : empty(t('st.reading'), '');
  const draft = app.drafts.mouse;
  const cur = draft.stages[draft.active];
  const dirty = JSON.stringify(draft.stages) !== JSON.stringify(s.stages) || draft.active !== s.activeStage;
  const stages = `<div class="stages" data-stages>
      <div class="stage-row">${draft.stages.map((v, i) => `<button class="stage" data-stage="${i}" aria-pressed="${i === draft.active}">${v}<small>${esc(i === draft.active ? t('m.active') : t('m.stage', { n: i + 1 }))}</small></button>`).join('')}
        ${draft.stages.length < 5 ? '<button class="stage add" data-stage-add aria-label="+">+</button>' : ''}</div>
      <div class="stage-edit">
        <div class="slider-line"><input type="range" min="100" max="16000" step="50" value="${cur}" data-stage-range aria-label="DPI"><input class="num-input" type="number" min="100" max="16000" step="50" value="${cur}" data-stage-num aria-label="DPI"></div>
        <button class="btn quiet" data-stage-remove${draft.stages.length <= 1 ? ' disabled' : ''}>${esc(t('m.remove'))}</button>
        <button class="btn primary" data-stage-save${dirty ? '' : ' disabled'}>${esc(t('m.save'))}</button>
      </div></div>`;
  return `<section class="group"><h2>${esc(t('m.precision'))}</h2>
      ${row(t('m.stages'), t('m.stages.sub'), stages, 'stack')}
      ${row(t('m.polling'), t('m.polling.sub'), seg('mouse.polling', [[125, '125 Hz'], [500, '500 Hz'], [1000, '1000 Hz']], s.pollingHz))}
    </section>
    <section class="group"><h2>${esc(t('m.power'))}</h2>
      ${row(t('m.sleep'), t('m.sleep.sub'), seg('mouse.sleep', [60, 120, 300, 600, 900].map(v => [v, t('min', { n: v / 60 })]), s.sleepSeconds))}
      ${row(t('m.low'), t('m.low.sub'), seg('mouse.lowBattery', [5, 10, 15, 20, 25].map(v => [v, `${v}%`]), s.lowBatteryPercent))}
    </section>`;
}

function headsetBody(app) {
  const d = app.devices.headset;
  const s = app.deviceSettings.headset ?? {};
  if (!d || d.state === 'missing') return empty(t('st.missing'), t('note.missing.headset'));
  const off = d.state !== 'ready';
  const draft = app.drafts.headset;
  const preset = s.eqPreset;
  const changed = draft && s.eq && draft.eq.some((v, i) => v !== s.eq[i]);
  const eq = `<div class="eq-wrap">
      ${seg('headset.eqPreset', ['flat', 'bass', 'smiley', 'focus'].map(p => [p, t(`h.eq.${p}`)]).concat(preset === 'custom' ? [['custom', t('h.eq.custom')]] : []), changed ? null : preset, off)}
      <div class="eq" data-eq>${eqCurve(draft.eq)}
        <div class="eq-bands">${draft.eq.map((v, i) => `<label class="band"><output>${fmtDb(v)}</output><input type="range" min="-12" max="12" step="0.5" value="${v}" data-band="${i}"${off ? ' disabled' : ''} aria-label="${BANDS[i]} Hz"><span>${BANDS[i]}</span></label>`).join('')}</div>
      </div>
      <div class="eq-actions"><button class="btn primary" data-eq-apply${changed && !off ? '' : ' disabled'}>${esc(t('h.apply'))}</button>
        <button class="btn quiet" data-eq-reset${changed ? '' : ' disabled'}>${esc(t('h.reset'))}</button></div></div>`;
  const unknown = v => v == null ? `<span class="unknown">${esc(t('h.unknown'))}</span>` : '';
  return `${off ? empty(t('h.off'), t('h.off.sub')) : ''}
    <section class="group"><h2>${esc(t('h.voice'))}</h2>
      ${row(t('h.sidetone'), t('h.sidetone.sub'), `<div>${seg('headset.sidetone', [0, 1, 2, 3].map(v => [v, t(`h.side.${v}`)]), s.sidetone, off)}${unknown(s.sidetone)}</div>`)}
      ${row(t('h.autoOff'), t('h.autoOff.sub'), `<div>${seg('headset.autoOff', [[0, t('h.never')], ...[5, 10, 15, 30, 60, 90].map(v => [v, t('min', { n: v })])], s.autoOff, off)}${unknown(s.autoOff)}</div>`)}
    </section>
    <section class="group"><h2>${esc(t('h.eq'))}</h2>${row('', t('h.eq.sub'), eq, 'stack')}</section>
    <p class="note">${esc(t('h.note'))}</p>`;
}

export const fmtDb = v => (v > 0 ? '+' : '') + (Number.isInteger(v) ? v : v.toFixed(1));

/** The curve the ten bands draw, behind their sliders. */
export function eqCurve(bands) {
  const pts = bands.map((v, i) => [((i + 0.5) / bands.length) * 1000, 100 - (v / 12) * 90]);
  const line = pts.map(p => p.join(',')).join(' ');
  return `<svg class="eq-curve" viewBox="0 0 1000 200" preserveAspectRatio="none" aria-hidden="true">
    <line class="zero" x1="0" y1="100" x2="1000" y2="100"/>
    <polygon class="area" points="${pts[0][0]},100 ${line} ${pts.at(-1)[0]},100"/>
    <polyline class="line" points="${line}"/></svg>`;
}

export const EFFECTS = ['static', 'breathing', 'wave', 'spectrum', 'gradient', 'music'];
export const SCREENS = ['off', 'clock', 'devices', 'music', 'cycle', 'text', 'image', 'stars', 'matrix', 'energy', 'sound', 'goal'];

/** Colour swatches with a custom picker at the end; withNone adds "no colour" first. */
const swatches = (ctl, current, withNone = false) => `<div class="swatches" role="radiogroup" data-swatch="${ctl}">
    ${withNone ? `<button class="swatch none" role="radio" data-v="" aria-checked="${!current}" aria-label="${esc(t('c.off'))}"></button>` : ''}
    ${SWATCHES.map(c => `<button class="swatch" role="radio" style="--c:${c}" data-v="${c}" aria-checked="${current?.toUpperCase() === c}" aria-label="${c}"></button>`).join('')}
    <label class="swatch-custom" title="#"><input type="color" value="${current ?? '#ffffff'}" data-swatch-custom="${ctl}"></label></div>`;

function keyboardBody(app) {
  const d = app.devices.keyboard;
  if (!d || d.state === 'missing') return empty(t('st.missing'), t('note.missing.keyboard'));
  const s = app.deviceSettings.keyboard;
  if (!s) return empty(t('st.reading'), '');
  const l = app.drafts.keyboard.lighting;
  const scr = app.drafts.keyboard.screen;
  const fx = l.effect;
  const colours = !l.enabled || fx === 'spectrum' || fx === 'wave' ? ''
    : fx === 'gradient'
      ? row(t('k.from'), '', swatches('main', l.main)) + row(t('k.to'), '', swatches('second', l.second))
      : row(t('k.main'), '', swatches('main', l.main)) + row(t('k.accent'), t('k.accent.sub'), swatches('accent', l.accent, true));
  const slider = (key, label, min, max, value, unit) => row(label, '', `<div class="slider-line"><input type="range" min="${min}" max="${max}" step="1" value="${value}" data-kb="${key}"><output>${value}${unit}</output></div>`);
  const lighting = `<section class="group"><h2>${esc(t('k.lighting'))}</h2>
      ${row(t('k.control'), l.enabled ? t('k.control.on') : t('k.control.off'), `<button class="toggle" role="switch" aria-checked="${l.enabled}" data-kb-enabled></button>`)}
      ${l.enabled ? `<div class="row stack"><div class="cards" role="radiogroup">${EFFECTS.map(e => `<button class="card-pick" role="radio" data-fx="${e}" aria-checked="${fx === e}">
          <span class="fx-swatch" style="background:${effectSwatch(e, l)}"></span><b>${esc(t(`fx.${e}`))}</b><small>${esc(t(`fx.${e}.sub`))}</small></button>`).join('')}</div></div>` : ''}
      ${colours}
      ${l.enabled && ['breathing', 'spectrum', 'wave'].includes(fx) ? slider('speed', t('k.speed'), 1, 10, l.speed, '') : ''}
      ${l.enabled ? slider('brightness', t('k.brightness'), 5, 100, l.brightness, '%') : ''}
      <div class="row stack kb-wrap ${l.enabled ? '' : 'kb-off'}">${keyboardPreview(s.layout, l)}${l.enabled ? '' : `<span class="kb-off-note">${esc(t('k.ownNote'))}</span>`}</div>
    </section>`;

  const option = scr.mode === 'text'
    ? row(t('scr.text'), t('scr.text.sub'), `<div class="text-line"><input class="text-input" type="text" maxlength="60" value="${esc(scr.text)}" data-screen-text dir="auto"><button class="btn primary" data-screen-text-save>${esc(t('scr.show'))}</button></div>`)
    : scr.mode === 'image'
      ? row(t('scr.picture'), t('scr.picture.sub'), `<label class="btn primary">${esc(t(s.hasPicture ? 'scr.pictureChange' : 'scr.pictureChoose'))}<input type="file" accept="image/png,image/jpeg,image/gif,image/bmp,image/webp" hidden data-screen-file></label>`)
      : '';
  const screen = `<section class="group" id="screen-group"><h2>${esc(t('scr.title'))}</h2>
      <div class="row stack"><div class="oled big"><canvas class="oled-canvas" width="128" height="40"></canvas>${scr.mode === 'off' ? `<span class="oled-off">${esc(t('scr.offNote'))}</span>` : ''}</div></div>
      <div class="row stack"><div class="cards screens" role="radiogroup">${SCREENS.map(m => `<button class="card-pick" role="radio" data-screen="${m}" aria-checked="${scr.mode === m}">
          <b>${esc(t(`scr.${m}`))}</b><small>${esc(t(`scr.${m}.sub`))}</small></button>`).join('')}</div></div>
      ${option}
    </section>
    <p class="note">${esc(t('k.note'))}</p>`;
  return lighting + screen;
}
export function scaleHex(h, brightness) {
  const f = (brightness ?? 100) / 100;
  const n = [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16));
  // Never draw a dim key darker than the "off" grey, so the preview stays readable.
  return hex({ r: Math.max(45, n[0] * f), g: Math.max(50, n[1] * f), b: Math.max(60, n[2] * f) });
}

function controllerBody(app) {
  const d = app.devices.controller;
  if (!d || d.state === 'missing') return empty(t('st.missing'), t('note.missing.controller'));
  if (d.link === 'bluetooth') return empty(t('c.bt'), t('c.bt.sub'));
  const c = app.drafts.controller;
  const shown = scaleHex(c.color, c.brightness);
  const players = [0, 0x04, 0x0A, 0x15, 0x1B][c.player];
  return `<section class="group"><h2>${esc(t('c.lights'))}</h2>
      <div class="row stack"><div class="pad-preview"><span class="lightbar" style="--lb:${shown}"></span>
        <span class="players">${[0, 1, 2, 3, 4].map(i => `<i class="${players & (1 << i) ? 'on' : ''}"></i>`).join('')}</span></div></div>
      ${row(t('c.lightbar'), '', `<div class="swatches" role="radiogroup" data-swatch="pad">
        ${SWATCHES.map(h => `<button class="swatch" role="radio" style="--c:${h}" data-v="${h}" aria-checked="${c.color.toUpperCase() === h}" aria-label="${h}"></button>`).join('')}
        <label class="swatch-custom"><input type="color" value="${c.color}" data-swatch-custom="pad"></label></div>`)}
      ${row(t('c.brightness'), '', `<div class="slider-line"><input type="range" min="0" max="100" step="5" value="${c.brightness}" data-pad-brightness><output>${c.brightness}%</output></div>`)}
      ${row(t('c.players'), t('c.players.sub'), seg('controller.player', [[0, t('c.off')], [1, '1'], [2, '2'], [3, '3'], [4, '4']], c.player))}
      ${row(t('c.mic'), t('c.mic.sub'), `<button class="toggle" role="switch" aria-checked="${c.mic}" data-pad-mic></button>`)}
    </section>
    <p class="note">${esc(t('c.note'))}</p>`;
}

function laptopBody(app) {
  const d = app.devices.laptop;
  if (!d || d.state !== 'ready' || d.temperature == null) return empty(stateLabel('laptop', d ?? { state: 'missing' }), stateNote('laptop', d ?? { state: 'missing' }));
  return laptopReadouts(app) + `<p class="note">${esc(t('l.note'))}</p>`;
}

/** The two live readouts and the temperature trail: redrawn every three seconds. */
export function laptopReadouts(app) {
  const d = app.devices.laptop;
  const temp = d.temperature, fan = d.fanDuty ?? 0;
  const hist = app.temps;
  const n = hist.length;
  const min = Math.min(40, ...hist), max = Math.max(95, ...hist);
  const pts = hist.map((v, i) => `${n < 2 ? 0 : (i / (n - 1)) * 1000},${120 - ((v - min) / (max - min)) * 110}`).join(' ');
  const minutes = Math.round((n * 3) / 60);
  return `<div id="laptop-live"><div class="readouts">
      <div class="readout ${temp >= 90 ? 'very-hot' : temp >= 80 ? 'hot' : ''}" data-brand="heat"><b>${esc(t('l.temp'))}</b><span class="big">${temp}<small>°C</small></span><span class="meter"><i style="--v:${Math.min(100, temp)}%"></i></span></div>
      <div class="readout" data-brand="hub"><b>${esc(t('l.fan'))}</b><span class="big">${fan}<small>%</small></span><span class="meter"><i style="--v:${fan}%"></i></span></div>
    </div>
    <section class="panel" style="max-width:980px"><h2>${esc(t('l.history'))}</h2>
      <svg class="spark" viewBox="0 0 1000 120" preserveAspectRatio="none" aria-hidden="true">${n > 1 ? `<polygon class="area" points="0,120 ${pts} 1000,120"/><polyline class="line" points="${pts}"/>` : ''}</svg>
      <div class="spark-legend"><span>${minutes > 0 ? esc(t('l.ago', { n: minutes })) : ''}</span><span>${esc(t('l.now'))}</span></div>
    </section></div>`;
}
