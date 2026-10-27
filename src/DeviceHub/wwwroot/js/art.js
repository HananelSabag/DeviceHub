// Drawings of the devices on this desk, made for DeviceHub (no product photos): line art in the page's ink,
// lit in each device's own colour. All share one 160 × 100 frame so they line up.

const svg = (body, label) =>
  `<svg class="art" viewBox="0 0 160 100" role="img" aria-label="${label}">${body}</svg>`;

/** Razer Basilisk X HyperSpeed, from above: the thumb rest on the left, two side buttons, the wheel lit. */
const mouse = () => svg(`
  <path class="body" d="M80 7C95 7 105 19 106 37C107 54 109 70 105 84C102 93 92 97 82 97C70 97 58 95 53 86C49 79 49 70 53 62C56 56 57 48 57 38C57 19 66 7 80 7Z"/>
  <path class="line" d="M80 8V41M58 43C70 47 92 47 105 43"/>
  <rect class="glow" x="76" y="15" width="8" height="15" rx="4"/>
  <rect class="line" x="77" y="33" width="6" height="4" rx="1.6"/>
  <path class="thick" d="M55.5 49L53 57M53.5 61L51.5 68"/>
  <circle class="line" cx="83" cy="79" r="4.5"/><circle class="glow" cx="83" cy="79" r="1.6"/>`, 'Basilisk X HyperSpeed');

/** SteelSeries Arctis 7+, from the front: the steel band, the ski-goggle strap, cushions and the mic. */
const headset = () => svg(`
  <path class="thick" d="M34 60C34 17 126 17 126 60"/>
  <path class="glow-line" d="M43 61C45 32 115 32 117 61"/>
  <rect class="body" x="22" y="52" width="25" height="40" rx="11"/>
  <rect class="body" x="113" y="52" width="25" height="40" rx="11"/>
  <rect class="soft" x="40" y="56" width="10" height="32" rx="5"/>
  <rect class="soft" x="110" y="56" width="10" height="32" rx="5"/>
  <circle class="line" cx="34" cy="72" r="6"/><circle class="line" cx="126" cy="72" r="6"/>
  <path class="line" d="M28 89C24 97 40 99 53 94"/><circle class="glow" cx="55" cy="93.5" r="2.6"/>`, 'Arctis 7+');

/** SteelSeries Apex 5: a full-size board with its OLED top right and the magnetic wrist rest. */
function keyboard() {
  const keys = [];
  const row = (y, x0, x1, step, h = 5.2) => { for (let x = x0; x + step - 1.2 <= x1; x += step) keys.push(`<rect x="${x}" y="${y}" width="${step - 1.4}" height="${h}" rx="1"/>`); };
  row(28, 13, 100, 6.6, 4); // function row
  for (let r = 0; r < 5; r++) row(35 + r * 6.6, 13, 107, 6.6);
  for (let r = 0; r < 2; r++) row(35 + r * 6.6, 111, 130, 6.6);
  row(61.4, 117.6, 124, 6.6); row(68, 111, 130, 6.6);
  for (let r = 0; r < 5; r++) row(35 + r * 6.6, 133, 152, 6.6);
  return svg(`
    <rect class="body" x="8" y="22" width="148" height="55" rx="5"/>
    <g class="keys">${keys.join('')}</g>
    <rect class="screen" x="120" y="25.5" width="29" height="7" rx="1.2"/>
    <path class="glow-line" d="M123 29H131M133 29H139" />
    <rect class="soft-line" x="8" y="81" width="148" height="13" rx="6.5"/>`, 'Apex 5');
}

/** DualSense Edge, from the front: the touchpad with its light, two sticks, the cross and the face buttons. */
const controller = () => svg(`
  <path class="body" d="M44 19C53 13 107 13 116 19C130 21 140 35 146 57C152 79 146 94 134 92C124 90 116 76 108 70H52C44 76 36 90 26 92C14 94 8 79 14 57C20 35 30 21 44 19Z"/>
  <rect class="line" x="60" y="19" width="40" height="25" rx="4"/>
  <path class="glow-line" d="M56 22C56 33 57 41 60 46M104 22C104 33 103 41 100 46"/>
  <circle class="line" cx="62" cy="58" r="7.5"/><circle class="soft" cx="62" cy="58" r="3.5"/>
  <circle class="line" cx="98" cy="58" r="7.5"/><circle class="soft" cx="98" cy="58" r="3.5"/>
  <path class="line" d="M35 33h6v6h6v6h-6v6h-6v-6h-6v-6h6Z"/>
  <circle class="line" cx="123" cy="32" r="3.2"/><circle class="line" cx="131" cy="40" r="3.2"/><circle class="line" cx="123" cy="48" r="3.2"/><circle class="line" cx="115" cy="40" r="3.2"/>
  <circle class="glow" cx="80" cy="57" r="2.4"/>`, 'DualSense Edge');

/** The laptop, open, its screen warm with the heat it reports. */
const laptop = () => svg(`
  <rect class="body" x="36" y="10" width="88" height="60" rx="5"/>
  <rect class="screen-warm" x="41" y="15" width="78" height="50" rx="2"/>
  <path class="glow-line" d="M52 50C60 36 66 56 74 42C82 28 88 50 96 38C102 30 106 40 110 34"/>
  <path class="body" d="M24 72H136L148 86C149 88 148 90 145 90H15C12 90 11 88 12 86Z"/>
  <path class="line" d="M64 84H96"/>`, 'Laptop');

export const ART = { mouse, headset, keyboard, controller, laptop };
export const art = id => (ART[id] ?? (() => ''))();
