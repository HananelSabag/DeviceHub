// A small console dialog. While it's open the D-pad stays inside it and Back closes it.

import { $, esc, icon } from './ui.js';
import { focusFirst, focusEl, renderHints } from './nav.js';
import { sfx } from './sound.js';

let dialog = null;

/**
 * actions: [{ id, label, icon?, primary?, danger? }]; body: extra HTML above them (its controls carry data-act too).
 * onAction(id, button) may return false to keep the dialog open (a settings toggle). Resolves with the chosen id,
 * or null when dismissed with Back.
 */
export function openDialog({ title, text = '', body = '', actions = [], focus, onAction }) {
  closeDialog(null);
  return new Promise(resolve => {
    const el = $('#modal');
    el.innerHTML = `<div class="modal-card" role="dialog" aria-modal="true" aria-labelledby="modal-title">
      <h2 id="modal-title">${esc(title)}</h2>${text ? `<p>${esc(text)}</p>` : ''}${body}
      <div class="modal-actions">${actions.map(a => `<button class="btn${a.primary ? ' primary' : ''}${a.danger ? ' danger' : ''}" data-nav data-act="${esc(a.id)}"${a.id === focus ? ' data-default' : ''}>${a.icon ? icon(a.icon) : ''}${esc(a.label)}</button>`).join('')}</div>
    </div>`;
    el.hidden = false;
    $('main').inert = true;
    $('.topbar').inert = true;
    dialog = { el, resolve, returnFocus: document.activeElement };
    el.onclick = e => {
      const b = e.target.closest('[data-act]');
      if (b) { if (onAction?.(b.dataset.act, b) !== false) closeDialog(b.dataset.act); }
      else if (e.target === el) closeDialog(null);
    };
    sfx.open();
    focusFirst(el);
    renderHints();
  });
}

export const dialogOpen = () => !!dialog;

export function closeDialog(result) {
  if (!dialog) return;
  const { el, resolve, returnFocus } = dialog;
  dialog = null;
  el.hidden = true;
  el.innerHTML = '';
  $('main').inert = false;
  $('.topbar').inert = false;
  if (result === null) sfx.close();
  if (returnFocus?.isConnected) focusEl(returnFocus);
  renderHints();
  resolve(result);
}
