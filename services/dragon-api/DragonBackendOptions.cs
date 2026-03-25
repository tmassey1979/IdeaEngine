namespace Dragon.Api;

public sealed class DragonBackendOptions
{
    public const string SectionName = "DragonBackend";

    public string BaseUrl { get; set; } = "http://dragon-backend:5078";
    public int TimeoutSeconds { get; set; } = 60;
}
