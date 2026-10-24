// Messages to and from the C# host. In a plain browser (design preview) a small fake host answers instead,
// with readings like the ones this PC gives.

const host = window.chrome?.webview;
const listeners = {};

export const on = (type, fn) => (listeners[type] ??= []).push(fn);
export const emit = (type, data) => (listeners[type] ?? []).forEach(fn => fn(data));
export const inHost = !!host;

host?.addEventListener('message', e => emit(e.data.type, e.data.data));

export function send(cmd, extra = {}) {
  if (host) host.postMessage({ cmd, ...extra });
  else Preview.handle(cmd, extra);
}

const Preview = {
  settings: { language: new URLSearchParams(location.search).get('lang') ?? 'he', sounds: true, startFullscreen: false, switchCalls: true },
  devices: [
    { id: 'mouse', model: 'Razer Basilisk X HyperSpeed', state: 'ready', link: 'dongle', battery: 94, pollingHz: 1000, dpi: 16000, dpiY: 16000 },
    { id: 'headset', model: 'SteelSeries Arctis 7+', state: 'asleep', link: 'dongle' },
    { id: 'controller', model: 'DualSense Edge', state: 'ready', link: 'usb', battery: 100, full: true },
    { id: 'laptop', model: 'GIGABYTE G5 KD', state: 'ready', temperature: 64, fanDuty: 64 },
  ],
  audio: {
    outputs: [
      { id: 'a', flow: 'output', name: 'Arctis 7+', sub: 'אוזניות SteelSeries', kind: 'headset', active: true, default: false, calls: true },
      { id: 'b', flow: 'output', name: 'רמקולי Logitech', sub: 'בלוטות\' · UGREEN-90748', kind: 'speakers', active: false },
      { id: 'c', flow: 'output', name: 'VG27AQ1A', sub: 'מסך · HDMI / DisplayPort', kind: 'monitor', active: true },
      { id: 'd', flow: 'output', name: 'DualSense Edge', sub: 'רמקול ושקע אוזניות בשלט', kind: 'controller', active: true },
      { id: 'e', flow: 'output', name: 'רמקולי הלפטופ', sub: 'מובנה בלפטופ', kind: 'laptop', active: true, default: true },
    ],
    inputs: [
      { id: 'f', flow: 'input', name: 'Arctis 7+', sub: 'המיקרופון של האוזניות', kind: 'headset', active: true },
      { id: 'g', flow: 'input', name: 'DualSense Edge', sub: 'המיקרופון של השלט', kind: 'controller', active: true, default: true, calls: true },
      { id: 'h', flow: 'input', name: 'המיקרופון של הלפטופ', sub: 'מובנה בלפטופ', kind: 'laptop', active: true },
    ],
  },
  handle(cmd, extra) {
    const later = (type, data, ms = 200) => setTimeout(() => emit(type, data), ms);
    switch (cmd) {
      case 'hello':
        later('hello', { settings: this.settings, version: 'preview' }, 0);
        later('audio', this.audio, 50);
        later('devices', this.devices.map(d => ({ ...d, at: new Date().toISOString() })), 400);
        break;
      case 'refresh': later('devicesBusy', {}, 0); later('devices', this.devices.map(d => ({ ...d, at: new Date().toISOString() })), 700); break;
      case 'saveSettings': this.settings = extra.settings; later('settings', this.settings, 0); break;
      case 'setAudio': {
        const list = [...this.audio.outputs, ...this.audio.inputs];
        const flow = list.find(e => e.id === extra.id)?.flow;
        list.filter(e => e.flow === flow).forEach(e => { e.default = e.id === extra.id; if (this.settings.switchCalls) e.calls = e.default; });
        later('audio', this.audio, 150);
        break;
      }
      default: emit('preview', cmd);
    }
  },
};
