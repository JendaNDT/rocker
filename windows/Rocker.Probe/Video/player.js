// SPDX-License-Identifier: AGPL-3.0-only
(() => {
  "use strict";
  let player, loaded = false, pending, timeout, timer, generation = 0, loop, state = -1;
  const send = value => window.chrome.webview.postMessage(value);
  const origin = window.location.origin;
  function fail(code) { if (pending) send({ event: "error", code, token: pending.token }); }
  function load(id, token = 0) {
    if (!/^[A-Za-z0-9_-]{11}$/.test(id) || !Number.isSafeInteger(token) || token < 0 || token > 2147483647) return;
    pending = { id, token }; loop = null; state = -1;
    if (loaded) { create(); return; }
    if (document.getElementById("youtube-api")) return;
    const script = document.createElement("script");
    script.id = "youtube-api"; script.src = "https://www.youtube.com/iframe_api";
    script.onerror = () => { clearTimeout(timeout); script.remove(); fail(-1); };
    timeout = setTimeout(() => { script.remove(); fail(-1); }, 20000);
    document.head.appendChild(script);
  }
  function progress(epoch = generation) {
    if (epoch !== generation || !player || typeof player.getCurrentTime !== "function") return;
    const position = player.getCurrentTime(), duration = player.getDuration();
    if (loop && (state === 1 || state === 0) && (position >= loop.end || position < loop.start)) {
      player.seekTo(loop.start, true);
      if (state === 0) player.playVideo();
    }
    send({ event: "progress", position, duration, rate: player.getPlaybackRate(), token: pending.token });
  }
  function create() {
    clearInterval(timer); const epoch = ++generation, token = pending.token;
    if (player) player.destroy();
    if (!document.getElementById("player")) {
      const target = document.createElement("div"); target.id = "player"; document.body.appendChild(target);
    }
    const emit = value => { if (epoch === generation) send({ ...value, token }); };
    player = new YT.Player("player", {
      width: "100%", height: "100%", videoId: pending.id,
      playerVars: { autoplay: 0, controls: 1, enablejsapi: 1, origin, playsinline: 1 },
      events: {
        onReady: () => {
          if (epoch !== generation) return;
          emit({ event: "ready" }); emit({ event: "rates", rates: player.getAvailablePlaybackRates() });
          progress(epoch); timer = setInterval(() => progress(epoch), 250);
        },
        onStateChange: event => { if (epoch !== generation) return; state = event.data; emit({ event: "state", state }); progress(epoch); },
        onPlaybackRateChange: () => progress(epoch),
        onError: event => emit({ event: "error", code: event.data })
      }
    });
  }
  window.onYouTubeIframeAPIReady = () => { clearTimeout(timeout); loaded = true; if (pending) create(); };
  window.chrome.webview.addEventListener("message", event => {
    const command = event.data;
    if (!command || typeof command !== "object") return;
    if (command.op === "load") { load(command.id, command.token); return; }
    if (!player || typeof player.playVideo !== "function" || (command.token !== undefined && command.token !== pending.token)) return;
    if (command.op === "play") player.playVideo();
    else if (command.op === "pause") player.pauseVideo();
    else if (command.op === "stop") player.stopVideo();
    else if (command.op === "seek" && Number.isFinite(command.seconds) && command.seconds >= 0 && command.seconds <= 864000) player.seekTo(command.seconds, true);
    else if (command.op === "rate" && player.getAvailablePlaybackRates().includes(command.rate)) player.setPlaybackRate(command.rate);
    else if (command.op === "loop") {
      if (command.enabled === false) loop = null;
      else if (command.enabled === true && Number.isFinite(command.start) && Number.isFinite(command.end) && command.start >= 0 && command.end > command.start && command.end <= 864000) loop = { start: command.start, end: command.end };
    }
  });
  send({ event: "bridge-ready" });
})();
