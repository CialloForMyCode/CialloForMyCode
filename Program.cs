using CialloForMyCode;

// 交互式命令行：不带参数就停在这里等命令；带参数则照旧跑一次就退出。
if (args.Length > 0)
{
    Tetris.Run(args);
    return;
}

bool done = false;

// shell 的命令：注册在一处，分发查表。加命令只加一行，不用碰下面的循环。
(string Name, string Desc, Action<string[]> Run)[] Commands = [];   // 先置空，下面的 help 要回头遍历它

Commands =
[
    ("tetris", "俄罗斯方块", Tetris.Cmd),
    ("help", "看这个", _ => ShowHelp(Commands)),
    ("clear", "清屏", _ => { try { Console.Clear(); } catch { } }),
    ("exit", "退出", _ => { done = true; }),
];

Console.WriteLine("输 help 看命令，exit 退出。");
Console.WriteLine();

while (!done)
{
    Console.Write("CialloForMyCode> ");
    Console.ResetColor();

    string? line = Console.ReadLine();
    if (line == null)
    {
        Console.WriteLine();
        break;
    }

    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0) continue;

    string name = parts[0];
    int hit = Array.FindIndex(Commands, c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    if (hit < 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"没有 {name} 这个命令，输 help 看看。");
        Console.ResetColor();
        continue;
    }

    Commands[hit].Run(parts[1..]);
}

void ShowHelp((string Name, string Desc, Action<string[]> Run)[] cmds)
{
    Console.WriteLine("可用命令：");
    foreach (var (n, desc, _) in cmds)
        Console.WriteLine($"  {n,-8}{desc}");
}