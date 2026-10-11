// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Video;
int checks = 0;
void Check(bool value, string message) { ++checks; if (!value) throw new Exception(message); }
foreach (var input in new[] { "M7lc1UVf-VE", "https://youtu.be/M7lc1UVf-VE?t=12", "https://www.youtube.com/watch?v=M7lc1UVf-VE&list=ignored", "https://m.youtube.com/shorts/M7lc1UVf-VE", "https://youtube.com/embed/M7lc1UVf-VE", "https://music.youtube.com/live/M7lc1UVf-VE" })
    Check(VideoProtocol.ExtractId(input) == "M7lc1UVf-VE", "valid YouTube URL");
foreach (var input in new[] { "", "javascript:alert(1)", "https://youtube.com.evil.example/watch?v=M7lc1UVf-VE", "https://youtube.com@evil.example/watch?v=M7lc1UVf-VE", "https://user@youtube.com/watch?v=M7lc1UVf-VE", "https://youtube.com:123/watch?v=M7lc1UVf-VE", "https://youtube.com/watch?v=short", "http://youtube.com/watch?v=M7lc1UVf-VE" })
    Check(VideoProtocol.ExtractId(input) is null, "untrusted URL rejected");
Check(VideoProtocol.ReadEvent(VideoProtocol.Page, "{\"event\":\"bridge-ready\"}")?.Kind == "bridge-ready", "local bridge identity");
Check(VideoProtocol.ReadEvent("https://youtube.com", "{\"event\":\"ready\"}") is null, "iframe cannot impersonate wrapper");
Check(VideoProtocol.ReadEvent(VideoProtocol.Page, "{\"event\":\"unknown\"}") is null, "unknown event rejected");
Check(VideoProtocol.ReadEvent(VideoProtocol.Page, "{\"event\":\"progress\",\"position\":12,\"duration\":120,\"rate\":0.5}")?.Rate == 0.5, "typed progress");
Check(VideoProtocol.ReadEvent(VideoProtocol.Page, "{\"event\":\"progress\",\"position\":-1,\"duration\":120,\"rate\":1}") is null, "invalid progress rejected");
Check(VideoProtocol.ReadEvent(VideoProtocol.Page, "{\"event\":\"rates\",\"rates\":[0.5,1,1]}")?.Rates?.Length == 2, "actual rates deduplicated");
Check(VideoProtocol.ReadEvent(VideoProtocol.Page, "{\"event\":\"rates\",\"rates\":[0]}") is null, "invalid rates rejected");
Check(VideoProtocol.ReadEvent(VideoProtocol.Page, "{\"event\":\"error\",\"code\":153}")?.Code == 153, "identity error preserved");
Check(VideoProtocol.ReadEvent(VideoProtocol.Page, "{\"event\":\"state\",\"state\":400}") is null, "invalid state rejected");
foreach (var malformed in new[] { "null", "[]", "not json", "{\"event\":123}", "{\"event\":\"error\",\"code\":{}}", new string('x',8193) })
    Check(VideoProtocol.ReadEvent(VideoProtocol.Page, malformed) is null, "malformed message rejected without exception");
Check(VideoProtocol.DescribeError(153).Contains("identifikaci", StringComparison.Ordinal), "actionable identity error");
Console.WriteLine($"PASS: {checks} URL/bridge assertions; no online video or physical audio tested.");
