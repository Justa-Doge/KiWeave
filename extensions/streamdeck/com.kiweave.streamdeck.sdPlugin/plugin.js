/* KiWeave's Stream Deck plugin intentionally forwards only the three
   declarative actions exposed by StreamDeckProtocol. It never runs commands. */
const WebSocket = require('ws');
const net = require('net');
let websocket = null;
function sendKiWeave(message, done) {
  const pipe = net.connect('\\\\.\\pipe\\KiWeave-StreamDeck-' + (process.env.USERNAME || 'user'));
  let data = '';
  pipe.on('data', chunk => { data += chunk.toString(); });
  pipe.on('end', () => { try { done(JSON.parse(data)); } catch (_) { done({ ok: false }); } });
  pipe.on('error', () => done({ ok: false }));
  pipe.end(JSON.stringify(message) + '\n');
}
function invoke(action, context, profile) {
  const message = profile ? { version: 1, action: action, profile: profile } : { version: 1, action: action };
  sendKiWeave(message, result => {
    if (websocket && context) websocket.send(JSON.stringify({ event: 'showOk', context: context }));
    if (!result.ok && websocket && context) websocket.send(JSON.stringify({ event: 'showAlert', context: context }));
  });
}
function connectElgato(port, uuid) {
  websocket = new WebSocket('ws://127.0.0.1:' + port);
  websocket.on('open', () => websocket.send(JSON.stringify({ event: 'registerPlugin', uuid: uuid })));
  websocket.on('message', raw => {
    let event; try { event = JSON.parse(raw); } catch (_) { return; }
    if (event.event !== 'keyDown') return;
    const uuid = event.action || '';
    if (uuid === 'com.kiweave.status') invoke('status', event.context, null);
    else if (uuid === 'com.kiweave.settings') invoke('show-settings', event.context, null);
    else if (uuid === 'com.kiweave.profile') invoke('activate-profile', event.context, event.payload && event.payload.profile);
  });
}
if (require.main === module) connectElgato(process.argv[2], process.argv[3]);
module.exports = { sendKiWeave: sendKiWeave, invoke: invoke };
