// SPDX-License-Identifier: AGPL-3.0-only
namespace Rocker.Domain;
public sealed record Completion(int Done, int Total);
public static class LessonRules
{
    public static bool IsLessonComplete(string id, IReadOnlyCollection<string> completed) => completed.Contains(id);
    public static Completion Completion(IEnumerable<Lesson> lessons, IReadOnlyCollection<string> completed)
    {
        var ids = new HashSet<string>(completed, StringComparer.Ordinal);
        int total = 0, done = 0;
        foreach (var lesson in lessons) { ++total; if (ids.Contains(lesson.Id)) ++done; }
        return new Completion(done, total);
    }
    public static Lesson? NextIncomplete(IEnumerable<Lesson> lessons, IReadOnlyCollection<string> completed) =>
        lessons.FirstOrDefault(lesson => !completed.Contains(lesson.Id));
    public static string[] CompleteLesson(IReadOnlyCollection<string> completed, string id) => completed.Append(id).Distinct(StringComparer.Ordinal).ToArray();
}
