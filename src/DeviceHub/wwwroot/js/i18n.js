// Interface strings in both languages. Sound device names come from the C# side already in the chosen language.

const STRINGS = {
  he: {
    'dev.title': 'המכשירים שלי', 'dev.updated': 'עודכן ב-{time}', 'dev.reading': 'קורא את המכשירים…', 'dev.refresh': 'קרא שוב',
    'kind.mouse': 'עכבר', 'kind.headset': 'אוזניות', 'kind.controller': 'שלט', 'kind.laptop': 'לפטופ',

    'st.charging': 'בטעינה', 'st.full': 'מלא', 'st.missing': 'לא מחובר', 'st.limited': 'בלוטות\'', 'st.error': 'לא נקרא',
    'st.asleep.mouse': 'רדום', 'st.asleep.headset': 'כבויות',
    'note.missing.mouse': 'המקלט של Razer לא מחובר.',
    'note.missing.headset': 'הדונגל של האוזניות לא מחובר.',
    'note.missing.controller': 'חבר את השלט בכבל USB או בבלוטות\'.',
    'note.missing.laptop': 'הרכיב של היצרן לא נמצא (InsydeDCHU).',
    'note.asleep.mouse': 'המקלט מחובר. הזז את העכבר כדי להעיר אותו.',
    'note.asleep.headset': 'הדונגל מחובר. הדלק את האוזניות.',
    'note.limited': 'בבלוטות\' השלט מדווח סוללה רק אחרי שמשחק מעיר אותו. בכבל USB תמיד.',
    'note.error': 'לא הצלחתי לקרוא: {msg}',
    'link.usb': 'USB', 'link.bluetooth': 'בלוטות\'', 'link.dongle': 'מקלט אלחוטי',
    'fact.game': 'משחק {n}%', 'fact.chat': 'צ\'אט {n}%', 'fact.chatmix': 'ChatMix: עוצמת המשחק והצ\'אט',
    'lap.fan': 'מאוורר', 'lap.note': 'קריאה בלבד, בלי שליטה במאווררים', 'lap.noteLong': 'השליטה במאווררים נשארת ב-Control Center של היצרן.',

    'snd.title': 'צליל', 'snd.meta': 'לחיצה על מכשיר מעבירה אליו את הצליל', 'snd.out': 'יציאת שמע', 'snd.in': 'מיקרופון',
    'snd.now': 'פועל עכשיו', 'snd.calls': 'שיחות', 'snd.away': 'לא מחובר', 'snd.none': 'לא נמצאו מכשירים.',
    'snd.switched': 'הצליל עבר ל-{name}', 'snd.switchedIn': 'המיקרופון עכשיו: {name}',

    'set.title': 'הגדרות', 'set.language': 'שפה', 'set.sounds': 'צלילי ממשק', 'set.sounds.sub': 'וגם רטט קטן בשלט',
    'set.fullscreen': 'פתיחה במסך מלא', 'set.fullscreen.sub': 'F11 מחליף בכל רגע',
    'set.calls': 'גם השיחות עוברות', 'set.calls.sub': 'החלפת מכשיר מעבירה גם את Discord, Teams ושאר אפליקציות השיחה',
    'set.close': 'סגור', 'set.about': 'DeviceHub {v}. קורא מהמכשירים ולא משנה בהם דבר; הדבר היחיד שהוא משנה הוא מכשיר השמע שבחרת.',

    'power.title': 'לצאת מ-DeviceHub?', 'power.cancel': 'ביטול', 'power.minimize': 'למזער', 'power.exit': 'יציאה',

    'hint.select': 'בחירה', 'hint.switch': 'העבר לכאן', 'hint.refresh': 'קרא שוב', 'hint.back': 'חזרה', 'hint.cancel': 'ביטול',
    'hint.full': 'מסך מלא', 'hint.settings': 'הגדרות',
    'pad.connected': 'שלט PlayStation מחובר', 'pad.connectedGeneric': 'שלט מחובר',
    'error': 'משהו נכשל: {msg}', 'preview': 'לא זמין בתצוגה בדפדפן', 'exit': 'יציאה', 'settings': 'הגדרות',
  },
  en: {
    'dev.title': 'My devices', 'dev.updated': 'Updated {time}', 'dev.reading': 'Reading the devices…', 'dev.refresh': 'Read again',
    'kind.mouse': 'Mouse', 'kind.headset': 'Headset', 'kind.controller': 'Controller', 'kind.laptop': 'Laptop',

    'st.charging': 'Charging', 'st.full': 'Full', 'st.missing': 'Not connected', 'st.limited': 'Bluetooth', 'st.error': 'Not read',
    'st.asleep.mouse': 'Asleep', 'st.asleep.headset': 'Off',
    'note.missing.mouse': 'The Razer receiver isn\'t plugged in.',
    'note.missing.headset': 'The headset\'s dongle isn\'t plugged in.',
    'note.missing.controller': 'Connect the controller by USB or Bluetooth.',
    'note.missing.laptop': 'The maker\'s sensor component (InsydeDCHU) wasn\'t found.',
    'note.asleep.mouse': 'The receiver is in. Move the mouse to wake it.',
    'note.asleep.headset': 'The dongle is in. Switch the headset on.',
    'note.limited': 'Over Bluetooth the controller reports its battery only once a game wakes it up. Over USB, always.',
    'note.error': 'Couldn\'t read it: {msg}',
    'link.usb': 'USB', 'link.bluetooth': 'Bluetooth', 'link.dongle': 'Wireless receiver',
    'fact.game': 'Game {n}%', 'fact.chat': 'Chat {n}%', 'fact.chatmix': 'ChatMix: game and chat volume',
    'lap.fan': 'Fan', 'lap.note': 'Read only, no fan control', 'lap.noteLong': 'Fan control stays in the maker\'s Control Center.',

    'snd.title': 'Sound', 'snd.meta': 'Press a device to send the sound there', 'snd.out': 'Output', 'snd.in': 'Microphone',
    'snd.now': 'In use', 'snd.calls': 'Calls', 'snd.away': 'Not connected', 'snd.none': 'No devices found.',
    'snd.switched': 'Sound now plays on {name}', 'snd.switchedIn': 'Microphone is now {name}',

    'set.title': 'Settings', 'set.language': 'Language', 'set.sounds': 'Interface sounds', 'set.sounds.sub': 'and a light rumble on the controller',
    'set.fullscreen': 'Start full screen', 'set.fullscreen.sub': 'F11 switches any time',
    'set.calls': 'Calls follow too', 'set.calls.sub': 'Switching also moves Discord, Teams and other call apps',
    'set.close': 'Close', 'set.about': 'DeviceHub {v}. It reads your devices and changes nothing on them; the one thing it changes is the sound device you pick.',

    'power.title': 'Exit DeviceHub?', 'power.cancel': 'Cancel', 'power.minimize': 'Minimize', 'power.exit': 'Exit',

    'hint.select': 'Select', 'hint.switch': 'Switch here', 'hint.refresh': 'Read again', 'hint.back': 'Back', 'hint.cancel': 'Cancel',
    'hint.full': 'Full screen', 'hint.settings': 'Settings',
    'pad.connected': 'PlayStation controller connected', 'pad.connectedGeneric': 'Controller connected',
    'error': 'Something failed: {msg}', 'preview': 'Not available in the browser preview', 'exit': 'Exit', 'settings': 'Settings',
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
