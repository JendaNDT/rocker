# W39 — measured production graph budget

`rocker_performance_tests` renders 60 minutes of synthetic audio through the production C++ graph: 20 minutes each of amp + metronome + recording, eight loop layers, and eight project tracks. Configuration: 48 kHz, 256-frame blocks, selected mono input, stereo output, all amplifier effects active at the values in the source. Each processed recording frame must be consumed exactly once, DSP samples finite, queues without loss, and callback ownership allocation/release counts zero. P99 must use less than half of the 5.333 ms block budget. RSS before/after and P50/P95/P99/max are recorded, including every offline deadline exceedance.

This is accelerated offline rendering, not a real-time hour on a sound card. The recording consumer drains synchronously, excluding disk throughput. Pitch queue is drained without analysis; worker correctness is covered separately. Scheduling spikes are retained in the report, never called zero hardware xruns. ASIO/WASAPI, simultaneous YouTube, online AI, actual disk throughput and 60 minutes of wall-clock playing on M-Track remain in W44.

On this Linux host the three P99 values were 325, 305 and 447 microseconds. Maximum observations were 35.4, 6.4 and 76.9 ms; those exceed the block deadline and prevent claiming a real-time stability result. Audio graph ownership allocations/releases and lost recording frames were zero. The report identifies platform/configuration; Windows produces its own report during build.

AI HTTP/SSE decoding now resumes outside the UI synchronization context, and posts guarded partial updates through the DispatcherQueue. Audio execution remains separate from rendering and HTTP. Native source and UI work use bounded queues/caches and fixed control ownership. Larger-than-tested track/layer ranges are limits, not measured performance claims.

Reproduce from a Release media-enabled build:

```
rocker_performance_tests performance-checks.json 3600
```

A `3`-second quick run checks setup only. The CI suite runs the complete accelerated hour. Native screenshot capture also exercises 32 pages, light/dark and widths 640/1280/1680; physical DPI changes and screen-reader use remain deferred.
