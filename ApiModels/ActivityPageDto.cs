public sealed record ActivityPageDto<T>(IReadOnlyList<T> Items, bool Degraded);
