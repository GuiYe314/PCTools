using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace JuDianWorkbench.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x4A57;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private IntPtr _windowHandle;
    private HwndSource? _source;
    private Action? _pressed;
    private HotkeyDefinition? _registeredDefinition;

    public bool IsRegistered => _registeredDefinition is not null;

    public void Attach(Window window, Action pressed)
    {
        if (_source is not null) throw new InvalidOperationException("全局快捷键服务已经连接到窗口。");
        _windowHandle = new WindowInteropHelper(window).Handle;
        if (_windowHandle == IntPtr.Zero) throw new InvalidOperationException("无法获取主窗口句柄。");
        _source = HwndSource.FromHwnd(_windowHandle) ?? throw new InvalidOperationException("无法连接主窗口消息。");
        _pressed = pressed;
        _source.AddHook(WindowMessageHook);
    }

    public bool TryRegister(string gesture, out string? error)
    {
        if (!TryParse(gesture, out var definition, out error)) return false;
        if (_source is null)
        {
            error = "主窗口尚未准备好，请稍后重试。";
            return false;
        }

        var previous = _registeredDefinition;
        Unregister();
        if (RegisterDefinition(definition, out error)) return true;
        if (previous is not null) RegisterDefinition(previous.Value, out _);
        return false;
    }

    public void Unregister()
    {
        if (_registeredDefinition is null || _windowHandle == IntPtr.Zero) return;
        UnregisterHotKey(_windowHandle, HotkeyId);
        _registeredDefinition = null;
    }

    public static bool TryCreateGesture(ModifierKeys modifiers, Key key, out string gesture, out string? error)
    {
        modifiers &= ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows;
        gesture = string.Empty;
        if (modifiers == ModifierKeys.None)
        {
            error = "快捷键至少要包含 Ctrl、Alt、Shift 或 Win 中的一个。";
            return false;
        }
        if (!IsUsablePrimaryKey(key))
        {
            error = "请在组合键中再按一个字母、数字或功能键。";
            return false;
        }
        int virtualKey;
        try { virtualKey = KeyInterop.VirtualKeyFromKey(key); }
        catch (ArgumentException)
        {
            error = "这个按键不能注册为全局快捷键。";
            return false;
        }
        if (virtualKey == 0)
        {
            error = "这个按键不能注册为全局快捷键。";
            return false;
        }

        gesture = FormatGesture(modifiers, key);
        error = null;
        return true;
    }

    public static bool TryNormalizeGesture(string gesture, out string normalized, out string? error)
    {
        if (!TryParse(gesture, out var definition, out error))
        {
            normalized = string.Empty;
            return false;
        }
        normalized = FormatGesture(definition.Modifiers, definition.Key);
        return true;
    }

    public void Dispose()
    {
        Unregister();
        if (_source is not null) _source.RemoveHook(WindowMessageHook);
        _source = null;
        _pressed = null;
        _windowHandle = IntPtr.Zero;
    }

    private bool RegisterDefinition(HotkeyDefinition definition, out string? error)
    {
        var modifiers = ToNativeModifiers(definition.Modifiers) | ModNoRepeat;
        if (!RegisterHotKey(_windowHandle, HotkeyId, modifiers, (uint)KeyInterop.VirtualKeyFromKey(definition.Key)))
        {
            var nativeError = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            error = $"无法使用该快捷键，可能已被 Windows 或其他软件占用。（{nativeError}）";
            return false;
        }
        _registeredDefinition = definition;
        error = null;
        return true;
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _pressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    private static bool TryParse(string gesture, out HotkeyDefinition definition, out string? error)
    {
        definition = default;
        if (string.IsNullOrWhiteSpace(gesture))
        {
            error = "请先在输入框中按下要使用的组合键。";
            return false;
        }

        var modifiers = ModifierKeys.None;
        Key? primaryKey = null;
        foreach (var rawPart in gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = rawPart.Replace(" ", string.Empty, StringComparison.Ordinal);
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase)) { modifiers |= ModifierKeys.Control; continue; }
            if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) { modifiers |= ModifierKeys.Alt; continue; }
            if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) { modifiers |= ModifierKeys.Shift; continue; }
            if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase)) { modifiers |= ModifierKeys.Windows; continue; }
            if (primaryKey is not null || !TryParseKey(part, out var key))
            {
                error = "快捷键格式无效，请重新录入。";
                return false;
            }
            primaryKey = key;
        }

        if (primaryKey is null)
        {
            error = "快捷键需要包含一个字母、数字或功能键。";
            return false;
        }
        if (!TryCreateGesture(modifiers, primaryKey.Value, out _, out error)) return false;
        definition = new HotkeyDefinition(modifiers, primaryKey.Value);
        return true;
    }

    private static bool TryParseKey(string value, out Key key)
    {
        if (value.Length == 1 && value[0] is >= '0' and <= '9')
        {
            key = (Key)((int)Key.D0 + value[0] - '0');
            return true;
        }
        return Enum.TryParse(value, true, out key) && Enum.IsDefined(key);
    }

    private static bool IsUsablePrimaryKey(Key key) => key is not (
        Key.None or Key.System or Key.ImeProcessed or Key.DeadCharProcessed or
        Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl or
        Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.Clear);

    private static string FormatGesture(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key is >= Key.D0 and <= Key.D9 ? ((int)key - (int)Key.D0).ToString() : key.ToString());
        return string.Join(" + ", parts);
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        var result = 0u;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= ModAlt;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= ModControl;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= ModShift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= ModWin;
        return result;
    }

    private readonly record struct HotkeyDefinition(ModifierKeys Modifiers, Key Key);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
