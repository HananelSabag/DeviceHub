// DeviceHub front end: the rail of devices, and one page at a time (overview, a device, sound, settings).
// Readings arrive from the host on their own; settings are asked for when a device page opens; every change is
// sent as { cmd: 'control', device, action, value } and the host answers with the device's settings read back.

import { setLang, t } from './i18n.js';
import { on, send } from './bridge.js';
import { $, $$, toast, rgb, hex } from './ui.js';
import { DEVICES, railHtml, overviewHtml, soundHtml, settingsHtml, devicePageHtml, deviceHeadHtml, laptopReadouts, eqCurve, fmtDb, scaleHex } from './views.js';
import { ANIMATED, recolor } from './effects.js';

const app = {
  settings: { language: 'he', switchCalls: true, keepInTray: true, batteryAlerts: true },
  startWithWindows: false,
  version: '',
  page: 'overview',
  devices: {},          // id → latest reading
  readAt: null,
  reading: false,
  audio: null,
  pendingAudio: null,   // the sound device just pressed, until Windows confirms it
  deviceSettings: {},   // id → settings read from the device (or remembered by the host)
  drafts: {},           // id → what the page is editing before it is sent
  temps: [],            // the laptop's temperature every 3 s, for the last 5 minutes
  screen: null,         // the keyboard screen's current picture (640 bytes)
};

// ---- Drawing ----

function renderRail() { $('#rail').innerHTML = railHtml(app); }

function renderPage() {
  const page = $('#page');
  const scroll = page.scrollTop;
  page.innerHTML = app.page === 'overview' ? overviewHtml(app)
    : app.page === 'sound' ? soundHtml(app)
    : app.page === 'settings' ? settingsHtml(app)
    : devicePageHtml(app, app.page);
  page.scrollTop = scroll;
  drawScreen();
  previewTimer();
}

const renderAll = () => { renderRail(); renderPage(); };

/** What on a device page depends on the reading (asleep, unplugged, USB or Bluetooth): when it changes, redraw the page. */
const shape = d => d ? `${d.state}|${d.link ?? ''}` : '';
const shapes = {};

/** The pages that show the keyboard's screen ask the host for its pictures; the others don't. */
const showsScreen = page => page === 'overview' || page === 'keyboard';

function go(page) {
  if (app.page === page) return;
  if (showsScreen(page) !== showsScreen(app.page)) send('watchScreen', { on: showsScreen(page) });
  app.page = page;
  $('#page').scrollTop = 0;
  renderAll();
  if (DEVICES.includes(page) && page !== 'laptop') send('settingsFor', { device: page });
}

// ---- The keyboard screen and the lighting preview ----

/** Paints the screen's picture into every OLED canvas on the page, white pixels a little blue like the real thing. */
function drawScreen() {
  if (!app.screen) return;
  for (const canvas of $$('.oled-canvas')) {
    const ctx = canvas.getContext('2d');
    const img = ctx.createImageData(128, 40);
    for (let i = 0; i < 128 * 40; i++) {
      const on = app.screen[i >> 3] & (0x80 >> (i & 7));
      img.data.set(on ? [226, 236, 255, 255] : [6, 8, 12, 255], i * 4);
    }
    ctx.putImageData(img, 0, 0);
  }
}

let animTimer = 0;
/** A moving effect animates its preview ten times a second, only while the keyboard page shows it. */
function previewTimer() {
  clearInterval(animTimer);
  const l = app.drafts.keyboard?.lighting;
  const layout = app.deviceSettings.keyboard?.layout;
  if (app.page !== 'keyboard' || !l?.enabled || !ANIMATED.includes(l.effect) || !layout) return;
  const start = performance.now();
  animTimer = setInterval(() => recolor($('.kb-preview'), layout, l, (performance.now() - start) / 1000), 100);
}
document.addEventListener('visibilitychange', () => { if (document.hidden) clearInterval(animTimer); else previewTimer(); });

// ---- Drafts: what a page edits before sending ----

