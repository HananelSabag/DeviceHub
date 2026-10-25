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
  settings: { language: new URLSearchParams(location.search).get('lang') ?? 'he', switchCalls: true },
  devices: [
    { id: 'mouse', model: 'Razer Basilisk X HyperSpeed', state: 'ready', link: 'dongle', battery: 95, pollingHz: 1000, dpi: 16000, dpiY: 16000 },
    { id: 'headset', model: 'SteelSeries Arctis 7+', state: 'ready', link: 'dongle', battery: 75, chatGame: 92, chatChat: 100 },
    { id: 'keyboard', model: 'SteelSeries Apex 5', state: 'ready', link: 'usb' },
    { id: 'controller', model: 'DualSense Edge', state: 'ready', link: 'usb', battery: 100, full: true },
    { id: 'laptop', model: 'GIGABYTE G5 KD', state: 'ready', temperature: 68, fanDuty: 66 },
  ],
  deviceSettings: {
    mouse: { pollingHz: 1000, dpi: 16000, stages: [800, 1800, 4600, 7200, 16000], activeStage: 4, sleepSeconds: 300, lowBatteryPercent: 5 },
    headset: { sidetone: null, autoOff: null, eqPreset: null, eq: null },
    keyboard: { lighting: null },
    controller: { lights: { red: 0, green: 60, blue: 255, player: 1, micLed: false } },
  },
  audio: {
    outputs: [
      { id: 'a', flow: 'output', name: 'Arctis 7+', sub: 'אוזניות SteelSeries', kind: 'headset', active: true },
      { id: 'b', flow: 'output', name: 'רמקולי Logitech', sub: 'דרך UGREEN-90748', kind: 'speakers', active: true, default: true, calls: true },
      { id: 'c', flow: 'output', name: 'VG27AQ1A', sub: 'מסך', kind: 'monitor', active: true },
      { id: 'd', flow: 'output', name: 'DualSense Edge', sub: 'רמקול ושקע אוזניות', kind: 'controller', active: true },
      { id: 'e', flow: 'output', name: 'רמקולי הלפטופ', sub: 'מובנה בלפטופ', kind: 'laptop', active: true },
    ],
    inputs: [
      { id: 'f', flow: 'input', name: 'Arctis 7+', sub: 'המיקרופון של האוזניות', kind: 'headset', active: true, default: true },
      { id: 'g', flow: 'input', name: 'UGREEN לשיחות', sub: 'מוריד את הרמקולים לאיכות טלפון', kind: 'speakers', active: true, calls: true },
      { id: 'h', flow: 'input', name: 'DualSense Edge', sub: 'המיקרופון של השלט', kind: 'controller', active: true },
      { id: 'i', flow: 'input', name: 'מיקרופון הלפטופ', sub: 'מובנה בלפטופ', kind: 'laptop', active: true },
    ],
  },
  handle(cmd, extra) {
    const later = (type, data, ms = 200) => setTimeout(() => emit(type, data), ms);
    switch (cmd) {
      case 'hello':
        later('hello', { settings: this.settings, version: 'preview' }, 0);
        later('audio', this.audio, 50);
        later('devices', this.devices, 300);
        setInterval(() => emit('laptop', { ...this.devices[4], temperature: 62 + Math.round(Math.random() * 10) }), 3000);
        break;
      case 'refresh': later('devicesBusy', {}, 0); later('devices', this.devices, 700); break;
      case 'saveSettings': this.settings = extra.settings; later('settings', this.settings, 0); break;
      case 'settingsFor': later('deviceSettings', { device: extra.device, settings: this.deviceSettings[extra.device] }, 250); break;
      case 'control': later('deviceSettings', { device: extra.device, settings: this.deviceSettings[extra.device] }, 300); later('controlDone', {}, 310); break;
      case 'setAudio': {
        const list = [...this.audio.outputs, ...this.audio.inputs];
        const flow = list.find(e => e.id === extra.id)?.flow;
        list.filter(e => e.flow === flow).forEach(e => { e.default = e.id === extra.id; if (this.settings.switchCalls) e.calls = e.default; });
        later('audio', this.audio, 150);
        break;
      }
    }
  },
};
