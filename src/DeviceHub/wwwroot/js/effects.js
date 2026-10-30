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

export const ANIMATED = ['breathing', 'spectrum', 'wave', 'music', 'reactive', 'ripple', 'starlight', 'rain', 'fire', 'aurora', 'visualizer'];

const seed = code => ((Math.imul(code, 2654435761) >>> 0) % 1000) / 1000;
const HEIGHT = 6.5;

/** The CPU heat as a colour, the same stops as the host's KeyboardEffects.Heat. */
export function heatColor(c) {
  const stops = [[50, [30, 91, 255]], [62, [43, 234, 90]], [75, [255, 208, 0]], [85, [255, 106, 26]], [92, [255, 42, 42]]];
  if (c <= stops[0][0]) return stops[0][1];
  for (let i = 1; i < stops.length; i++) if (c <= stops[i][0]) return lerp(stops[i - 1][1], stops[i][1], (c - stops[i - 1][0]) / (stops[i][0] - stops[i - 1][0]));
  return stops.at(-1)[1];
}

function fire(y, x, t, speed, s) {
  const flicker = 0.5 + 0.5 * Math.sin(t * (3 + speed * 0.6) + x * 9.1 + s * 5) * Math.sin(t * 3.7 + x * 4.3 + s * 2);
  const heat = Math.min(1, Math.max(0, y * 1.25 - 0.45 + flicker * 0.55));
  return heat < 0.4 ? lerp([0, 0, 0], [170, 8, 0], heat / 0.4)
    : heat < 0.75 ? lerp([170, 8, 0], [255, 100, 0], (heat - 0.4) / 0.35)
    : lerp([255, 100, 0], [255, 210, 60], (heat - 0.75) / 0.25);
}

// The preview has no real key presses: it pretends one every 1.5 s, on a key picked from this row of letters.
const DEMO_KEYS = [[4.5, 3.5], [8.5, 2.5], [6.5, 4.5], [11.5, 3.5], [3.5, 2.5], [9.5, 4.5]];
const demoPress = t => { const i = Math.floor(t / 1.5); return { age: t - i * 1.5, at: DEMO_KEYS[i % DEMO_KEYS.length] }; };

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
    case 'reactive':
    case 'ripple': {
      const fade = 1.95 - speed * 0.145;
      const { age, at } = demoPress(t);
      const kx = key.x + key.w / 2, ky = (key.y ?? 0) + 0.5;
      const same = Math.abs(kx - at[0]) < 0.6 && Math.abs(ky - at[1]) < 0.6;
      let v = same ? Math.max(0, 1 - age / fade) : 0;
      if (l.effect === 'ripple') {
        const life = fade * 1.6, r = age * (6 + speed * 1.8);
        const d = Math.sqrt((kx - at[0]) ** 2 + (ky - at[1]) ** 2 * 1.3);
        v = Math.max(v, Math.max(0, 1 - Math.abs(d - r) / 1.3) * Math.max(0, 1 - age / life));
      }
      c = lerp(scale(second, 0.35), main, Math.min(1, v));
      break;
    }
    case 'starlight': c = lerp(scale(second, 0.12), main, Math.max(0, Math.sin((t * (0.25 + speed * 0.07) + seed(key.code) * 7.3) * Math.PI * 2)) ** 12); break;
    case 'rain': {
      const column = Math.floor(key.x + key.w / 2), period = 2.3 - speed * 0.16;
      const head = ((t / period + seed(column * 7 + 3)) % 1.5) * (HEIGHT + 2) - 1;
      const behind = head - ((key.y ?? 0) + 0.5);
      c = lerp(scale(second, 0.1), main, behind >= 0 && behind < 2.4 ? 1 - behind / 2.4 : 0);
      break;
    }
    case 'fire': c = fire(((key.y ?? 0) + 0.5) / HEIGHT, x, t, speed, seed(key.code)); break;
    case 'aurora': c = scale(lerp(main, second, 0.5 + 0.5 * Math.sin(x * 5 + t * speed * 0.25 + Math.sin((key.y ?? 0) * 1.3 + t * 0.6) * 1.5)), 0.65 + 0.35 * Math.sin(x * 3 - t * 0.4)); break;
    case 'visualizer': {
      // The preview has no sound: a level that rises and falls on its own.
      const level = 0.45 + 0.4 * Math.sin(t * 2.1) * Math.sin(t * 0.7 + 1);
      const height = 1 - ((key.y ?? 0) + 0.5) / HEIGHT, column = Math.floor(x * 22);
      c = height <= level * 1.35 * (0.7 + 0.3 * Math.sin(column * 1.7 + t * 9)) ? lerp(main, second, height) : scale(main, 0.06);
      break;
    }
    case 'temperature': c = heatColor(l.previewTemp ?? 65); break;
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
    const fill = on ? keyColor(lighting, { code, x, y, w }, layout, t) : '#2D323C';
    return `<rect data-code="${code}" x="${x * U + G / 2}" y="${y * U + G / 2}" width="${w * U - G}" height="${h * U - G}" rx="3" fill="${fill}"/>`;
  }).join('');
  return `<svg class="kb-preview" viewBox="0 0 ${layout.width * U} ${layout.height * U}" role="img" aria-label="Apex 5">${rects}</svg>`;
}

/** Recolours an existing preview in place (no new elements): used for the animated preview, 10 times a second. */
export function recolor(svg, layout, lighting, t) {
  if (!svg) return;
  const rects = svg.children;
  layout.keys.forEach(([code, x, y, w], i) => rects[i]?.setAttribute('fill', keyColor(lighting, { code, x, y, w }, layout, t)));
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
    case 'reactive': return `radial-gradient(circle at 30% 50%, ${main} 0 14%, ${second} 15% 100%)`;
    case 'ripple': return `radial-gradient(circle at 30% 50%, ${second} 0 20%, ${main} 22% 30%, ${second} 32% 100%)`;
    case 'aurora': return `linear-gradient(90deg, ${main}, ${second}, ${main})`;
    case 'starlight': return `radial-gradient(circle at 20% 50%, ${main} 0 6%, transparent 7%), radial-gradient(circle at 65% 50%, ${main} 0 5%, transparent 6%), #111`;
    case 'rain': return `repeating-linear-gradient(90deg, ${main} 0 6%, #111 6% 18%)`;
    case 'fire': return 'linear-gradient(90deg, #aa0800, #ff6400, #ffd23c, #ff6400, #aa0800)';
    case 'visualizer': return `linear-gradient(90deg, ${main} 0 55%, #111 55%)`;
    case 'temperature': return 'linear-gradient(90deg, #1e5bff, #2bea5a, #ffd000, #ff6a1a, #ff2a2a)';
    default: return main;
  }
}
