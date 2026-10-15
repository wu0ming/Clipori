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
Check(IntPtr.Size == 8, "CI target is Windows x64");
Check(Marshal.SizeOf<NativeMethods.INPUT>() == 40, "SendInput INPUT has the required x64 40-byte ABI size");
Check(Marshal.OffsetOf<NativeMethods.INPUT>(nameof(NativeMethods.INPUT.ki)).ToInt32() == 8,
    "Win32 KEYBDINPUT union starts at the required x64 offset");
Check(ShortcutService.TryParse("Ctrl+F", out _), "Local shortcut Ctrl+F is valid");
Check(ShortcutService.HasConflict("Ctrl+F", "Ctrl+F", "Ctrl+T"), "Duplicate shortcuts rejected");
Check(!ShortcutService.HasConflict("Ctrl+F", "Ctrl+N", "Ctrl+T"), "Distinct shortcuts accepted");
Check(ShortcutService.DescribeGlobal(3, 0x56) == "Ctrl+Alt+V", "Global shortcut display");

var search = new SearchEngine();
var entries = new[] {
    new Clipori.Windows.Models.ClipEntry { Text = "这段说明其实不是很清楚", Title = "中文说明" },
    new Clipori.Windows.Models.ClipEntry { Text = "The browser is running", Title = "Chrome browser" },
    new Clipori.Windows.Models.ClipEntry { Text = "可持续发展经理招聘", Title = "招聘启事" },
};
search.Rebuild(entries);
Check(search.Search("不清楚", "all").Any(x => x.Entry == entries[0]), "Chinese fuzzy subsequence: 不清楚 -> 不是很清楚");
Check(search.Search("borwser", "all").Any(x => x.Entry == entries[1]), "English edit-distance search tolerates typos");
Check(search.Search("可持续", "all").First().Entry == entries[2], "Exact Chinese substring ranks first");
Check(search.Search("可持续", "image").Count == 0, "Search respects content type filter");
Check(search.Search("", "all", 2).Count == 2, "Unfiltered search honors result rendering limit");

var generated = Enumerable.Range(0, 900).Select(i =>
    new Clipori.Windows.Models.ClipEntry { Title = "entry-" + i, Text = "This is a random clipboard history entry " + i }).ToList();
search.Rebuild(generated);
var sw = System.Diagnostics.Stopwatch.StartNew();
search.Search("entrie", "all");
sw.Stop();
Console.WriteLine("Search 900 entries (fuzzy): " + sw.ElapsedMilliseconds + " ms");
Check(sw.ElapsedMilliseconds < 10000, "Search completes within coarse CI performance guard");
Console.WriteLine("All Clipori Windows regression checks passed.");
