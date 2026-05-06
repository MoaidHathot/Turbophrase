using Turbophrase.Core.Configuration;

namespace Turbophrase.Services;

public sealed record PickerOperation(string Id, string DisplayName, HotkeyBinding Binding)
{
    public int Number { get; init; }

    public override string ToString() => Number > 0 ? $"{Number}. {DisplayName}" : DisplayName;
}
