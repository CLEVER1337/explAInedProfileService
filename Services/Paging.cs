public static class Paging
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public static int Limit(int? requested) => Math.Clamp(requested ?? DefaultLimit, 1, MaxLimit);

    public static int Offset(int? requested) => Math.Max(requested ?? 0, 0);
}
