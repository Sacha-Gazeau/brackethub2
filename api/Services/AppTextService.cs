using System.Reflection;
using System.Text.Json.Nodes;

namespace api.Services;

public interface IAppTextService
{
    string Get(string key);
    string Get(string key, IReadOnlyDictionary<string, string?> replacements);
}

public sealed class AppTextService : IAppTextService
{
    private const string ResourceName = "api.Localization.nl-BE.json";

    private readonly JsonNode? _root;
    private readonly ILogger<AppTextService> _logger;

    public AppTextService(ILogger<AppTextService> logger)
    {
        _logger = logger;
        _root = LoadRoot();
    }

    public string Get(string key) => Get(key, new Dictionary<string, string?>());

    public string Get(string key, IReadOnlyDictionary<string, string?> replacements)
    {
        var template = ResolveString(key);
        if (string.IsNullOrWhiteSpace(template))
        {
            _logger.LogWarning("Missing localization text for key {LocalizationKey}.", key);
            return key;
        }

        var value = template;
        foreach (var replacement in replacements)
        {
            value = value.Replace(
                $"{{{{{replacement.Key}}}}}",
                replacement.Value ?? string.Empty,
                StringComparison.Ordinal);
        }

        return value;
    }

    private JsonNode? LoadRoot()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                _logger.LogError("Localization resource {ResourceName} was not found.", ResourceName);
                return null;
            }

            return JsonNode.Parse(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load localization resource {ResourceName}.", ResourceName);
            return null;
        }
    }

    private string? ResolveString(string key)
    {
        JsonNode? current = _root;
        foreach (var segment in key.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is not JsonObject currentObject ||
                !currentObject.TryGetPropertyValue(segment, out current))
            {
                return null;
            }
        }

        return current?.GetValue<string>();
    }
}
