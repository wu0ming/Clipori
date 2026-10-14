using Clipori.Windows.Services;
using Clipori.Windows.Interop;
using System.Runtime.InteropServices;

static void Check(bool condition, string reason)
{
    if (!condition) throw new Exception("Regression failed: " + reason);
    Console.WriteLine("PASS: " + reason);
}

var python = """
#!/usr/bin/python3
l = []
for i in range(3):
    x = int(input('integer:\n'))
    l.append(x)
l.sort()
print(l)
""";
Check(ClipboardClassifier.Classify(python) == "code", "Python for/range/list example detected as code");
Check(ClipboardClassifier.Classify("def hello(name):\n    print(name)") == "code", "Short Python snippet detected");
Check(ClipboardClassifier.Classify("using System;\nclass A {\n  public void F() { }\n}") == "code", "C# snippet detected");
Check(ClipboardClassifier.Classify("https://example.com/path") == "link", "URL recognized");
Check(ClipboardClassifier.Classify("我希望周一上午安排一次会议，请问可以吗？") == "text", "Chinese prose not code");
string secret = "用于测试的中文内容 ☕";
string encrypted = ProtectionForTests.ProtectString(secret);
Check(encrypted != secret && ProtectionForTests.UnprotectString(encrypted) == secret, "DPAPI round trip");

Check(OperatingSystem.IsWindows(), "Win32 P/Invoke smoke checks run on Windows");
Check(NativeLibrary.TryLoad("kernel32.dll", out var kernel), "kernel32.dll loads");
try
{
    Check(NativeLibrary.TryGetExport(kernel, "GetCurrentThreadId", out _),
        "GetCurrentThreadId export belongs to kernel32.dll");
}
finally { NativeLibrary.Free(kernel); }
Check(NativeMethods.GetCurrentThreadId() > 0,
    "GetCurrentThreadId P/Invoke resolves and returns a valid thread ID");
Check(!NativeMethods.RestoreForegroundAndFocus(IntPtr.Zero, IntPtr.Zero),
    "Missing target window must not receive injected paste");
Console.WriteLine("All Clipori Windows regression checks passed.");