function draftFrom(id, s) {
  switch (id) {
    case 'mouse': return s ? { stages: [...s.stages], active: s.activeStage } : null;
    case 'headset': return { eq: [...(s?.eq ?? Array(10).fill(0))] };
    case 'keyboard': return {
      lighting: { enabled: false, effect: 'static', main: '#FF6A1A', accent: null, second: '#1E5BFF', speed: 5, brightness: 100, ...s?.lighting },
      screen: { mode: 'off', text: '', ...s?.screen },
    };
    case 'controller': {
      const l = s?.lights;
      if (!l) return { color: '#1E5BFF', brightness: 100, player: 1, mic: false };
      const max = Math.max(l.red, l.green, l.blue);
      const color = max > 0 ? hex({ r: l.red * 255 / max, g: l.green * 255 / max, b: l.blue * 255 / max }) : '#1E5BFF';
      return { color, brightness: Math.round(max / 255 * 20) * 5, player: l.player, mic: l.micLed };
    }
  }
  return null;
}

for (const id of DEVICES) app.drafts[id] = draftFrom(id, null);

// ---- Sending changes ----

function control(device, action, value, busyEl) {
  busyEl?.classList.add('busy');
  send('control', { device, action, value });
}

function controllerValue(c) {
  const { r, g, b } = rgb(c.color);
  const f = c.brightness / 100;
  return { red: Math.round(r * f), green: Math.round(g * f), blue: Math.round(b * f), player: c.player, micLed: c.mic };
}

const applyLighting = () => control('keyboard', 'lighting', app.drafts.keyboard.lighting);
const applyScreen = () => control('keyboard', 'screen', app.drafts.keyboard.screen);
const applyController = () => control('controller', 'lights', controllerValue(app.drafts.controller));

function pressSound(id) {
  const all = [...(app.audio?.outputs ?? []), ...(app.audio?.inputs ?? [])];
  const e = all.find(x => x.id === id);
  if (!e || !e.active || e.default) return;
  app.pendingAudio = { id, flow: e.flow, name: e.name };
  send('setAudio', { id });
  renderPage();
}

function saveSettings(next, extra = {}) {
  const langChanged = next.language !== app.settings.language;
  app.settings = next;
  send('saveSettings', { settings: next, ...extra });
  if (langChanged) setLang(next.language);
  renderAll();
}

/** A picture from the PC for the keyboard's screen: read here, converted by the host. */
function sendPicture(file) {
  if (!file) return;
  const reader = new FileReader();
  reader.onload = () => { toast(t('scr.reading')); control('keyboard', 'picture', reader.result); };
  reader.readAsDataURL(file);
}

// ---- Clicks ----

