// Moving around like a console: D-pad / stick / arrows move the focus spatially, ✕ / A / Enter selects,
// ○ / B / Esc goes back, △ / Y / O opens Settings. (BoostOS's navigation, without its input tests.)

import { $, $$, reduceMotion } from './ui.js';
import { sfx, rememberPad } from './sound.js';
import { t } from './i18n.js';

const nav = {
  kind: 'keys',            // 'keys' | 'ps' | 'xbox'
  back: () => {},
  options: () => {},
  hints: () => [],
};

export function initNav({ back, options, hints }) {
  Object.assign(nav, { back, options, hints });
  if ([...(navigator.getGamepads?.() ?? [])].some(Boolean)) startPolling();
  renderHints();
}

// Poll at 60 Hz with a timer, and only while a controller is connected. A requestAnimationFrame loop would ask the
// compositor for a new frame on every refresh even when nothing on screen changes.
let pollTimer = 0;
function startPolling() {
  if (!pollTimer) pollTimer = setInterval(() => pollPad(performance.now()), 16);
}
function stopPolling() {
  clearInterval(pollTimer);
  pollTimer = 0;
}

/** What the D-pad can reach: the open dialog only, otherwise the page and the top bar. */
export function navItems() {
  const modal = $('.modal:not([hidden])');
  const scope = modal ? $$('[data-nav]', modal) : [...$$('main [data-nav]'), ...$$('.topbar [data-nav]')];
  return scope.filter(el => !el.disabled && el.offsetParent !== null && !el.closest('[inert]'));
}

export function focusFirst(root) {
  const items = root ? $$('[data-nav]', root).filter(el => !el.disabled && el.offsetParent !== null) : navItems();
  (items.find(el => el.hasAttribute('data-default')) ?? items[0])?.focus({ preventScroll: true });
}

export function focusEl(el) {
  if (!el) return;
  el.focus({ preventScroll: true });
  el.scrollIntoView({ block: 'nearest', inline: 'nearest', behavior: reduceMotion ? 'auto' : 'smooth' });
}

export function moveFocus(dir) {
  const items = navItems();
  if (!items.length) return;
  const cur = items.includes(document.activeElement) ? document.activeElement : null;
  if (!cur) { focusFirst(); return; }

  const a = cur.getBoundingClientRect();
  const ax = a.left + a.width / 2, ay = a.top + a.height / 2;
  let best = null, bestScore = Infinity;
  for (const el of items) {
    if (el === cur) continue;
    const b = el.getBoundingClientRect();
    // Measure to the nearest edge across, so a wide card next to small ones is reached naturally.
    const dx = b.left + b.width / 2 - ax, dy = b.top + b.height / 2 - ay;
    const gapX = Math.max(0, b.left - a.right, a.left - b.right), gapY = Math.max(0, b.top - a.bottom, a.top - b.bottom);
    let along, across;
    if (dir === 'left') { if (dx > -4) continue; along = -dx; across = gapY; }
    else if (dir === 'right') { if (dx < 4) continue; along = dx; across = gapY; }
    else if (dir === 'up') { if (dy > -4) continue; along = -dy; across = gapX; }
    else { if (dy < 4) continue; along = dy; across = gapX; }
    const score = along + across * 3;
    if (score < bestScore) { bestScore = score; best = el; }
  }
  if (best) { focusEl(best); sfx.move(); }
}

function goBack() {
  sfx.back();
  nav.back();
}

// Every press of a navigable control gets the selection sound, unless it asks for its own (data-sfx).
document.addEventListener('click', e => {
  const el = e.target.closest('[data-nav]');
  if (!el || el.disabled) return;
  const kind = el.dataset.sfx ?? 'select';
  if (kind !== 'none') sfx[kind]?.();
});

const DIRS = { ArrowLeft: 'left', ArrowRight: 'right', ArrowUp: 'up', ArrowDown: 'down' };

document.addEventListener('keydown', e => {
  if (DIRS[e.key]) { e.preventDefault(); setKind('keys'); moveFocus(DIRS[e.key]); }
  else if (e.key === 'Escape' || e.key === 'Backspace') { e.preventDefault(); setKind('keys'); goBack(); }
  else if (e.key === 'Enter' && document.activeElement?.matches('[data-nav]:not(button)')) { e.preventDefault(); document.activeElement.click(); }
  else if (e.code === 'KeyO' || e.key === 'ContextMenu') { e.preventDefault(); setKind('keys'); nav.options(); }
});

// The mouse's back button goes back.
document.addEventListener('mouseup', e => {
  if (e.button === 3) { e.preventDefault(); goBack(); }
});

