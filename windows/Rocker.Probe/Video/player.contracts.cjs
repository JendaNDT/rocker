// SPDX-License-Identifier: AGPL-3.0-only
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
function bridge() {
  const messages = [], scripts = [], players = [];
  let listener, interval, options;
  const elements = new Map([['player', {}]]);
  const player = {
    destroy() { elements.delete('player'); },
    getAvailablePlaybackRates: () => [0.5, 1, 2], getPlaybackRate: () => 1,
    getCurrentTime: () => 12, getDuration: () => 120,
    playVideo() { this.played = true; }, pauseVideo() { this.paused = true; }, stopVideo() { this.stopped = true; },
    setPlaybackRate(value) { this.rate = value; }, seekTo(value) { this.seek = value; }
  };
  const context = {
    window: { location: { origin: 'https://jendandt.rocker' }, chrome: { webview: {
      postMessage: message => messages.push(message), addEventListener: (_, callback) => { listener = callback; }
    } } },
    document: {
      getElementById: id => elements.get(id),
      createElement: () => ({ remove() { elements.delete(this.id); } }),
      head: { appendChild(element) { scripts.push(element); elements.set(element.id, element); } },
      body: { appendChild(element) { elements.set(element.id, element); } }
    },
    YT: { Player: function (_, config) { options = config; players.push(config); return player; } },
    setTimeout: () => 1, clearTimeout() {}, setInterval: fn => { interval = fn; return 2; }, clearInterval() {}
  };
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, 'player.js'), 'utf8'), context);
  return { messages, scripts, players, player, context, send: data => listener({ data }),
    ready() { context.window.onYouTubeIframeAPIReady(); options.events.onReady(); },
    get options() { return options; }, progress() { interval(); } };
}
test('local initialization has no network request or autoplay', () => {
  const b = bridge(); assert.equal(b.messages[0].event, 'bridge-ready'); assert.equal(b.scripts.length, 0); assert.equal(b.players.length, 0);
});
test('invalid commands cannot inject an URL or script', () => {
  const b = bridge(); b.send({ op: 'load', id: 'https://evil.example' }); b.send(null); b.send('code'); assert.equal(b.scripts.length, 0);
});
test('official API keeps origin, visible controls and explicit playback', () => {
  const b = bridge(); b.send({ op: 'load', id: 'M7lc1UVf-VE' });
  assert.equal(b.scripts[0].src, 'https://www.youtube.com/iframe_api'); b.ready();
  assert.equal(b.options.playerVars.origin, 'https://jendandt.rocker'); assert.equal(b.options.playerVars.autoplay, 0); assert.equal(b.options.playerVars.controls, 1);
  assert.equal(b.player.played, undefined); b.send({ op: 'play' }); assert.equal(b.player.played, true);
  b.send({ op: 'pause' }); assert.equal(b.player.paused, true); b.send({ op: 'stop' }); assert.equal(b.player.stopped, true);
});
test('rates are observed from the API and unsupported requests are ignored', () => {
  const b = bridge(); b.send({ op: 'load', id: 'M7lc1UVf-VE' }); b.ready();
  assert.equal(b.messages.find(message => message.event === 'rates').rates.length, 3);
  b.send({ op: 'rate', rate: 0.75 }); assert.equal(b.player.rate, undefined);
  b.send({ op: 'rate', rate: 0.5 }); assert.equal(b.player.rate, 0.5);
  b.send({ op: 'seek', seconds: -1 }); assert.equal(b.player.seek, undefined);
  b.send({ op: 'seek', seconds: 30 }); assert.equal(b.player.seek, 30);
  b.progress(); assert.equal(b.messages.at(-1).position, 12);
});
test('embed errors and states preserve official result codes', () => {
  const b = bridge(); b.send({ op: 'load', id: 'M7lc1UVf-VE' }); b.ready();
  b.options.events.onError({ data: 153 }); assert.equal(b.messages.at(-1).code, 153);
  b.options.events.onStateChange({ data: 2 }); assert.ok(b.messages.some(message => message.event === 'state' && message.state === 2));
});
test('loading another video recreates the target without another API request', () => {
  const b = bridge(); b.send({ op: 'load', id: 'M7lc1UVf-VE' }); b.ready();
  b.send({ op: 'load', id: 'dQw4w9WgXcQ' }); assert.equal(b.scripts.length, 1); assert.equal(b.players.length, 2); assert.equal(b.options.videoId, 'dQw4w9WgXcQ');
});
test('A/B loops follow playing progress and never force a paused seek', () => {
  const b = bridge(); b.send({ op: 'load', id: 'M7lc1UVf-VE', token: 7 }); b.ready();
  b.send({ op: 'loop', enabled: true, start: 4, end: 10, token: 7 });
  b.progress(); assert.equal(b.player.seek, undefined);
  b.options.events.onStateChange({ data: 1 }); assert.equal(b.player.seek, 4);
  b.player.seek = undefined; b.send({ op: 'loop', enabled: false, token: 7 }); b.progress(); assert.equal(b.player.seek, undefined);
});
test('replacement rejects obsolete callbacks and native commands', () => {
  const b = bridge(); b.send({ op: 'load', id: 'M7lc1UVf-VE', token: 1 }); b.ready(); const old = b.options;
  b.send({ op: 'load', id: 'dQw4w9WgXcQ', token: 2 }); const count = b.messages.length;
  old.events.onError({ data: 100 }); old.events.onStateChange({ data: 1 }); old.events.onReady(); assert.equal(b.messages.length, count);
  b.options.events.onReady(); assert.equal(b.messages.at(-1).token, 2);
  b.send({ op: 'play', token: 1 }); assert.equal(b.player.played, undefined);
  b.send({ op: 'seek', seconds: 30, token: 2 }); assert.equal(b.player.seek, 30);
});