document.addEventListener('click', e => {
  const el = e.target.closest('button, [data-page]');
  if (!el || el.disabled) return;
  const d = el.dataset;

  if (d.page) { go(d.page); return; }
  if ('refresh' in d) { send('refresh'); return; }
  if (d.load) { send('settingsFor', { device: d.load }); return; }
  if (d.audio) { pressSound(d.audio); return; }

  if (d.setting) {
    if (d.setting === 'startWithWindows') {
      app.startWithWindows = !app.startWithWindows;
      saveSettings(app.settings, { startWithWindows: app.startWithWindows });
      return;
    }
    const value = d.setting === 'language' ? d.v : !app.settings[d.setting];
    saveSettings({ ...app.settings, [d.setting]: value });
    return;
  }

  // Keyboard: lighting and screen
  const kb = app.drafts.keyboard;
  if ('kbEnabled' in d) { kb.lighting.enabled = !kb.lighting.enabled; renderPage(); applyLighting(); return; }
  if (d.fx) { kb.lighting.effect = d.fx; renderPage(); applyLighting(); return; }
  if (d.screen) { kb.screen.mode = d.screen; renderPage(); applyScreen(); return; }
  if ('screenTextSave' in d) { kb.screen.text = $('[data-screen-text]').value; applyScreen(); return; }

  const segEl = el.closest('[data-ctl]');
  if (segEl && d.v != null) {
    const [device, action] = segEl.dataset.ctl.split('.');
    if (device === 'controller' && action === 'player') { app.drafts.controller.player = Number(d.v); renderPage(); applyController(); return; }
    if (el.getAttribute('aria-checked') === 'true') return;
    control(device, action, action === 'eqPreset' ? d.v : Number(d.v), segEl);
    return;
  }

  // Mouse: DPI stages
  const m = app.drafts.mouse;
  if (d.stage != null) { m.active = Number(d.stage); renderPage(); return; }
  if ('stageAdd' in d) { m.stages.push(Math.min(16000, (m.stages.at(-1) ?? 800) + 400)); m.active = m.stages.length - 1; renderPage(); return; }
  if ('stageRemove' in d) { m.stages.splice(m.active, 1); m.active = Math.min(m.active, m.stages.length - 1); renderPage(); return; }
  if ('stageSave' in d) { control('mouse', 'stages', { stages: m.stages, active: m.active }, el); return; }

  // Headset: equalizer
  if ('eqApply' in d) { control('headset', 'eq', app.drafts.headset.eq, el); return; }
  if ('eqReset' in d) { app.drafts.headset = draftFrom('headset', app.deviceSettings.headset); renderPage(); return; }

  // Keyboard and controller colours
  const sw = el.closest('[data-swatch]');
  if (sw && el.classList.contains('swatch')) {
    const which = sw.dataset.swatch;
    if (which === 'pad') { app.drafts.controller.color = d.v; renderPage(); applyController(); }
    else { kb.lighting[which] = d.v || null; renderPage(); applyLighting(); }
    return;
  }
  if ('padMic' in d) { app.drafts.controller.mic = !app.drafts.controller.mic; renderPage(); applyController(); }
});

document.addEventListener('keydown', e => {
  if (e.key === 'Enter' && e.target.matches?.('[data-screen-text]')) { app.drafts.keyboard.screen.text = e.target.value; applyScreen(); }
});

// ---- Sliders and pickers: draw while dragging, send on release ----

document.addEventListener('input', e => {
  const el = e.target;
  const d = el.dataset;
  if ('stageRange' in d || 'stageNum' in d) {
    const m = app.drafts.mouse;
    const v = Math.max(100, Math.min(16000, Math.round(Number(el.value) / 50) * 50 || 100));
    m.stages[m.active] = v;
    if ('stageRange' in d) $('[data-stage-num]').value = v; else $('[data-stage-range]').value = v;
    const pill = $(`[data-stage="${m.active}"]`);
    if (pill) pill.firstChild.textContent = v;
    $('[data-stage-save]').disabled = false;
    return;
  }
  if (d.band != null) {
    const eq = app.drafts.headset.eq;
    eq[Number(d.band)] = Number(el.value);
    el.previousElementSibling.textContent = fmtDb(eq[Number(d.band)]);
    $('[data-eq] .eq-curve').outerHTML = eqCurve(eq);
    $('[data-eq-apply]').disabled = app.devices.headset?.state !== 'ready';
    $('[data-eq-reset]').disabled = false;
    $$('[data-ctl="headset.eqPreset"] button').forEach(b => b.setAttribute('aria-checked', 'false'));
    return;
  }
  const l = app.drafts.keyboard.lighting;
  const layout = app.deviceSettings.keyboard?.layout;
  if (d.kb) {
    l[d.kb] = Number(el.value);
    el.nextElementSibling.textContent = d.kb === 'brightness' ? `${el.value}%` : el.value;
    if (layout) recolor($('.kb-preview'), layout, l, 0);
    return;
  }
  if ('padBrightness' in d) {
    app.drafts.controller.brightness = Number(el.value);
    el.nextElementSibling.textContent = `${el.value}%`;
    $('.lightbar').style.setProperty('--lb', scaleHex(app.drafts.controller.color, app.drafts.controller.brightness));
    return;
  }
  if (d.swatchCustom) {
    if (d.swatchCustom === 'pad') {
      app.drafts.controller.color = el.value.toUpperCase();
      $('.lightbar').style.setProperty('--lb', scaleHex(app.drafts.controller.color, app.drafts.controller.brightness));
    } else {
      l[d.swatchCustom] = el.value.toUpperCase();
      if (layout) recolor($('.kb-preview'), layout, l, 0);
    }
  }
});