// Hovering moves the selection, like a console cursor. Ignore the fake mouseover Chrome fires while a list scrolls.
let lastRealMouse = 0;
document.addEventListener('mousemove', e => {
  if (e.movementX || e.movementY) {
    lastRealMouse = performance.now();
    document.body.classList.remove('pad-mode');
  }
});
document.addEventListener('mouseover', e => {
  if (performance.now() - lastRealMouse > 120) return;
  const el = e.target.closest('[data-nav]');
  if (el && el !== document.activeElement && !el.disabled && navItems().includes(el)) el.focus({ preventScroll: true });
});

// ---- Controller ----

const pad = { prev: [], held: null, repeatAt: 0 };

function kindOf(gp) {
  const id = gp.id.toLowerCase();
  return id.includes('054c') || id.includes('dualsense') || id.includes('wireless controller') || id.includes('dualshock') ? 'ps' : 'xbox';
}

function pollPad(now) {
  // Only while DeviceHub is visible and focused, so presses inside a game never reach us.
  if (document.hidden || !document.hasFocus()) return;
  const gp = [...(navigator.getGamepads?.() ?? [])].find(Boolean);
  if (!gp) return;
  rememberPad(gp);

  const down = i => !!gp.buttons[i]?.pressed;
  const pressed = i => down(i) && !pad.prev[i];
  const anyPress = gp.buttons.some((b, i) => b.pressed && !pad.prev[i]);
  if (anyPress) { setKind(kindOf(gp)); document.body.classList.add('pad-mode'); }

  if (pressed(0)) document.activeElement?.click?.();
  if (pressed(1)) goBack();
  if (pressed(3)) nav.options();
  let dir = down(12) ? 'up' : down(13) ? 'down' : down(14) ? 'left' : down(15) ? 'right' : null;
  const x = gp.axes[0] ?? 0, y = gp.axes[1] ?? 0;
  if (!dir && Math.hypot(x, y) > 0.55) dir = Math.abs(x) > Math.abs(y) ? (x > 0 ? 'right' : 'left') : (y > 0 ? 'down' : 'up');
  if (dir && dir !== pad.held) { moveFocus(dir); pad.repeatAt = now + 380; }
  else if (dir && now >= pad.repeatAt) { moveFocus(dir); pad.repeatAt = now + 115; }
  pad.held = dir;
  pad.prev = gp.buttons.map(b => b.pressed);
}

export const padSnapshot = () => [...(navigator.getGamepads?.() ?? [])].find(Boolean) ?? null;

window.addEventListener('gamepadconnected', e => {
  setKind(kindOf(e.gamepad));
  startPolling();
  renderHints();
});
window.addEventListener('gamepaddisconnected', () => {
  if ([...(navigator.getGamepads?.() ?? [])].some(Boolean)) return;
  setKind('keys');
  stopPolling();
  renderHints();
});

function setKind(kind) {
  if (nav.kind === kind) return;
  nav.kind = kind;
  renderHints();
}

// The face-button symbols, drawn so they sit exactly in the middle of their ring.
const PS_SYMBOL = {
  cross: '<path d="m7 7 10 10M17 7 7 17"/>',
  circle: '<circle cx="12" cy="12" r="5.6"/>',
  triangle: '<path d="M12 6.2 17.6 16H6.4L12 6.2Z"/>',
};
const psSymbol = name => `<svg viewBox="0 0 24 24" aria-hidden="true">${PS_SYMBOL[name]}</svg>`;

const round = (cls, inner) => `<span class="glyph round ${cls}">${inner}</span>`;
const key = text => `<span class="glyph key">${text}</span>`;
const GLYPHS = {
  select: { ps: round('ps-cross', psSymbol('cross')), xbox: round('xb-a', 'A'), keys: key('Enter') },
  back: { ps: round('ps-circle', psSymbol('circle')), xbox: round('xb-b', 'B'), keys: key('Esc') },
  options: { ps: round('ps-triangle', psSymbol('triangle')), xbox: round('xb-y', 'Y'), keys: key('O') },
  full: { ps: key('F11'), xbox: key('F11'), keys: key('F11') },
};

/** Hints for what is focused: [['select', label], ['back', label], ...] */
export function renderHints() {
  const gp = padSnapshot();
  $('#pad-status').textContent = gp ? t(kindOf(gp) === 'ps' ? 'pad.connected' : 'pad.connectedGeneric') : '';
  $('#hints').innerHTML = nav.hints().map(([g, label]) => `<span class="hint">${GLYPHS[g][nav.kind]}<span>${label}</span></span>`).join('');
}
