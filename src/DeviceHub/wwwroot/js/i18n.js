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
    'k.note': 'אפקטים שזזים והמסך רצים כל עוד DeviceHub פתוח או במגש, ונשלחים שוב כשהמקלדת מתחברת. תמונה קבועה זזה פיקסל כל שתי דקות כדי לא להישרף במסך. הפרופילים השמורים במקלדת לא משתנים.',

    'c.lights': 'אורות', 'c.lightbar': 'פס האור', 'c.brightness': 'עוצמה', 'c.players': 'נורות השחקן', 'c.players.sub': 'חמש הנקודות מתחת למשטח המגע.',
    'c.mic': 'נורת המיקרופון', 'c.mic.sub': 'הנורה בכפתור ההשתקה.', 'c.off': 'כבוי',
    'c.note': 'בכבל USB בלבד. השלט שוכח את האורות כשהוא מתנתק, ו-DeviceHub שולח אותם שוב כשהוא חוזר.',
    'c.bt': 'השלט מחובר בבלוטות\'', 'c.bt.sub': 'כדי לשנות אורות חבר אותו בכבל USB.',

    'l.temp': 'טמפרטורה', 'l.fan': 'מאוורר', 'l.history': 'הדקות האחרונות', 'l.now': 'עכשיו', 'l.ago': 'לפני {n} דק\'',
    'l.note': 'הכל כאן קריאה, חוץ ממצב צריכת החשמל של Windows. השליטה במאווררים נשארת ב-Control Center של היצרן.',

    's.title': 'צליל', 's.sub': 'לחיצה על מכשיר מעבירה אליו את הצליל', 's.out': 'יציאת שמע', 's.in': 'מיקרופון',
    's.now': 'פעיל', 's.calls': 'שיחות', 's.away': 'לא מחובר', 's.none': 'לא נמצאו מכשירים.',
    's.switched': 'הצליל עבר ל-{name}', 's.switchedIn': 'המיקרופון עכשיו: {name}',
    'set.title': 'הגדרות', 'set.language': 'שפה', 'set.calls': 'גם השיחות עוברות', 'set.calls.sub': 'החלפת מכשיר שמע מעבירה גם את Discord, Teams ושאר אפליקציות השיחה.',
    'set.about': 'DeviceHub {v}. קורא את המכשירים בלי דרייברים ובלי שירותים ברקע; כל שינוי נבדק מול המכשיר.',

    'ov.screen': 'המסך במקלדת', 'ov.screenEdit': 'לשנות את המסך', 'scr.offNote': 'DeviceHub לא מציג כלום במסך',
    'k.control': 'DeviceHub שולט בתאורה', 'k.control.on': 'האפקט שבחרת רץ על המקלדת.', 'k.control.off': 'כבוי: המקלדת מציגה את התאורה השמורה בה.',
    'k.ownNote': 'התאורה השמורה במקלדת', 'k.from': 'צבע התחלה', 'k.to': 'צבע סיום', 'k.speed': 'מהירות',
    'fx.static': 'סטטי', 'fx.static.sub': 'צבע אחד, והדגשה ל-WASD', 'fx.breathing': 'נשימה', 'fx.breathing.sub': 'מתעמעם ומתבהר לאט',
    'fx.wave': 'גל', 'fx.wave.sub': 'קשת שזזה לאורך המקלדת', 'fx.spectrum': 'ספקטרום', 'fx.spectrum.sub': 'הכל עובר בין הצבעים',
    'fx.gradient': 'מעבר צבע', 'fx.gradient.sub': 'בין שני צבעים, מקצה לקצה', 'fx.music': 'מוזיקה', 'fx.music.sub': 'המקשים פועמים עם הצליל',
    'scr.title': 'מסך OLED', 'scr.off': 'כבוי', 'scr.off.sub': 'מסך שחור', 'scr.clock': 'שעון', 'scr.clock.sub': 'שעה, יום ותאריך',
    'scr.devices': 'סוללות', 'scr.devices.sub': 'עכבר, אוזניות, שלט, חום', 'scr.music': 'מה מתנגן', 'scr.music.sub': 'השיר מ-Spotify או מהדפדפן',
    'scr.cycle': 'סבב', 'scr.cycle.sub': 'שעון, סוללות ושיר לסירוגין', 'scr.text': 'טקסט', 'scr.text.sub': 'מה שתכתוב', 'scr.image': 'תמונה', 'scr.image.sub': 'תמונה או GIF מהמחשב',
    'scr.stars': 'מהירות האור', 'scr.stars.sub': 'כוכבים טסים אליך', 'scr.matrix': 'מטריקס', 'scr.matrix.sub': 'קוד נופל',
    'scr.energy': 'כדור אנרגיה', 'scr.energy.sub': 'טעינה, קרן, פיצוץ', 'scr.sound': 'פסי מוזיקה', 'scr.sound.sub': 'זזים עם הצליל', 'scr.goal': 'גול!', 'scr.goal.sub': 'כדור בבוסט אל השער',
    'scr.show': 'הצג', 'scr.picture': 'התמונה', 'scr.picture.sub': 'PNG, JPG או GIF מונפש. מוקטנת ומומרת לשחור-לבן.',
    'scr.pictureChoose': 'בחר תמונה', 'scr.pictureChange': 'החלף תמונה', 'scr.reading': 'ממיר את התמונה…',
    'set.background': 'ברקע', 'set.tray': 'להישאר במגש כשסוגרים', 'set.tray.sub': 'כך האפקטים והמסך במקלדת ממשיכים. יציאה מלאה מהתפריט של האייקון במגש.',
    'set.startup': 'הפעלה עם Windows', 'set.startup.sub': 'נפתח בשקט במגש, בלי חלון.',
    'set.alerts': 'התראת סוללה חלשה', 'set.alerts.sub': 'הודעה כשהעכבר, האוזניות או השלט יורדים מתחת ל-15%.',

    'fx.reactive': 'לחיצה', 'fx.reactive.sub': 'המקש נדלק כשלוחצים עליו', 'fx.ripple': 'אדווה', 'fx.ripple.sub': 'גל שמתפשט מהמקש',
    'k.press': 'צבע הלחיצה', 'k.background': 'צבע הרקע',
    'scr.system': 'מחשב', 'scr.system.sub': 'CPU ו-GPU: חום ועומס', 'scr.show': 'מופע', 'scr.show.sub': 'כל האנימציות, עם מעברים',
    'scr.power': 'עלייה בכוח', 'scr.power.sub': 'הילה, מונה כוח, פיצוץ',
    'h.profiles': 'פרופילים', 'h.profiles.apply': 'החל פרופיל', 'h.profiles.sub': 'אקולייזר, שמיעה עצמית וכיבוי אוטומטי בלחיצה אחת.',
    'h.profile.none': 'עוד אין פרופילים. שמור את ההגדרות הנוכחיות כאן למטה.', 'h.profile.save': 'שמור את ההגדרות הנוכחיות',
    'h.profile.save.sub': 'בשם שתבחר, למשל "משחקים" או "מוזיקה". אותו שם מחליף את הקודם.', 'h.profile.placeholder': 'שם הפרופיל',
    'h.profile.saveBtn': 'שמור', 'h.profile.delete': 'מחק את הפרופיל', 'h.profile.applied': 'הפרופיל {name} הוחל',
    'l.cpu': 'מעבד', 'l.gpu': 'כרטיס מסך', 'l.memory': 'זיכרון', 'l.battery': 'סוללת הלפטופ', 'l.load': 'עומס {n}%', 'l.speed': 'מהירות {n}%',
    'l.of': 'מתוך {n} GB', 'l.noBattery': 'אין סוללה', 'l.charging': 'בטעינה', 'l.onCharger': 'מחובר לחשמל', 'l.onBattery': 'על סוללה, {w}W',
    'l.fans': 'מאווררים', 'l.fanCpu': 'המעבד', 'l.fanGpu': 'כרטיס המסך',
    'l.mode': 'מצב צריכת חשמל', 'l.mode.sub': 'ההגדרה של Windows: חיסכון, מאוזן או ביצועים מרביים.',
    'l.mode.efficiency': 'חיסכון', 'l.mode.balanced': 'מאוזן', 'l.mode.performance': 'ביצועים',

    'fx.aurora': 'זוהר', 'fx.aurora.sub': 'שני צבעים זורמים בגלים', 'fx.starlight': 'כוכבים', 'fx.starlight.sub': 'מקשים מנצנצים אחד אחד',
    'fx.rain': 'גשם', 'fx.rain.sub': 'טיפות יורדות בעמודות', 'fx.fire': 'אש', 'fx.fire.sub': 'להבות עולות מהשורה התחתונה',
    'fx.visualizer': 'מד צליל', 'fx.visualizer.sub': 'עמודות עולות עם הצליל', 'fx.temperature': 'חום המעבד', 'fx.temperature.sub': 'כחול כשקר, אדום כשחם',
    'scr.analog': 'שעון מחוגים', 'scr.analog.sub': 'שעון עגול, שעה ויום', 'scr.fireworks': 'זיקוקים', 'scr.fireworks.sub': 'טילים ופיצוצים',
    'scr.life': 'משחק החיים', 'scr.life.sub': 'האוטומט של קונוויי', 'scr.bounce': 'לוגו קופץ', 'scr.bounce.sub': 'שומר המסך של ה-DVD',
    'c.mode': 'מצב פס האור', 'c.mode.static': 'קבוע', 'c.mode.breathing': 'נשימה', 'c.mode.rainbow': 'קשת', 'c.mode.battery': 'סוללה',
    'c.mode.static.sub': 'הצבע שבחרת.', 'c.mode.breathing.sub': 'הצבע שבחרת, מתעמעם ומתבהר. עוצר כשמשחק במסך מלא פתוח.',
    'c.mode.rainbow.sub': 'כל הצבעים, לאט. עוצר כשמשחק במסך מלא פתוח.', 'c.mode.battery.sub': 'ירוק, צהוב מתחת לחצי, אדום מתחת ל-20%.',
    'set.minimize': 'מזעור למגש', 'set.minimize.sub': 'כפתור המזעור שולח את DeviceHub לאייקון ליד השעון, כמו אבטחת Windows.',
    'hl.title': 'מצב DeviceHub', 'hl.running': 'פועל כבר', 'hl.hours': '{n} שעות', 'hl.cost': 'עלות', 'hl.costValue': '{cpu}% מעבד בממוצע, {mb} MB זיכרון',
    'hl.frames': 'נשלח עד עכשיו', 'hl.framesValue': '{k} תמונות למקלדת, {s} למסך, {p} לשלט', 'hl.keyboard': 'המקלדת מחוברת', 'hl.yes': 'כן', 'hl.no': 'לא',
    'hl.errors': 'שגיאה אחרונה', 'hl.none': 'אין', 'hl.note': 'הכל נרשם גם ביומן: מתי DeviceHub נפתח ונסגר, ולמה.', 'hl.log': 'פתח את היומן',

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
    'k.note': 'Moving effects and the screen run while DeviceHub is open or in the tray, and are sent again when the keyboard is plugged in. A still picture shifts a pixel every two minutes so it never burns in. Profiles saved in the keyboard are not changed.',

    'c.lights': 'Lights', 'c.lightbar': 'Lightbar', 'c.brightness': 'Brightness', 'c.players': 'Player lights', 'c.players.sub': 'The five dots under the touchpad.',
    'c.mic': 'Mic light', 'c.mic.sub': 'The light in the mute button.', 'c.off': 'Off',
    'c.note': 'USB only. The controller forgets its lights when it disconnects, and DeviceHub sends them again when it comes back.',
    'c.bt': 'The controller is on Bluetooth', 'c.bt.sub': 'Connect it with a USB cable to change its lights.',

    'l.temp': 'Temperature', 'l.fan': 'Fan', 'l.history': 'The last few minutes', 'l.now': 'Now', 'l.ago': '{n} min ago',
    'l.note': 'Everything here is read only, except Windows\' power mode. Fan control stays in the maker\'s Control Center.',

    's.title': 'Sound', 's.sub': 'Press a device to send the sound there', 's.out': 'Output', 's.in': 'Microphone',
    's.now': 'In use', 's.calls': 'Calls', 's.away': 'Not connected', 's.none': 'No devices found.',
    's.switched': 'Sound now plays on {name}', 's.switchedIn': 'Microphone is now {name}',
    'set.title': 'Settings', 'set.language': 'Language', 'set.calls': 'Calls follow too', 'set.calls.sub': 'Switching the sound device also moves Discord, Teams and other call apps.',
    'set.about': 'DeviceHub {v}. Reads your devices without drivers or background services; every change is checked against the device.',

    'ov.screen': 'Keyboard screen', 'ov.screenEdit': 'Change the screen', 'scr.offNote': 'DeviceHub isn\'t using the screen',
    'k.control': 'DeviceHub controls the lighting', 'k.control.on': 'Your effect runs on the keyboard.', 'k.control.off': 'Off: the keyboard shows the lighting saved in it.',
    'k.ownNote': 'The keyboard\'s own lighting', 'k.from': 'From', 'k.to': 'To', 'k.speed': 'Speed',
    'fx.static': 'Static', 'fx.static.sub': 'One colour, WASD picked out', 'fx.breathing': 'Breathing', 'fx.breathing.sub': 'Dims and brightens slowly',
    'fx.wave': 'Wave', 'fx.wave.sub': 'A rainbow moving across', 'fx.spectrum': 'Spectrum', 'fx.spectrum.sub': 'Everything cycles colours',
    'fx.gradient': 'Gradient', 'fx.gradient.sub': 'Two colours, end to end', 'fx.music': 'Music', 'fx.music.sub': 'Keys pulse with the sound',
    'scr.title': 'OLED screen', 'scr.off': 'Off', 'scr.off.sub': 'A dark screen', 'scr.clock': 'Clock', 'scr.clock.sub': 'Time, day and date',
    'scr.devices': 'Batteries', 'scr.devices.sub': 'Mouse, headset, pad, heat', 'scr.music': 'Now playing', 'scr.music.sub': 'The song from Spotify or a browser',
    'scr.cycle': 'Rotation', 'scr.cycle.sub': 'Clock, batteries and song in turn', 'scr.text': 'Text', 'scr.text.sub': 'Your own words', 'scr.image': 'Picture', 'scr.image.sub': 'An image or GIF from your PC',
    'scr.stars': 'Warp speed', 'scr.stars.sub': 'Stars flying at you', 'scr.matrix': 'Matrix', 'scr.matrix.sub': 'Falling code',
    'scr.energy': 'Energy ball', 'scr.energy.sub': 'Charge, beam, blast', 'scr.sound': 'Music bars', 'scr.sound.sub': 'Moving with the sound', 'scr.goal': 'Goal!', 'scr.goal.sub': 'A boosting ball into the net',
    'scr.show': 'Show', 'scr.picture': 'The picture', 'scr.picture.sub': 'PNG, JPG or an animated GIF. Shrunk and turned black and white.',
    'scr.pictureChoose': 'Choose a picture', 'scr.pictureChange': 'Change the picture', 'scr.reading': 'Converting the picture…',
    'set.background': 'In the background', 'set.tray': 'Stay in the tray when closed', 'set.tray.sub': 'So the keyboard\'s effects and screen keep going. Exit fully from the tray icon\'s menu.',
    'set.startup': 'Start with Windows', 'set.startup.sub': 'Starts quietly in the tray, without a window.',
    'set.alerts': 'Low battery alerts', 'set.alerts.sub': 'A note when the mouse, headset or controller drops below 15%.',

    'fx.reactive': 'Key press', 'fx.reactive.sub': 'A key lights up as you press it', 'fx.ripple': 'Ripple', 'fx.ripple.sub': 'A wave spreading from the key',
    'k.press': 'Press colour', 'k.background': 'Background',
    'scr.system': 'System', 'scr.system.sub': 'CPU and GPU: heat and load', 'scr.show': 'Show', 'scr.show.sub': 'Every animation, with transitions',
    'scr.power': 'Power up', 'scr.power.sub': 'Aura, power reading, blast',
    'h.profiles': 'Profiles', 'h.profiles.apply': 'Apply a profile', 'h.profiles.sub': 'Equalizer, sidetone and auto-off in one press.',
    'h.profile.none': 'No profiles yet. Save the current settings below.', 'h.profile.save': 'Save the current settings',
    'h.profile.save.sub': 'Under a name you choose, like "Games" or "Music". The same name replaces the old one.', 'h.profile.placeholder': 'Profile name',
    'h.profile.saveBtn': 'Save', 'h.profile.delete': 'Delete the profile', 'h.profile.applied': 'Profile {name} applied',
    'l.cpu': 'CPU', 'l.gpu': 'GPU', 'l.memory': 'Memory', 'l.battery': 'Laptop battery', 'l.load': 'Load {n}%', 'l.speed': 'Speed {n}%',
    'l.of': 'of {n} GB', 'l.noBattery': 'No battery', 'l.charging': 'Charging', 'l.onCharger': 'On the charger', 'l.onBattery': 'On battery, {w} W',
    'l.fans': 'Fans', 'l.fanCpu': 'CPU', 'l.fanGpu': 'GPU',
    'l.mode': 'Power mode', 'l.mode.sub': 'Windows\' setting: power saving, balanced or best performance.',
    'l.mode.efficiency': 'Saving', 'l.mode.balanced': 'Balanced', 'l.mode.performance': 'Performance',

    'fx.aurora': 'Aurora', 'fx.aurora.sub': 'Two colours flowing in waves', 'fx.starlight': 'Starlight', 'fx.starlight.sub': 'Keys glint one by one',
    'fx.rain': 'Rain', 'fx.rain.sub': 'Drops falling down the columns', 'fx.fire': 'Fire', 'fx.fire.sub': 'Flames rising from the bottom row',
    'fx.visualizer': 'Sound meter', 'fx.visualizer.sub': 'Columns rising with the sound', 'fx.temperature': 'CPU heat', 'fx.temperature.sub': 'Blue when cool, red when hot',
    'scr.analog': 'Analog clock', 'scr.analog.sub': 'A clock face, time and day', 'scr.fireworks': 'Fireworks', 'scr.fireworks.sub': 'Rockets and bursts',
    'scr.life': 'Game of Life', 'scr.life.sub': 'Conway\'s automaton', 'scr.bounce': 'Bouncing logo', 'scr.bounce.sub': 'The DVD screensaver',
    'c.mode': 'Lightbar mode', 'c.mode.static': 'Still', 'c.mode.breathing': 'Breathing', 'c.mode.rainbow': 'Rainbow', 'c.mode.battery': 'Battery',
    'c.mode.static.sub': 'The colour you pick.', 'c.mode.breathing.sub': 'Your colour, dimming and brightening. Pauses while a full-screen game is open.',
    'c.mode.rainbow.sub': 'Every colour, slowly. Pauses while a full-screen game is open.', 'c.mode.battery.sub': 'Green, yellow below half, red below 20%.',
    'set.minimize': 'Minimize to the tray', 'set.minimize.sub': 'The minimize button sends DeviceHub to the icon by the clock, like Windows Security.',
    'hl.title': 'How DeviceHub is doing', 'hl.running': 'Running for', 'hl.hours': '{n} hours', 'hl.cost': 'Cost', 'hl.costValue': '{cpu}% CPU on average, {mb} MB memory',
    'hl.frames': 'Sent so far', 'hl.framesValue': '{k} frames to the keyboard, {s} to the screen, {p} to the controller', 'hl.keyboard': 'Keyboard connected', 'hl.yes': 'Yes', 'hl.no': 'No',
    'hl.errors': 'Last error', 'hl.none': 'None', 'hl.note': 'Everything is also written to a log: when DeviceHub started and stopped, and why.', 'hl.log': 'Open the log',

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
