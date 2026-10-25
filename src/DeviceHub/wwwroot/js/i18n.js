// Interface strings in both languages. Sound device names come from the C# side already in the chosen language.

const STRINGS = {
  he: {
    'nav.overview': 'סקירה', 'nav.devices': 'מכשירים', 'nav.sound': 'צליל', 'nav.settings': 'הגדרות',
    'kind.mouse': 'עכבר', 'kind.headset': 'אוזניות', 'kind.keyboard': 'מקלדת', 'kind.controller': 'שלט', 'kind.laptop': 'לפטופ',

    'ov.sub': 'עודכן ב-{time}', 'ov.reading': 'קורא את המכשירים…', 'ov.refresh': 'קרא שוב', 'ov.power': 'סוללות וחום', 'ov.sound': 'צליל עכשיו',

    'st.missing': 'לא מחובר', 'st.asleep.mouse': 'רדום', 'st.asleep.headset': 'כבויות', 'st.limited': 'בלוטות\'', 'st.error': 'לא נקרא',
    'st.charging': 'בטעינה', 'st.full': 'מלא', 'st.connected': 'מחובר', 'st.reading': 'קורא…',
    'note.missing.mouse': 'המקלט של Razer לא מחובר.', 'note.missing.headset': 'הדונגל של האוזניות לא מחובר.',
    'note.missing.keyboard': 'המקלדת לא מחוברת.', 'note.missing.controller': 'חבר את השלט בכבל USB או בבלוטות\'.',
    'note.missing.laptop': 'הרכיב של היצרן לא נמצא (InsydeDCHU).',
    'note.asleep.mouse': 'המקלט מחובר. הזז את העכבר כדי להעיר אותו.', 'note.asleep.headset': 'הדונגל מחובר. הדלק את האוזניות.',
    'note.limited': 'בבלוטות\' השלט מדווח סוללה רק אחרי שמשחק מעיר אותו. בכבל USB תמיד.',
    'note.error': 'לא הצלחתי לקרוא: {msg}',
    'link.usb': 'USB', 'link.bluetooth': 'בלוטות\'', 'link.dongle': 'מקלט אלחוטי',
    'fact.chatmix': 'ChatMix: משחק {g}% · צ\'אט {c}%', 'fact.fan': 'מאוורר {n}%', 'fact.own': 'התאורה של המקלדת', 'fact.color': 'צבע מ-DeviceHub',

    'm.precision': 'דיוק', 'm.stages': 'שלבי DPI', 'm.stages.sub': 'הכפתור שמתחת לגלגלת עובר ביניהם. בחר שלב כדי לערוך אותו; השלב שבחרת נשמר כפעיל.',
    'm.stage': 'שלב {n}', 'm.active': 'פעיל', 'm.remove': 'הסר שלב', 'm.save': 'שמור בעכבר',
    'm.polling': 'קצב דיווח', 'm.polling.sub': 'כמה פעמים בשנייה העכבר מדווח. 1000 Hz הכי חלק, 125 חוסך סוללה.',
    'm.power': 'סוללה', 'm.sleep': 'שינה אחרי', 'm.sleep.sub': 'כמה זמן בלי תנועה עד שהעכבר נרדם.',
    'm.low': 'התראת סוללה חלשה', 'm.low.sub': 'מתחת לרמה הזאת העכבר מהבהב.', 'min': '{n} דק\'',
    'm.asleep': 'העכבר רדום', 'm.asleep.sub': 'הזז אותו כדי להעיר אותו, ואז קרא שוב.', 'retry': 'קרא שוב',

    'h.voice': 'קול', 'h.sidetone': 'שמיעה עצמית', 'h.sidetone.sub': 'כמה מהקול שלך חוזר לאוזניות כשאתה מדבר.',
    'h.side.0': 'כבוי', 'h.side.1': 'חלש', 'h.side.2': 'בינוני', 'h.side.3': 'חזק',
    'h.autoOff': 'כיבוי אוטומטי', 'h.autoOff.sub': 'אחרי כמה דקות בלי שימוש האוזניות נכבות.', 'h.never': 'אף פעם',
    'h.eq': 'אקולייזר', 'h.eq.sub': 'עשרה תחומים, כל אחד עד 12 dB הגברה או הנחתה. הזז ולחץ "החל"; ערכה מוכנה נשמרת מיד.',
    'h.eq.flat': 'שטוח', 'h.eq.bass': 'בס', 'h.eq.smiley': 'סמיילי', 'h.eq.focus': 'פוקוס', 'h.eq.custom': 'אישי',
    'h.apply': 'החל', 'h.reset': 'בטל שינויים',
    'h.unknown': 'עוד לא נקבע מ-DeviceHub',
    'h.note': 'האוזניות לא מדווחות את ההגדרות האלה בחזרה, אז DeviceHub מציג את מה שהוא קבע לאחרונה. כל שינוי נשמר באוזניות עצמן.',
    'h.off': 'האוזניות כבויות', 'h.off.sub': 'הדלק אותן כדי לשנות הגדרות. ההגדרות עוברות דרך הדונגל.',

    'k.lighting': 'תאורה', 'k.mode': 'מקור התאורה', 'k.mode.sub': 'התאורה השמורה במקלדת, או צבע ש-DeviceHub שולח.',
    'k.own': 'של המקלדת', 'k.color': 'צבע מ-DeviceHub',
    'k.main': 'צבע המקשים', 'k.accent': 'הדגשה', 'k.accent.sub': 'צבע נפרד ל-W A S D ולחצים.',
    'k.brightness': 'עוצמה',
    'k.note': 'הצבעים נשארים עד שהמקלדת מאבדת חשמל, ו-DeviceHub שולח אותם שוב כשהוא רואה אותה. הפרופילים השמורים במקלדת לא משתנים.',

    'c.lights': 'אורות', 'c.lightbar': 'פס האור', 'c.brightness': 'עוצמה', 'c.players': 'נורות השחקן', 'c.players.sub': 'חמש הנקודות מתחת למשטח המגע.',
    'c.mic': 'נורת המיקרופון', 'c.mic.sub': 'הנורה בכפתור ההשתקה.', 'c.off': 'כבוי',
    'c.note': 'בכבל USB בלבד. השלט שוכח את האורות כשהוא מתנתק, ו-DeviceHub שולח אותם שוב כשהוא חוזר.',
    'c.bt': 'השלט מחובר בבלוטות\'', 'c.bt.sub': 'כדי לשנות אורות חבר אותו בכבל USB.',

    'l.temp': 'טמפרטורה', 'l.fan': 'מאוורר', 'l.history': 'הדקות האחרונות', 'l.now': 'עכשיו', 'l.ago': 'לפני {n} דק\'',
    'l.note': 'קריאה בלבד. השליטה במאווררים נשארת ב-Control Center של היצרן.',

    's.title': 'צליל', 's.sub': 'לחיצה על מכשיר מעבירה אליו את הצליל', 's.out': 'יציאת שמע', 's.in': 'מיקרופון',
    's.now': 'פעיל', 's.calls': 'שיחות', 's.away': 'לא מחובר', 's.none': 'לא נמצאו מכשירים.',
    's.switched': 'הצליל עבר ל-{name}', 's.switchedIn': 'המיקרופון עכשיו: {name}',
    'set.title': 'הגדרות', 'set.language': 'שפה', 'set.calls': 'גם השיחות עוברות', 'set.calls.sub': 'החלפת מכשיר שמע מעבירה גם את Discord, Teams ושאר אפליקציות השיחה.',
    'set.about': 'DeviceHub {v}. קורא את המכשירים בלי דרייברים ובלי שירותים ברקע; כל שינוי נבדק מול המכשיר.',

    'saved': 'נשמר', 'error': 'לא נשמר: {msg}',
  },
  en: {
    'nav.overview': 'Overview', 'nav.devices': 'Devices', 'nav.sound': 'Sound', 'nav.settings': 'Settings',
    'kind.mouse': 'Mouse', 'kind.headset': 'Headset', 'kind.keyboard': 'Keyboard', 'kind.controller': 'Controller', 'kind.laptop': 'Laptop',

    'ov.sub': 'Updated {time}', 'ov.reading': 'Reading the devices…', 'ov.refresh': 'Read again', 'ov.power': 'Batteries and heat', 'ov.sound': 'Sound now',

    'st.missing': 'Not connected', 'st.asleep.mouse': 'Asleep', 'st.asleep.headset': 'Off', 'st.limited': 'Bluetooth', 'st.error': 'Not read',
    'st.charging': 'Charging', 'st.full': 'Full', 'st.connected': 'Connected', 'st.reading': 'Reading…',
    'note.missing.mouse': 'The Razer receiver isn\'t plugged in.', 'note.missing.headset': 'The headset\'s dongle isn\'t plugged in.',
    'note.missing.keyboard': 'The keyboard isn\'t plugged in.', 'note.missing.controller': 'Connect the controller by USB or Bluetooth.',
    'note.missing.laptop': 'The maker\'s sensor component (InsydeDCHU) wasn\'t found.',
    'note.asleep.mouse': 'The receiver is in. Move the mouse to wake it.', 'note.asleep.headset': 'The dongle is in. Switch the headset on.',
    'note.limited': 'Over Bluetooth the controller reports its battery only once a game wakes it up. Over USB, always.',
    'note.error': 'Couldn\'t read it: {msg}',
    'link.usb': 'USB', 'link.bluetooth': 'Bluetooth', 'link.dongle': 'Wireless receiver',
    'fact.chatmix': 'ChatMix: game {g}% · chat {c}%', 'fact.fan': 'Fan {n}%', 'fact.own': 'Keyboard\'s own lighting', 'fact.color': 'DeviceHub colour',

    'm.precision': 'Precision', 'm.stages': 'DPI stages', 'm.stages.sub': 'The button under the wheel steps through them. Pick a stage to edit it; the one you pick is saved as active.',
    'm.stage': 'Stage {n}', 'm.active': 'Active', 'm.remove': 'Remove stage', 'm.save': 'Save to mouse',
    'm.polling': 'Polling rate', 'm.polling.sub': 'How often the mouse reports each second. 1000 Hz is smoothest, 125 saves battery.',
    'm.power': 'Battery', 'm.sleep': 'Sleep after', 'm.sleep.sub': 'How long without movement before the mouse sleeps.',
    'm.low': 'Low battery warning', 'm.low.sub': 'Below this level the mouse blinks.', 'min': '{n} min',
    'm.asleep': 'The mouse is asleep', 'm.asleep.sub': 'Move it to wake it, then read again.', 'retry': 'Read again',

    'h.voice': 'Voice', 'h.sidetone': 'Sidetone', 'h.sidetone.sub': 'How much of your own voice you hear while you talk.',
    'h.side.0': 'Off', 'h.side.1': 'Low', 'h.side.2': 'Medium', 'h.side.3': 'High',
    'h.autoOff': 'Auto-off', 'h.autoOff.sub': 'Minutes without use before the headset switches off.', 'h.never': 'Never',
    'h.eq': 'Equalizer', 'h.eq.sub': 'Ten bands, -12 to +12 dB. Move them and press Apply; a preset is saved straight away.',
    'h.eq.flat': 'Flat', 'h.eq.bass': 'Bass', 'h.eq.smiley': 'Smiley', 'h.eq.focus': 'Focus', 'h.eq.custom': 'Custom',
    'h.apply': 'Apply', 'h.reset': 'Undo changes',
    'h.unknown': 'Not set from DeviceHub yet',
    'h.note': 'The headset doesn\'t report these back, so DeviceHub shows what it last set. Every change is saved in the headset itself.',
    'h.off': 'The headset is off', 'h.off.sub': 'Switch it on to change settings; they travel through the dongle.',

    'k.lighting': 'Lighting', 'k.mode': 'Lighting source', 'k.mode.sub': 'The lighting saved in the keyboard, or a colour DeviceHub sends.',
    'k.own': 'Keyboard\'s own', 'k.color': 'DeviceHub colour',
    'k.main': 'Key colour', 'k.accent': 'Accent', 'k.accent.sub': 'A separate colour for W A S D and the arrows.',
    'k.brightness': 'Brightness',
    'k.note': 'Colours stay until the keyboard loses power, and DeviceHub sends them again when it sees it. Profiles saved in the keyboard are not changed.',

    'c.lights': 'Lights', 'c.lightbar': 'Lightbar', 'c.brightness': 'Brightness', 'c.players': 'Player lights', 'c.players.sub': 'The five dots under the touchpad.',
    'c.mic': 'Mic light', 'c.mic.sub': 'The light in the mute button.', 'c.off': 'Off',
    'c.note': 'USB only. The controller forgets its lights when it disconnects, and DeviceHub sends them again when it comes back.',
    'c.bt': 'The controller is on Bluetooth', 'c.bt.sub': 'Connect it with a USB cable to change its lights.',

    'l.temp': 'Temperature', 'l.fan': 'Fan', 'l.history': 'The last few minutes', 'l.now': 'Now', 'l.ago': '{n} min ago',
    'l.note': 'Read only. Fan control stays in the maker\'s Control Center.',

    's.title': 'Sound', 's.sub': 'Press a device to send the sound there', 's.out': 'Output', 's.in': 'Microphone',
    's.now': 'In use', 's.calls': 'Calls', 's.away': 'Not connected', 's.none': 'No devices found.',
    's.switched': 'Sound now plays on {name}', 's.switchedIn': 'Microphone is now {name}',
    'set.title': 'Settings', 'set.language': 'Language', 'set.calls': 'Calls follow too', 'set.calls.sub': 'Switching the sound device also moves Discord, Teams and other call apps.',
    'set.about': 'DeviceHub {v}. Reads your devices without drivers or background services; every change is checked against the device.',

    'saved': 'Saved', 'error': 'Not saved: {msg}',
  },
};

let lang = 'he';

export const getLang = () => lang;

export function setLang(next) {
  lang = next === 'en' ? 'en' : 'he';
  document.documentElement.lang = lang;
  document.documentElement.dir = lang === 'en' ? 'ltr' : 'rtl';
}

export function t(key, vars = {}) {
  const s = STRINGS[lang][key] ?? STRINGS.he[key] ?? key;
  return s.replace(/\{(\w+)\}/g, (_, k) => vars[k] ?? '');
}

export const locale = () => (lang === 'en' ? 'en-GB' : 'he-IL');
