using Clipori.Windows.Services;

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
Console.WriteLine("All Clipori Windows regression checks passed.");
