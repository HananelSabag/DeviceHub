// Small shared pieces: DOM helpers, icons, colours and the toast.

export const $ = (sel, root = document) => root.querySelector(sel);
export const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];
export const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

const ICONS = {
  overview: '<rect x="3" y="3" width="7.5" height="9" rx="1.6"/><rect x="13.5" y="3" width="7.5" height="5" rx="1.6"/><rect x="13.5" y="11" width="7.5" height="10" rx="1.6"/><rect x="3" y="15" width="7.5" height="6" rx="1.6"/>',
  mouse: '<rect x="6" y="2.5" width="12" height="19" rx="6"/><path d="M12 6.5v3.5"/>',
  headset: '<path d="M4 15v-3a8 8 0 0 1 16 0v3"/><rect x="3" y="14" width="4.5" height="6.5" rx="1.6"/><rect x="16.5" y="14" width="4.5" height="6.5" rx="1.6"/>',
  keyboard: '<rect x="2" y="6" width="20" height="12" rx="2"/><path d="M6 10h.01M10 10h.01M14 10h.01M18 10h.01M6 14h.01M18 14h.01M9 14h6"/>',
  gamepad: '<path d="M7 7h10a5 5 0 0 1 4.9 6l-.7 3.4a2.6 2.6 0 0 1-4.5 1.2L14.6 15H9.4l-2.1 2.6a2.6 2.6 0 0 1-4.5-1.2L2.1 13A5 5 0 0 1 7 7Z"/><path d="M7.5 10v3M6 11.5h3"/><circle cx="16" cy="10.5" r=".7"/><circle cx="17.6" cy="12.6" r=".7"/>',
  laptop: '<rect x="4" y="4.5" width="16" height="11" rx="1.6"/><path d="M2 19.5h20"/>',
  speakers: '<rect x="6" y="2.5" width="12" height="19" rx="2.5"/><circle cx="12" cy="14.5" r="3.6"/><circle cx="12" cy="7.2" r="1.3"/>',
  monitor: '<rect x="2.5" y="4" width="19" height="12.5" rx="2"/><path d="M8.5 20.5h7M12 16.5v4"/>',
  mic: '<rect x="9" y="3" width="6" height="11" rx="3"/><path d="M5.5 11a6.5 6.5 0 0 0 13 0M12 17.5V21M8.5 21h7"/>',
  earbuds: '<path d="M8 4a4 4 0 0 0-4 4v1a3 3 0 0 0 3 3h1V4Z"/><path d="M8 12v7"/><path d="M16 4a4 4 0 0 1 4 4v1a3 3 0 0 1-3 3h-1V4Z"/><path d="M16 12v7"/>',
  sound: '<path d="M4 9.5h3.5L12 5.5v13l-4.5-4H4v-5Z"/><path d="M15.5 9a4 4 0 0 1 0 6M18 6.5a7.5 7.5 0 0 1 0 11"/>',
  refresh: '<path d="M20 11a8 8 0 0 0-14.6-4.4L4 8.5"/><path d="M4 3.5v5h5"/><path d="M4 13a8 8 0 0 0 14.6 4.4L20 15.5"/><path d="M20 20.5v-5h-5"/>',
  bolt: '<path d="M13 2 4.5 13.5H11L10 22l8.5-11.5H12L13 2Z"/>',
  gear: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1Z"/>',
};

export const icon = name => `<svg class="ic" viewBox="0 0 24 24" aria-hidden="true">${ICONS[name] ?? ICONS.sound}</svg>`;

export const hex = ({ r, g, b }) => '#' + [r, g, b].map(v => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0')).join('');
export const rgb = h => ({ r: parseInt(h.slice(1, 3), 16), g: parseInt(h.slice(3, 5), 16), b: parseInt(h.slice(5, 7), 16) });

let toastTimer;
export function toast(message, bad = false) {
  const el = $('#toast');
  el.textContent = message;
  el.classList.toggle('bad', bad);
  el.classList.add('show');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => el.classList.remove('show'), bad ? 5200 : 2800);
}
