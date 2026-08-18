using Prometheus;

public static class ProfileMetrics
{
    public static readonly Counter LazyCreated = Metrics.CreateCounter(
        "profile_lazy_created_total", "Profiles materialised on first authenticated access.");

    public static readonly Counter UpstreamFailures = Metrics.CreateCounter(
        "profile_upstream_failures_total", "Activity-aggregation upstream failures.", "upstream");

    public static readonly Histogram AvatarBytes = Metrics.CreateHistogram(
        "profile_avatar_bytes", "Accepted avatar payload size in bytes.",
        new HistogramConfiguration { Buckets = [4096, 16384, 65536, 262144, 524288, 1048576] });
}