document.addEventListener('change', e => {
  const d = e.target.dataset;
  if (d.kb || ['main', 'accent', 'second'].includes(d.swatchCustom)) { applyLighting(); renderPage(); }
  else if ('padBrightness' in d || d.swatchCustom === 'pad') { applyController(); renderPage(); }
  else if ('screenFile' in d) sendPicture(e.target.files?.[0]);
});

// ---- Messages from the host ----

on('hello', h => {
  app.version = h.version;
  app.settings = { ...app.settings, ...h.settings };
  app.startWithWindows = !!h.startWithWindows;
  setLang(app.settings.language);
  renderAll();
  send('settingsFor', { device: 'keyboard' }); // the overview shows the keyboard's screen
  send('watchScreen', { on: showsScreen(app.page) });
});
on('settings', s => { app.settings = { ...app.settings, ...s.settings }; app.startWithWindows = !!s.startWithWindows; });
on('devicesBusy', () => { app.reading = true; if (app.page === 'overview') renderPage(); });

on('devices', list => {
  app.reading = false;
  app.readAt = new Date();
  for (const d of list ?? []) app.devices[d.id] = d;
  renderRail();
  const id = app.page;
  if (!DEVICES.includes(id)) { if (app.page === 'overview') renderPage(); return; }
  const now = shape(app.devices[id]);
  if (shapes[id] !== now) {
    shapes[id] = now;
    // A device that just woke up or came back: ask for its settings again.
    if (app.devices[id]?.state === 'ready' && id !== 'laptop') send('settingsFor', { device: id });
    renderPage();
  } else {
    $('#dev-head')?.replaceWith(Object.assign(document.createElement('div'), { innerHTML: deviceHeadHtml(app, id) }).firstElementChild);
  }
});

on('laptop', d => {
  app.devices.laptop = d;
  if (d.temperature != null) { app.temps.push(d.temperature); if (app.temps.length > 100) app.temps.shift(); }
  renderRail();
  if (app.page === 'overview') renderPage();
  if (app.page === 'laptop') {
    if ($('#laptop-live')) {
      $('#laptop-live').outerHTML = laptopReadouts(app);
      $('#dev-head').outerHTML = deviceHeadHtml(app, 'laptop');
    } else renderPage();
  }
});

on('screen', b64 => {
  const raw = atob(b64);
  app.screen = Uint8Array.from(raw, c => c.charCodeAt(0));
  drawScreen();
});

on('audio', state => {
  app.audio = state;
  const p = app.pendingAudio;
  if (p && (p.flow === 'input' ? state.inputs : state.outputs).find(x => x.id === p.id)?.default) {
    app.pendingAudio = null;
    toast(t(p.flow === 'input' ? 's.switchedIn' : 's.switched', { name: p.name }));
  }
  renderRail();
  if (app.page === 'overview' || app.page === 'sound') renderPage();
});

on('deviceSettings', ({ device, settings }) => {
  app.deviceSettings[device] = settings;
  app.drafts[device] = draftFrom(device, settings);
  shapes[device] = shape(app.devices[device]);
  if (app.page === device || (device === 'keyboard' && app.page === 'overview')) renderPage();
});

// The keyboard changes as you click, so it needs no "saved" note; the others confirm.
on('controlDone', c => { if (c.device !== 'keyboard') toast(t('saved')); });

on('error', e => {
  if (e.cmd === 'setAudio') { app.pendingAudio = null; renderPage(); }
  if (e.cmd === 'control' && e.device) {
    // Back to what the device really has.
    app.drafts[e.device] = draftFrom(e.device, app.deviceSettings[e.device]);
    if (app.page === e.device) renderPage();
  }
  toast(t('error', { msg: e.message }), true);
});

send('hello');
