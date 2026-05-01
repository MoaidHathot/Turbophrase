using System.Text.Json;
using System.Text.Json.Nodes;

namespace Turbophrase.Core.Configuration;

/// <summary>
/// Round-trip JSON editor for <c>turbophrase.json</c>. Mutates the existing file
/// in-place using <see cref="JsonNode"/> so that unknown keys (set by power users
/// who hand-edit the file or by features added in newer versions) are preserved
/// across saves.
/// </summary>
/// <remarks>
/// Comments are not preserved (System.Text.Json does not retain them when
/// re-serializing). Property casing and ordering are preserved when possible.
/// </remarks>
public class ConfigEditor
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly JsonNodeOptions NodeOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private readonly string _filePath;
    private readonly JsonObject _root;

    private ConfigEditor(string filePath, JsonObject root)
    {
        _filePath = filePath;
        _root = root;
    }

    /// <summary>
    /// Loads the configuration file at the given path. If the file does not exist
    /// it is first created with default content.
    /// </summary>
    public static ConfigEditor LoadOrCreate(string filePath)
    {
        if (!File.Exists(filePath))
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, ConfigurationService.GetDefaultConfigJson());
        }

        var content = File.ReadAllText(filePath);
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(content, NodeOptions, DocumentOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Configuration file '{filePath}' is not valid JSON: {ex.Message}", ex);
        }

        var root = node as JsonObject ?? new JsonObject();
        return new ConfigEditor(filePath, root);
    }

    /// <summary>
    /// Path of the underlying configuration file.
    /// </summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Sets the default provider name.
    /// </summary>
    public void SetDefaultProvider(string providerName)
    {
        _root["defaultProvider"] = providerName;
    }

    /// <summary>
    /// Sets the global custom-prompt system prompt template.
    /// </summary>
    public void SetCustomPromptTemplate(string template)
    {
        var customPrompt = GetOrCreateObject("customPrompt");
        customPrompt["systemPromptTemplate"] = template;
    }

    /// <summary>
    /// Replaces the entire <c>notifications</c> section with the given settings.
    /// </summary>
    public void SetNotifications(NotificationSettings notifications)
    {
        var node = JsonSerializer.SerializeToNode(notifications, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        }) as JsonObject;

        if (node != null)
        {
            _root["notifications"] = node;
        }
    }

    /// <summary>
    /// Replaces the entire <c>logging</c> section with the given settings.
    /// </summary>
    public void SetLogging(LoggingSettings logging)
    {
        var node = JsonSerializer.SerializeToNode(logging, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        }) as JsonObject;

        if (node != null)
        {
            _root["logging"] = node;
        }
    }

    /// <summary>
    /// Returns the names of providers currently present in the configuration.
    /// </summary>
    public IReadOnlyList<string> GetProviderNames()
    {
        if (_root["providers"] is not JsonObject providers)
        {
            return Array.Empty<string>();
        }

        return providers.Select(kv => kv.Key).ToList();
    }

    /// <summary>
    /// Reads the raw value of a single provider field as it currently appears
    /// in the file (i.e., before <c>${ENV}</c> or <c>@credman:</c> expansion).
    /// Returns <c>null</c> when the provider or field is missing.
    /// </summary>
    public string? GetProviderRawField(string providerName, string fieldName)
    {
        if (_root["providers"] is not JsonObject providers ||
            providers[providerName] is not JsonObject provider)
        {
            return null;
        }

        return provider[fieldName]?.GetValue<string>();
    }

    /// <summary>
    /// Updates a single provider's fields. Existing keys not listed in
    /// <paramref name="fields"/> are preserved. <c>null</c> values remove the
    /// key. Creates the provider entry if it does not exist.
    /// </summary>
    public void SetProviderFields(string providerName, IDictionary<string, string?> fields)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name is required.", nameof(providerName));
        }

        var providers = GetOrCreateObject("providers");
        if (providers[providerName] is not JsonObject provider)
        {
            provider = new JsonObject();
            providers[providerName] = provider;
        }

        foreach (var (key, value) in fields)
        {
            if (value == null)
            {
                provider.Remove(key);
            }
            else
            {
                provider[key] = value;
            }
        }
    }

    /// <summary>
    /// Removes a provider entry, returning <c>true</c> when it existed.
    /// </summary>
    public bool RemoveProvider(string providerName)
    {
        if (_root["providers"] is not JsonObject providers)
        {
            return false;
        }

        return providers.Remove(providerName);
    }

    // ------------------------------------------------------------------
    // Presets
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns the names of presets currently defined.
    /// </summary>
    public IReadOnlyList<string> GetPresetNames()
    {
        if (_root["presets"] is not JsonObject presets)
        {
            return Array.Empty<string>();
        }

        return presets.Select(kv => kv.Key).ToList();
    }

    /// <summary>
    /// Reads the raw string value of a preset field, or <c>null</c> when the
    /// preset or field is missing. Boolean and numeric fields are returned in
    /// their JSON string form (e.g. <c>"true"</c>, <c>"1"</c>).
    /// </summary>
    public string? GetPresetRawField(string presetKey, string fieldName)
    {
        if (_root["presets"] is not JsonObject presets ||
            presets[presetKey] is not JsonObject preset)
        {
            return null;
        }

        return preset[fieldName] switch
        {
            null => null,
            JsonValue v when v.TryGetValue<string>(out var s) => s,
            { } node => node.ToJsonString().Trim('"'),
        };
    }

    /// <summary>
    /// Replaces the entire entry for <paramref name="presetKey"/>. Existing
    /// keys not represented by <see cref="PromptPreset"/> are dropped; this
    /// is acceptable because presets do not host extension data today.
    /// To rename a preset, call <see cref="RenamePreset"/> instead.
    /// </summary>
    public void SetPreset(string presetKey, PromptPreset preset)
    {
        if (string.IsNullOrWhiteSpace(presetKey))
        {
            throw new ArgumentException("Preset key is required.", nameof(presetKey));
        }

        var presets = GetOrCreateObject("presets");
        var node = JsonSerializer.SerializeToNode(preset, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        }) as JsonObject ?? new JsonObject();

        presets[presetKey] = node;
    }

    /// <summary>
    /// Renames a preset, returning <c>true</c> when the source existed.
    /// Hotkeys that referenced the old preset key are updated in place.
    /// </summary>
    public bool RenamePreset(string oldKey, string newKey)
    {
        if (string.IsNullOrWhiteSpace(newKey))
        {
            throw new ArgumentException("New preset key is required.", nameof(newKey));
        }

        if (string.Equals(oldKey, newKey, StringComparison.Ordinal))
        {
            return true;
        }

        if (_root["presets"] is not JsonObject presets || presets[oldKey] is not JsonNode existing)
        {
            return false;
        }

        if (presets[newKey] != null)
        {
            throw new InvalidOperationException($"A preset named '{newKey}' already exists.");
        }

        presets.Remove(oldKey);
        presets[newKey] = existing.DeepClone();

        // Update any hotkeys that referenced the old key.
        if (_root["hotkeys"] is JsonArray hotkeys)
        {
            foreach (var node in hotkeys)
            {
                if (node is JsonObject obj &&
                    obj["preset"] is JsonValue val &&
                    val.TryGetValue<string>(out var preset) &&
                    string.Equals(preset, oldKey, StringComparison.Ordinal))
                {
                    obj["preset"] = newKey;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Removes a preset, returning <c>true</c> when it existed.
    /// </summary>
    public bool RemovePreset(string presetKey)
    {
        if (_root["presets"] is not JsonObject presets)
        {
            return false;
        }

        return presets.Remove(presetKey);
    }

    // ------------------------------------------------------------------
    // Hotkeys & picker actions
    // ------------------------------------------------------------------

    /// <summary>
    /// Replaces the entire <c>hotkeys</c> array.
    /// </summary>
    public void SetHotkeys(IEnumerable<HotkeyBinding> bindings)
    {
        _root["hotkeys"] = SerializeBindings(bindings);
    }

    /// <summary>
    /// Replaces the entire <c>pickerActions</c> array.
    /// </summary>
    public void SetPickerActions(IEnumerable<HotkeyBinding> actions)
    {
        _root["pickerActions"] = SerializeBindings(actions);
    }

    private static JsonArray SerializeBindings(IEnumerable<HotkeyBinding> bindings)
    {
        var array = new JsonArray();
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };

        foreach (var binding in bindings)
        {
            var node = JsonSerializer.SerializeToNode(binding, options) as JsonObject ?? new JsonObject();

            // HotkeyBinding exposes IsCustomPromptAction etc. as computed
            // properties; serializer also walks them. Strip the boolean
            // helpers so the file stays clean.
            node.Remove("isCustomPromptAction");
            node.Remove("isPresetPickerAction");
            node.Remove("isPresetAction");

            // Preset is "" for non-preset actions; drop the empty string so
            // the JSON remains tidy.
            if (node["preset"] is JsonValue v && v.TryGetValue<string>(out var preset) && string.IsNullOrEmpty(preset))
            {
                node.Remove("preset");
            }

            array.Add(node);
        }
        return array;
    }

    /// <summary>
    /// Persists the current document to disk.
    /// </summary>
    public void Save()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_filePath, _root.ToJsonString(WriteOptions));
    }

    /// <summary>
    /// Resets the configuration file at the given path to defaults, optionally
    /// creating a backup of the existing file.
    /// </summary>
    /// <returns>The path of the backup file, or <c>null</c> if no backup was made.</returns>
    public static string? ResetToDefaults(string filePath, bool createBackup = true)
    {
        string? backupPath = null;
        if (createBackup && File.Exists(filePath))
        {
            backupPath = $"{filePath}.bak-{DateTime.Now:yyyyMMddHHmmss}";
            File.Copy(filePath, backupPath, overwrite: false);
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, ConfigurationService.GetDefaultConfigJson());
        return backupPath;
    }

    private JsonObject GetOrCreateObject(string propertyName)
    {
        if (_root[propertyName] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        _root[propertyName] = created;
        return created;
    }
}
