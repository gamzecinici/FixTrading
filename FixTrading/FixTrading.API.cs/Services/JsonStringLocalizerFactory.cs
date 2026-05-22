using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace FixTrading.API.Services;

public sealed class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly IWebHostEnvironment _environment;
    private readonly ConcurrentDictionary<string, Lazy<IReadOnlyDictionary<string, string>>> _resourceCache = new();

    public JsonStringLocalizerFactory(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public IStringLocalizer Create(Type resourceSource)
    {
        return new JsonStringLocalizer(_environment.ContentRootPath, resourceSource.Name, _resourceCache);
    }

    public IStringLocalizer Create(string baseName, string location)
    {
        var resourceName = baseName.Split('.').Last();
        return new JsonStringLocalizer(_environment.ContentRootPath, resourceName, _resourceCache);
    }

    private sealed class JsonStringLocalizer : IStringLocalizer
    {
        private readonly string _contentRootPath;
        private readonly string _resourceName;
        private readonly ConcurrentDictionary<string, Lazy<IReadOnlyDictionary<string, string>>> _resourceCache;

        public JsonStringLocalizer(
            string contentRootPath,
            string resourceName,
            ConcurrentDictionary<string, Lazy<IReadOnlyDictionary<string, string>>> resourceCache)
        {
            _contentRootPath = contentRootPath;
            _resourceName = resourceName;
            _resourceCache = resourceCache;
        }

        public LocalizedString this[string name]
        {
            get
            {
                var value = GetString(name);
                return new LocalizedString(name, value ?? name, resourceNotFound: value is null);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var value = GetString(name);
                if (value is null)
                    return new LocalizedString(name, name, resourceNotFound: true);

                return new LocalizedString(
                    name,
                    string.Format(CultureInfo.CurrentCulture, value, arguments),
                    resourceNotFound: false);
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var cultures = includeParentCultures
                ? GetCultureNames(CultureInfo.CurrentUICulture).Reverse()
                : new[] { CultureInfo.CurrentUICulture.Name };

            foreach (var cultureName in cultures)
            {
                foreach (var (key, value) in GetResource(cultureName))
                    values[key] = value;
            }

            return values.Select(item => new LocalizedString(item.Key, item.Value, resourceNotFound: false));
        }

        private string? GetString(string name)
        {
            foreach (var cultureName in GetCultureNames(CultureInfo.CurrentUICulture))
            {
                var resource = GetResource(cultureName);
                if (resource.TryGetValue(name, out var value))
                    return value;
            }

            return null;
        }

        private IReadOnlyDictionary<string, string> GetResource(string cultureName)
        {
            var cacheKey = $"{_resourceName}|{cultureName}";
            return _resourceCache.GetOrAdd(
                cacheKey,
                _ => new Lazy<IReadOnlyDictionary<string, string>>(
                    () => LoadResource(cultureName),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }

        private IReadOnlyDictionary<string, string> LoadResource(string cultureName)
        {
            var fileName = string.IsNullOrWhiteSpace(cultureName)
                ? $"{_resourceName}.json"
                : $"{_resourceName}.{cultureName}.json";
            var path = Path.Combine(_contentRootPath, "Resources", fileName);

            if (!File.Exists(path))
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> GetCultureNames(CultureInfo culture)
        {
            for (var current = culture; !string.IsNullOrWhiteSpace(current.Name); current = current.Parent)
                yield return current.Name;

            yield return string.Empty;
        }
    }
}
