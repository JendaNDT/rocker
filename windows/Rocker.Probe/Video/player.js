// SPDX-License-Identifier: AGPL-3.0-only
(() => {
  "use strict";
  let player, loaded = false, pending, timeout, timer;
  const send = value => window.chrome.webview.postMessage(value);
  const origin = window.location.origin;
  function load(id) {
    if (!/^[A-Za-z0-9_-]{11}$/.test(id)) return;
    pending = id;
    if (loaded) { create(); return; }
    if (document.getElementById("youtube-api")) return;
    const script = document.createElement("script");
    script.id = "youtube-api";
    script.src = "https://www.youtube.com/iframe_api";
    script.onerror = () => {
      clearTimeout(timeout); script.remove(); send({ event: "error", code: -1 });
    };
    timeout = setTimeout(() => { script.remove(); send({ event: "error", code: -1 }); }, 20000);
    document.head.appendChild(script);
  }
  function progress() {
    if (!player || typeof player.getCurrentTime !== "function") return;
    send({ event: "progress", position: player.getCurrentTime(), duration: player.getDuration(), rate: player.getPlaybackRate() });
  }
  function create() {
    clearInterval(timer);
    if (player) player.destroy();
    if (!document.getElementById("player")) {
      const target = document.createElement("div"); target.id = "player"; document.body.appendChild(target);
    }
    player = new YT.Player("player", {
      width: "100%", height: "100%", videoId: pending,
      playerVars: { autoplay: 0, controls: 1, enablejsapi: 1, origin, playsinline: 1 },
      events: {
        onReady: () => {
          send({ event: "ready" });
          send({ event: "rates", rates: player.getAvailablePlaybackRates() });
          progress(); timer = setInterval(progress, 250);
        },
        onStateChange: event => { send({ event: "state", state: event.data }); progress(); },
        onPlaybackRateChange: progress,
        onError: event => send({ event: "error", code: event.data })
      }
    });
  }
  window.onYouTubeIframeAPIReady = () => { clearTimeout(timeout); loaded = true; if (pending) create(); };
  window.chrome.webview.addEventListener("message", event => {
    const command = event.data;
    if (!command || typeof command !== "object") return;
    if (command.op === "load") { load(command.id); return; }
    if (!player || typeof player.playVideo !== "function") return;
    if (command.op === "play") player.playVideo();
    else if (command.op === "pause") player.pauseVideo();
    else if (command.op === "stop") player.stopVideo();
    else if (command.op === "seek" && Number.isFinite(command.seconds) && command.seconds >= 0) player.seekTo(command.seconds, true);
    else if (command.op === "rate" && player.getAvailablePlaybackRates().includes(command.rate)) player.setPlaybackRate(command.rate);
  });
  send({ event: "bridge-ready" });
})();
