using System.Text.Json.Nodes;
using Turbophrase.Core.Configuration;

namespace Turbophrase.Core.Tests.Configuration;

public class ConfigEditorTests : IDisposable
{
    private readonly string _tempPath;

    public ConfigEditorTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"turbophrase-test-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath))
        {
            File.Delete(_tempPath);
        }

        // Clean up any backup files we created.
        var dir = Path.GetDirectoryName(_tempPath)!;
        foreach (var file in Directory.EnumerateFiles(dir, $"{Path.GetFileName(_tempPath)}.bak-*"))
        {
            try { File.Delete(file); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void LoadOrCreate_CreatesFile_WhenMissing()
    {
        Assert.False(File.Exists(_tempPath));

        var editor = ConfigEditor.LoadOrCreate(_tempPath);

        Assert.True(File.Exists(_tempPath));
        Assert.Equal(_tempPath, editor.FilePath);
    }

    [Fact]
    public void SetDefaultProvider_PersistsValue()
    {
        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        editor.SetDefaultProvider("anthropic");
        editor.Save();

        var roundTrip = JsonNode.Parse(File.ReadAllText(_tempPath))!.AsObject();
        Assert.Equal("anthropic", roundTrip["defaultProvider"]!.GetValue<string>());
    }

    [Fact]
    public void SetCustomPromptTemplate_CreatesSectionIfMissing()
    {
        File.WriteAllText(_tempPath, "{ \"defaultProvider\": \"openai\" }");

        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        editor.SetCustomPromptTemplate("hello {instruction} {text}");
        editor.Save();

        var roundTrip = JsonNode.Parse(File.ReadAllText(_tempPath))!.AsObject();
        Assert.Equal("hello {instruction} {text}",
            roundTrip["customPrompt"]!["systemPromptTemplate"]!.GetValue<string>());
    }

    [Fact]
    public void Save_PreservesUnknownTopLevelKeys()
    {
        // Power user adds a key Turbophrase doesn't recognize. We must not
        // drop it on round-trip.
        File.WriteAllText(_tempPath, """
            {
              "defaultProvider": "openai",
              "experimental": { "futureFeature": true, "version": 7 },
              "providers": { }
            }
            """);

        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        editor.SetDefaultProvider("ollama");
        editor.Save();

        var roundTrip = JsonNode.Parse(File.ReadAllText(_tempPath))!.AsObject();
        Assert.Equal("ollama", roundTrip["defaultProvider"]!.GetValue<string>());
        Assert.NotNull(roundTrip["experimental"]);
        Assert.True(roundTrip["experimental"]!["futureFeature"]!.GetValue<bool>());
        Assert.Equal(7, roundTrip["experimental"]!["version"]!.GetValue<int>());
    }

    [Fact]
    public void Save_PreservesUnknownNestedKeys_InProviders()
    {
        File.WriteAllText(_tempPath, """
            {
              "defaultProvider": "openai",
              "providers": {
                "openai": { "type": "openai", "apiKey": "${OPENAI_API_KEY}", "customField": "preserve-me" }
              }
            }
            """);

        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        editor.SetDefaultProvider("openai");
        editor.Save();

        var roundTrip = JsonNode.Parse(File.ReadAllText(_tempPath))!.AsObject();
        var openai = roundTrip["providers"]!["openai"]!;
        Assert.Equal("preserve-me", openai["customField"]!.GetValue<string>());
        Assert.Equal("${OPENAI_API_KEY}", openai["apiKey"]!.GetValue<string>());
    }

    [Fact]
    public void SetNotifications_SerializesAllFieldsAsCamelCase()
    {
        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        editor.SetNotifications(new NotificationSettings
        {
            ShowOnStartup = false,
            ShowOnSuccess = false,
            ShowOnError = true,
            ShowOnConfigReload = false,
            ShowOnProviderChange = true,
            ShowProcessingOverlay = false,
            ShowProcessingAnimation = true,
        });
        editor.Save();

        var roundTrip = JsonNode.Parse(File.ReadAllText(_tempPath))!.AsObject();
        var n = roundTrip["notifications"]!.AsObject();
        Assert.False(n["showOnStartup"]!.GetValue<bool>());
        Assert.True(n["showOnError"]!.GetValue<bool>());
        Assert.True(n["showProcessingAnimation"]!.GetValue<bool>());
    }

    [Fact]
    public void SetLogging_SerializesAsCamelCase()
    {
        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        editor.SetLogging(new LoggingSettings { Enabled = true });
        editor.Save();

        var roundTrip = JsonNode.Parse(File.ReadAllText(_tempPath))!.AsObject();
        Assert.True(roundTrip["logging"]!["enabled"]!.GetValue<bool>());
    }

    [Fact]
    public void LoadOrCreate_ThrowsOnInvalidJson()
    {
        File.WriteAllText(_tempPath, "{ this is not json");

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigEditor.LoadOrCreate(_tempPath));
        Assert.Contains("not valid JSON", ex.Message);
    }

    [Fact]
    public void ResetToDefaults_CreatesBackup_WhenRequested()
    {
        File.WriteAllText(_tempPath, "{ \"defaultProvider\": \"custom\" }");

        var backup = ConfigEditor.ResetToDefaults(_tempPath, createBackup: true);

        Assert.NotNull(backup);
        Assert.True(File.Exists(backup));
        Assert.Contains("custom", File.ReadAllText(backup));

        var refreshed = File.ReadAllText(_tempPath);
        Assert.Contains("openai", refreshed);
    }

    [Fact]
    public void ResetToDefaults_SkipsBackup_WhenDisabled()
    {
        File.WriteAllText(_tempPath, "{ \"defaultProvider\": \"custom\" }");

        var backup = ConfigEditor.ResetToDefaults(_tempPath, createBackup: false);

        Assert.Null(backup);
    }

    [Fact]
    public void GetProviderNames_ReturnsDeclaredProviders()
    {
        File.WriteAllText(_tempPath, """
            {
              "providers": {
                "openai": { "type": "openai" },
                "anthropic": { "type": "anthropic" }
              }
            }
            """);

        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        var names = editor.GetProviderNames();

        Assert.Contains("openai", names);
        Assert.Contains("anthropic", names);
        Assert.Equal(2, names.Count);
    }

    [Fact]
    public void GetProviderRawField_ReturnsLiteralReference()
    {
        File.WriteAllText(_tempPath, """
            {
              "providers": {
                "openai": { "type": "openai", "apiKey": "@credman:openai:apiKey", "model": "gpt-4o" }
              }
            }
            """);

        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        Assert.Equal("@credman:openai:apiKey", editor.GetProviderRawField("openai", "apiKey"));
        Assert.Equal("gpt-4o", editor.GetProviderRawField("openai", "model"));
        Assert.Null(editor.GetProviderRawField("openai", "endpoint"));
        Assert.Null(editor.GetProviderRawField("missing", "apiKey"));
    }

    [Fact]
    public void SetProviderFields_PreservesUnknownKeys()
    {
        File.WriteAllText(_tempPath, """
            {
              "providers": {
                "openai": {
                  "type": "openai",
                  "apiKey": "old",
                  "customMargin": 42
                }
              }
            }
            """);

        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        editor.SetProviderFields("openai", new Dictionary<string, string?>
        {
            ["apiKey"] = "new-key",
            ["model"] = "gpt-4o",
        });
        editor.Save();

        var roundTrip = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_tempPath))!.AsObject();
        var openai = roundTrip["providers"]!["openai"]!;
        Assert.Equal("new-key", openai["apiKey"]!.GetValue<string>());
        Assert.Equal("gpt-4o", openai["model"]!.GetValue<string>());
        Assert.Equal(42, openai["customMargin"]!.GetValue<int>());
    }

    [Fact]
    public void SetProviderFields_NullValueRemovesKey()
    {
        File.WriteAllText(_tempPath, """
            { "providers": { "openai": { "type": "openai", "endpoint": "x" } } }
            """);

        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        editor.SetProviderFields("openai", new Dictionary<string, string?>
        {
            ["endpoint"] = null,
        });
        editor.Save();

        var roundTrip = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_tempPath))!.AsObject();
        var openai = roundTrip["providers"]!["openai"]!.AsObject();
        Assert.False(openai.ContainsKey("endpoint"));
    }

    [Fact]
    public void SetProviderFields_CreatesProviderEntryIfMissing()
    {
        File.WriteAllText(_tempPath, "{}");

        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        editor.SetProviderFields("anthropic", new Dictionary<string, string?>
        {
            ["type"] = "anthropic",
            ["apiKey"] = "@credman:anthropic:apiKey",
        });
        editor.Save();

        var roundTrip = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_tempPath))!.AsObject();
        var anthropic = roundTrip["providers"]!["anthropic"]!;
        Assert.Equal("anthropic", anthropic["type"]!.GetValue<string>());
        Assert.Equal("@credman:anthropic:apiKey", anthropic["apiKey"]!.GetValue<string>());
    }

    [Fact]
    public void RemoveProvider_DropsEntry()
    {
        File.WriteAllText(_tempPath, """
            { "providers": { "openai": { "type": "openai" }, "anthropic": { "type": "anthropic" } } }
            """);

        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        Assert.True(editor.RemoveProvider("openai"));
        Assert.False(editor.RemoveProvider("openai"));
        editor.Save();

        var roundTrip = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_tempPath))!.AsObject();
        Assert.False(roundTrip["providers"]!.AsObject().ContainsKey("openai"));
        Assert.True(roundTrip["providers"]!.AsObject().ContainsKey("anthropic"));
    }

    [Fact]
    public void SetPreset_WritesAllFieldsAsCamelCase()
    {
        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        editor.SetPreset("custom", new PromptPreset
        {
            Name = "Custom",
            SystemPrompt = "Do the thing.",
            Provider = "openai",
            PickerOrder = 5,
            IncludeInPicker = false,
        });
        editor.Save();

        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_tempPath))!.AsObject();
        var preset = root["presets"]!["custom"]!.AsObject();
        Assert.Equal("Custom", preset["name"]!.GetValue<string>());
        Assert.Equal("Do the thing.", preset["systemPrompt"]!.GetValue<string>());
        Assert.Equal("openai", preset["provider"]!.GetValue<string>());
        Assert.Equal(5, preset["pickerOrder"]!.GetValue<int>());
        Assert.False(preset["includeInPicker"]!.GetValue<bool>());
    }

    [Fact]
    public void RenamePreset_UpdatesHotkeyReferences()
    {
        File.WriteAllText(_tempPath, """
            {
              "hotkeys": [
                { "keys": "Ctrl+Shift+G", "preset": "grammar" },
                { "keys": "Ctrl+Shift+P", "preset": "paraphrase" }
              ],
              "presets": {
                "grammar":   { "name": "Fix Grammar",  "systemPrompt": "..." },
                "paraphrase":{ "name": "Paraphrase",  "systemPrompt": "..." }
              }
            }
            """);

        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        Assert.True(editor.RenamePreset("grammar", "fix-grammar"));
        editor.Save();

        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_tempPath))!.AsObject();
        var hotkeys = root["hotkeys"]!.AsArray();
        Assert.Equal("fix-grammar", hotkeys[0]!["preset"]!.GetValue<string>());
        Assert.Equal("paraphrase", hotkeys[1]!["preset"]!.GetValue<string>());
        Assert.True(root["presets"]!.AsObject().ContainsKey("fix-grammar"));
        Assert.False(root["presets"]!.AsObject().ContainsKey("grammar"));
    }

    [Fact]
    public void RenamePreset_NoOp_WhenSourceMissing()
    {
        File.WriteAllText(_tempPath, "{}");

        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        Assert.False(editor.RenamePreset("missing", "target"));
    }

    [Fact]
    public void RenamePreset_Throws_OnExistingTarget()
    {
        File.WriteAllText(_tempPath, """
            { "presets": { "a": {}, "b": {} } }
            """);

        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        Assert.Throws<InvalidOperationException>(() => editor.RenamePreset("a", "b"));
    }

    [Fact]
    public void SetHotkeys_ReplacesArray_AndStripsHelperBooleans()
    {
        File.WriteAllText(_tempPath, """
            { "hotkeys": [ { "keys": "Ctrl+Shift+G", "preset": "grammar" } ] }
            """);

        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        editor.SetHotkeys(new[]
        {
            new HotkeyBinding { Keys = "Ctrl+Alt+T", Action = "custom-prompt", Name = "Translate" },
            new HotkeyBinding { Keys = "Ctrl+F7", Action = "preset-picker", Name = "Choose" },
        });
        editor.Save();

        var hotkeys = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_tempPath))!["hotkeys"]!.AsArray();
        Assert.Equal(2, hotkeys.Count);
        var first = hotkeys[0]!.AsObject();
        Assert.Equal("Ctrl+Alt+T", first["keys"]!.GetValue<string>());
        Assert.Equal("custom-prompt", first["action"]!.GetValue<string>());
        Assert.False(first.ContainsKey("isCustomPromptAction"));
        Assert.False(first.ContainsKey("isPresetPickerAction"));
        Assert.False(first.ContainsKey("isPresetAction"));
        // Empty preset should be stripped:
        Assert.False(first.ContainsKey("preset"));
    }

    [Fact]
    public void SetPickerActions_WritesArray()
    {
        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        editor.SetPickerActions(new[]
        {
            new HotkeyBinding { Action = "custom-prompt", Name = "Shorten", IncludeInPicker = true, PickerOrder = 99 },
        });
        editor.Save();

        var actions = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_tempPath))!["pickerActions"]!.AsArray();
        Assert.Single(actions);
        var first = actions[0]!.AsObject();
        Assert.Equal("Shorten", first["name"]!.GetValue<string>());
        Assert.Equal(99, first["pickerOrder"]!.GetValue<int>());
    }

    [Fact]
    public void GetPresetRawField_ReadsScalarValues()
    {
        File.WriteAllText(_tempPath, """
            {
              "presets": {
                "grammar": {
                  "name": "Fix Grammar",
                  "systemPrompt": "Fix...",
                  "includeInPicker": true,
                  "pickerOrder": 3
                }
              }
            }
            """);

        var editor = ConfigEditor.LoadOrCreate(_tempPath);
        Assert.Equal("Fix Grammar", editor.GetPresetRawField("grammar", "name"));
        Assert.Equal("Fix...", editor.GetPresetRawField("grammar", "systemPrompt"));
        Assert.Equal("true", editor.GetPresetRawField("grammar", "includeInPicker"));
        Assert.Equal("3", editor.GetPresetRawField("grammar", "pickerOrder"));
        Assert.Null(editor.GetPresetRawField("grammar", "missing"));
        Assert.Null(editor.GetPresetRawField("missing", "name"));
    }
}
