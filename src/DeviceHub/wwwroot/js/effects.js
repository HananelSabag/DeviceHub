// The keyboard effects, computed the same way as the host's KeyboardEffects.cs, for the page's preview only.
// (The keyboard itself is driven by the host; this just draws what it will show.)

const parse = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16));
const scale = (c, f) => c.map(v => Math.round(v * f));
const lerp = (a, b, t) => a.map((v, i) => Math.round(v + (b[i] - v) * t));

function hue(h) {
  h = ((h % 1) + 1) % 1 * 6;
  const x = 1 - Math.abs((h % 2) - 1);
  const [r, g, b] = [[1, x, 0], [x, 1, 0], [0, 1, x], [0, x, 1], [x, 0, 1], [1, 0, x]][Math.floor(h)];
  return [r, g, b].map(v => Math.round(v * 255));
}

export const ANIMATED = ['breathing', 'spectrum', 'wave', 'music'];

/** The colour of one key ({ code, x, w } in key units) at time t, as #rrggbb. */
export function keyColor(l, key, layout, t) {
  const main = parse(l.main), second = parse(l.second ?? '#1E5BFF');
  const accent = l.accent && layout.accent.includes(key.code) ? parse(l.accent) : null;
  const own = accent ?? main;
  const x = (key.x + key.w / 2) / layout.width;
  const speed = Math.min(10, Math.max(1, l.speed ?? 5));
  let c;
  switch (l.effect) {
    case 'breathing': c = scale(own, 0.12 + 0.88 * Math.sin(Math.PI * t / (8.5 - speed * 0.7)) ** 2); break;
    case 'spectrum': c = hue(t * speed * 0.02); break;
    case 'wave': c = hue(x * 0.85 - t * speed * 0.06); break;
    case 'gradient': c = lerp(main, second, x); break;
    // The preview has no sound to follow: it shows a beat instead.
    case 'music': c = accent ? own : scale(own, 0.08 + 0.92 * Math.abs(Math.sin(t * 3.2)) ** 3); break;
    default: c = own;
  }
  c = scale(c, (l.brightness ?? 100) / 100);
  return '#' + c.map(v => Math.max(0, Math.min(255, v)).toString(16).padStart(2, '0')).join('');
}

/** The keyboard drawn from the host's key map, every key in its colour (or dark when DeviceHub isn't lighting it). */
export function keyboardPreview(layout, lighting, t = 0) {
  const U = 30, G = 3;
  const on = lighting?.enabled;
  const rects = layout.keys.map(([code, x, y, w, h]) => {
    const fill = on ? keyColor(lighting, { code, x, w }, layout, t) : '#2D323C';
    return `<rect data-code="${code}" x="${x * U + G / 2}" y="${y * U + G / 2}" width="${w * U - G}" height="${h * U - G}" rx="3" fill="${fill}"/>`;
  }).join('');
  return `<svg class="kb-preview" viewBox="0 0 ${layout.width * U} ${layout.height * U}" role="img" aria-label="Apex 5">${rects}</svg>`;
}

/** Recolours an existing preview in place (no new elements): used for the animated preview, 10 times a second. */
export function recolor(svg, layout, lighting, t) {
  if (!svg) return;
  const rects = svg.children;
  layout.keys.forEach(([code, x, , w], i) => rects[i]?.setAttribute('fill', keyColor(lighting, { code, x, w }, layout, t)));
}

/** A tiny swatch that hints at each effect, for the effect buttons. */
export function effectSwatch(effect, l) {
  const main = l.main, second = l.second ?? '#1E5BFF';
  switch (effect) {
    case 'breathing': return `linear-gradient(90deg, #111 0%, ${main} 50%, #111 100%)`;
    case 'spectrum': return 'linear-gradient(90deg, #ff2a2a, #ffd000, #2bea5a, #00c8ff, #8b3dff, #ff2d95)';
    case 'wave': return 'repeating-linear-gradient(90deg, #ff2a2a 0 12%, #ffd000 12% 24%, #2bea5a 24% 36%, #00c8ff 36% 48%, #8b3dff 48% 60%)';
    case 'gradient': return `linear-gradient(90deg, ${main}, ${second})`;
    case 'music': return `repeating-linear-gradient(90deg, ${main} 0 8%, #111 8% 12%, ${main} 12% 15%, #111 15% 22%)`;
    default: return main;
  }
}
