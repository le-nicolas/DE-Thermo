using System.Globalization;

namespace DEThermo.Cli;

internal sealed class OptionMap
{
    private readonly Dictionary<string, string> _values;

    private OptionMap(Dictionary<string, string> values)
    {
        _values = values;
    }

    public static OptionMap Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? pendingKey = null;

        foreach (var token in args)
        {
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                if (pendingKey is not null)
                {
                    map[pendingKey] = "true";
                }

                pendingKey = token[2..].Trim();
                if (string.IsNullOrWhiteSpace(pendingKey))
                {
                    throw new ArgumentException("Invalid empty option name.");
                }
            }
            else
            {
                if (pendingKey is null)
                {
                    throw new ArgumentException($"Unexpected value '{token}' with no option flag.");
                }

                map[pendingKey] = token.Trim();
                pendingKey = null;
            }
        }

        if (pendingKey is not null)
        {
            map[pendingKey] = "true";
        }

        return new OptionMap(map);
    }

    public string GetRequiredString(string key)
    {
        if (_values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new ArgumentException($"Missing required option --{key}");
    }

    public string? GetStringOrNull(string key)
    {
        return _values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    public int GetInt(string key, int fallback)
    {
        if (!_values.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new ArgumentException($"Option --{key} expects an integer.");
    }

    public double GetDouble(string key, double fallback)
    {
        if (!_values.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        if (double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new ArgumentException($"Option --{key} expects a number.");
    }

    public double GetRequiredDouble(string key)
    {
        if (!_values.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            throw new ArgumentException($"Missing required option --{key}");
        }

        if (double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new ArgumentException($"Option --{key} expects a number.");
    }
}
