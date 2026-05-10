using System;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.ViewModels;

public sealed class EquipmentBinderToolHostContext
{
    public required Func<string> CurrentFolderPathProvider { get; init; }
    public required Func<MulSlotEntry?> SelectedMulSlotProvider { get; init; }
    public Action<string>? StatusCallback { get; init; }
    public Action? ApplySuccessCallback { get; init; }
}
