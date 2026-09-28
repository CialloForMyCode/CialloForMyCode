using System.Diagnostics;

namespace CialloForMyCode;

internal static class Tetris
{
    private const int Width = 10;
    private const int Height = 20;
    private const int Box = 4;   // 方块都放在 4x4 的框里

    private static readonly string[][] Shapes =
    [
        ["....", "####", "....", "...."],  // I
        ["....", ".##.", ".##.", "...."],  // O
        ["....", "###.", ".#..", "...."],  // T
        ["....", ".##.", "##..", "...."],  // S
        ["....", "##..", ".##.", "...."],  // Z
        ["....", ".#..", "###.", "...."],  // J
        ["....", "..#.", "###.", "...."],  // L
    ];

    private static readonly ConsoleColor[] Colors =
    [
        ConsoleColor.Cyan, ConsoleColor.Yellow, ConsoleColor.Magenta,
        ConsoleColor.Green, ConsoleColor.Red, ConsoleColor.Blue, ConsoleColor.DarkYellow,
    ];

    private static readonly Random Rng = new();

    private static bool _rich = true;

    private sealed class Piece
    {
        public int Shape;
        public bool[,] Cells = new bool[Box, Box];
        public int X, Y;
    }

    private sealed class Game
    {
        public int[,] Board = new int[Height, Width];
        public Piece Piece = null!;
        public int NextShape;
        public int NextTurns;
        public int Score, Lines, Level = 1;
        public bool Paused, Over;
        public string Message = "";

        public Game()
        {
            NextShape = Rng.Next(Shapes.Length);
            NextTurns = Rng.Next(4);
            Piece = Spawn(NextShape, NextTurns);
            NextShape = Rng.Next(Shapes.Length);
            NextTurns = Rng.Next(4);
        }
    }

    // tetris 命令认哪些参数：注册在一处，switch 照它分发，help 照它打
    private static readonly (string Name, string Alias, string Desc)[] Options =
    [
        ("-p", "--play", "玩俄罗斯方块"),
        ("-d", "--demo", "自动演示一局"),
        ("-t", "--test", "跑一遍自检"),
        ("-h", "--help", "看参数"),
    ];

    public static void Cmd(string[] args)
    {
        if (args.Length == 0)
        {
            Help();
            return;
        }

        foreach (string raw in args)
        {
            switch (raw.ToLowerInvariant())
            {
                case "-p":
                case "--play":
                    Run(["--play"]);
                    break;
                case "-d":
                case "--demo":
                    Run(["--demo"]);
                    break;
                case "-t":
                case "--test":
                    Run(["--test"]);
                    break;
                case "-h":
                case "--help":
                    Help();
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"tetris：不认识 {raw}");
                    Console.ResetColor();
                    Help();
                    return;
            }
        }
    }

    private static void Help()
    {
        foreach (var (name, alias, desc) in Options)
            Console.WriteLine($"tetris {name} | {alias}    {desc}");
    }

    public static void Run(string[] args)
    {
        bool demo = args.Any(a => a.Equals("--demo", StringComparison.OrdinalIgnoreCase));

        if (args.Any(a => a.Equals("--test", StringComparison.OrdinalIgnoreCase)))
        {
            SelfCheck(); return;
        }

        if (Console.IsInputRedirected && !demo)
        {
            Console.WriteLine("俄罗斯方块要用键盘玩，这里拿不到键盘输入。");
            Console.WriteLine("想看它跑起来：dotnet run -- --demo");
            return;
        }

        _rich = !Console.IsOutputRedirected;
        if (!PrepareConsole()) return;

        Console.CancelKeyPress += (_, _) => RestoreConsole();

        try
        {
            if (demo) PlayDemo();
            else PlayGame();
        }
        finally
        {
            RestoreConsole();
        }
    }

    private static bool PrepareConsole()
    {
        try
        {
            int needWidth = Width * 2 + 36;
            if (_rich && (Console.WindowWidth < needWidth || Console.WindowHeight < Height + 4))
            {
                Console.WriteLine($"这个游戏要一点地方：请把窗口拉到至少 {needWidth} x {Height + 4}"
                                  + $"（现在是 {Console.WindowWidth} x {Console.WindowHeight}）。");
                Console.WriteLine("按 Alt+Enter 可以全屏。");
                return false;
            }

            if (_rich)
            {
                Console.CursorVisible = false;
                Console.Clear();
            }
            return true;
        }
        catch
        {
            return true; // 硬跑
        }
    }

    private static void RestoreConsole()
    {
        Console.ResetColor();
        try { Console.CursorVisible = true; } catch { }
        if (_rich)
            try { Console.SetCursorPosition(0, Height + 1); } catch { }
    }

    // ---------- 方块 ----------

    private static bool[,] BuildCells(int shape)
    {
        var grid = new bool[Box, Box];
        for (int r = 0; r < Box; r++)
            for (int c = 0; c < Box; c++)
                grid[r, c] = Shapes[shape][r][c] == '#';
        return grid;
    }

    private static bool[,] Rotate(bool[,] cells)
    {
        // 顺时针滚转
        var grid = new bool[Box, Box];
        for (int r = 0; r < Box; r++)
            for (int c = 0; c < Box; c++)
                grid[r, c] = cells[Box - 1 - c, r];
        return grid;
    }

    private static bool[,] RotateBack(bool[,] cells)
    {
        // 逆时针滚转
        var grid = new bool[Box, Box];
        for (int r = 0; r < Box; r++)
            for (int c = 0; c < Box; c++)
                grid[r, c] = cells[c, Box - 1 - r];
        return grid;
    }

    private static Piece Spawn(int shape, int turns)
    {
        var cells = BuildCells(shape);
        for (int i = 0; i < turns; i++)   // 初始朝向也随机
            cells = Rotate(cells);

        return new Piece
        {
            Shape = shape,
            Cells = cells,
            X = (Width - Box) / 2,
            Y = -1,
        };
    }

    private static bool Overlaps(Piece p, int x, int y)
    {
        int c = x - p.X, r = y - p.Y;
        return c >= 0 && c < Box && r >= 0 && r < Box && p.Cells[r, c];
    }

    private static bool Collides(int[,] board, Piece p, int dx = 0, int dy = 0, bool[,]? cells = null)
    {
        var cs = cells ?? p.Cells;
        for (int r = 0; r < Box; r++)
            for (int c = 0; c < Box; c++)
            {
                if (!cs[r, c]) continue;
                int x = p.X + dx + c, y = p.Y + dy + r;
                if (x < 0 || x >= Width || y >= Height) return true;
                if (y >= 0 && board[y, x] != 0) return true;
            }
        return false;
    }

    private static bool TryMove(int[,] board, Piece p, int dx, int dy)
    {
        if (Collides(board, p, dx, dy)) return false;
        p.X += dx;
        p.Y += dy;
        return true;
    }

    private static bool TryRotate(int[,] board, Piece p, bool clockwise = true)
    {
        var rotated = clockwise ? Rotate(p.Cells) : RotateBack(p.Cells);

        foreach (int kick in new[] { 0, -1, 1, -2, 2 })
        {
            if (Collides(board, p, kick, 0, rotated))
                continue;
            p.Cells = rotated;
            p.X += kick;
            return true;
        }
        return false;
    }

    // ---------- 规则 ----------

    private static int FallDelay(int level) => Math.Max(100, 700 - (level - 1) * 60);

    private static int ScoreFor(int cleared, int level) => cleared switch
    {
        1 => 100 * level,
        2 => 300 * level,
        3 => 500 * level,
        4 => 800 * level,
        _ => 0,
    };

    private static int ClearFullLines(int[,] board)
    {
        int cleared = 0;
        for (int y = Height - 1; y >= 0; y--)
        {
            bool full = true;
            for (int x = 0; x < Width; x++)
                if (board[y, x] == 0) { full = false; break; }
            if (!full) continue;

            for (int row = y; row > 0; row--)
                for (int x = 0; x < Width; x++)
                    board[row, x] = board[row - 1, x];

            for (int x = 0; x < Width; x++)
                board[0, x] = 0;

            cleared++;
            y++;
        }
        return cleared;
    }

    private static void Lock(Game g)
    {
        for (int r = 0; r < Box; r++)
            for (int c = 0; c < Box; c++)
            {
                if (!g.Piece.Cells[r, c]) continue;
                int x = g.Piece.X + c, y = g.Piece.Y + r;
                if (y < 0) { g.Over = true; continue; }
                g.Board[y, x] = g.Piece.Shape + 1;
            }

        int cleared = ClearFullLines(g.Board);
        if (cleared > 0)
        {
            g.Score += ScoreFor(cleared, g.Level);
            g.Lines += cleared;
            g.Level = 1 + g.Lines / 10;
        }

        g.Piece = Spawn(g.NextShape, g.NextTurns);
        g.NextShape = Rng.Next(Shapes.Length);
        g.NextTurns = Rng.Next(4);

        if (Collides(g.Board, g.Piece))
            g.Over = true;
    }

    // ---------- 两种跑法 ----------

    private static void PlayGame()
    {
        var g = new Game();
        var clock = Stopwatch.StartNew();
        long lastFall = 0;
        int lastRender = -1000;

        while (!g.Over)
        {
            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;

                if (key == ConsoleKey.Escape) return;
                if (key == ConsoleKey.P) { g.Paused = !g.Paused; continue; }
                if (g.Paused) continue;

                switch (key)
                {
                    case ConsoleKey.A:
                        TryMove(g.Board, g.Piece, -1, 0);
                        break;
                    case ConsoleKey.D:
                        TryMove(g.Board, g.Piece, 1, 0);
                        break;
                    case ConsoleKey.Q:
                        TryRotate(g.Board, g.Piece, clockwise: false);
                        break;
                    case ConsoleKey.E:
                        TryRotate(g.Board, g.Piece, clockwise: true);
                        break;
                    case ConsoleKey.S:
                        if (TryMove(g.Board, g.Piece, 0, 1)) g.Score += 1;
                        lastFall = clock.ElapsedMilliseconds;
                        break;
                    case ConsoleKey.Spacebar:
                        while (TryMove(g.Board, g.Piece, 0, 1)) g.Score += 2;
                        Lock(g);
                        lastFall = clock.ElapsedMilliseconds;
                        break;
                }
            }

            int now = (int)clock.ElapsedMilliseconds;
            if (!g.Paused && now - lastFall >= FallDelay(g.Level))
            {
                if (!TryMove(g.Board, g.Piece, 0, 1)) Lock(g);
                lastFall = now;
            }

            if (now - lastRender >= 33)
            {
                Render(g);
                lastRender = now;
            }

            Thread.Sleep(4);
        }

        g.Message = $"游戏结束 —— 分数 {g.Score}，共消 {g.Lines} 行，等级 {g.Level}";
        Render(g);
    }

    private static void PlayDemo()
    {
        var g = new Game();
        int pieces = 0;

        if (_rich) Console.Clear();

        while (!g.Over && pieces < 200)
        {
            pieces++;

            if (Rng.Next(3) == 0) TryRotate(g.Board, g.Piece);

            var heights = new int[Width];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (g.Board[y, x] != 0) { heights[x] = Height - y; break; }

            int minHeight = int.MaxValue;
            for (int x = 0; x < Width; x++)
                if (heights[x] < minHeight) minHeight = heights[x];

            int target = Rng.Next(Width);
            for (int i = 0; i < Width; i++)
            {
                int x = (target + i) % Width;
                if (heights[x] == minHeight) { target = x; break; }
            }

            int dx = target - Box / 2 - g.Piece.X;
            int step = Math.Sign(dx);
            for (int i = 0; i < Math.Abs(dx); i++)
                if (!TryMove(g.Board, g.Piece, step, 0)) break;

            while (TryMove(g.Board, g.Piece, 0, 1))
            {
                if (_rich)
                {
                    Render(g);
                    Thread.Sleep(25);
                }
            }
            Lock(g);

            if (_rich) Render(g);
        }

        Render(g);
        Console.ResetColor();
        Console.WriteLine($"demo：一共放下 {pieces} 块，分数 {g.Score}，消行 {g.Lines}，等级 {g.Level}"
                          + (g.Over ? "，堆到顶了" : ""));
    }

    // ---------- 自检 ----------

    private static void SelfCheck()
    {
        bool okShapes = true;
        for (int s = 0; s < Shapes.Length; s++)
        {
            var cells = BuildCells(s);
            int n = 0;
            for (int r = 0; r < Box; r++)
                for (int c = 0; c < Box; c++)
                    if (cells[r, c]) n++;

            int want = 4;
            bool connected = IsConnected(cells);
            if (n != want || !connected)
            {
                Console.WriteLine($"第 {s} 种方块不对：{n} 格（期望 {want}），连通={connected}");
                okShapes = false;
            }
        }
        Console.WriteLine($"{Shapes.Length} 种方块形状（都是 4 格、都连通）"
                          + (okShapes ? "OK" : "不对"));

        var g = new Game();
        for (int x = 0; x < Width; x++) g.Board[Height - 1, x] = 1;

        g.Piece = new Piece { Shape = 0, Cells = BuildCells(0), X = 0, Y = 0 };
        int lo = Box, hi = -1;
        for (int r = 0; r < Box; r++)
            for (int c = 0; c < Box; c++)
                if (g.Piece.Cells[r, c]) { lo = Math.Min(lo, c); hi = Math.Max(hi, c); }
        for (int c = lo; c <= hi; c++) g.Board[Height - 1, c] = 0;
        g.Board[Height - 2, Width - 1] = 2;

        while (TryMove(g.Board, g.Piece, 0, 1)) { }
        Lock(g);

        bool okLines = g.Lines == 1;
        bool okScore = g.Score == 100;
        bool okDrop = g.Board[Height - 1, Width - 1] == 2;
        bool okClean = g.Board[Height - 1, hi] == 0;

        Console.WriteLine($"补满底行 → 消行 {g.Lines}（期望 1）{(okLines ? " OK" : " 不对")}");
        Console.WriteLine($"           得分 {g.Score}（期望 100）{(okScore ? " OK" : " 不对")}");
        Console.WriteLine($"           上方那格掉到底行 {(okDrop ? "OK" : "不对")}");
        Console.WriteLine($"           底行已清空 {(okClean ? "OK" : "不对")}");
        Console.WriteLine(okLines && okScore && okDrop && okClean && okShapes ? "自检通过。" : "自检不通过。");
    }

    private static bool IsConnected(bool[,] cells)
    {
        int total = 0, sr = -1, sc = -1;
        for (int r = 0; r < Box; r++)
            for (int c = 0; c < Box; c++)
                if (cells[r, c]) { total++; if (sr < 0) { sr = r; sc = c; } }

        if (total == 0) return false;

        var seen = new bool[Box, Box];
        var stack = new Stack<(int R, int C)>();
        stack.Push((sr, sc));
        seen[sr, sc] = true;
        int reached = 0;

        while (stack.Count > 0)
        {
            var (r, c) = stack.Pop();
            reached++;
            foreach (var (dr, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                int nr = r + dr, nc = c + dc;
                if (nr < 0 || nr >= Box || nc < 0 || nc >= Box) continue;
                if (!cells[nr, nc] || seen[nr, nc]) continue;
                seen[nr, nc] = true;
                stack.Push((nr, nc));
            }
        }
        return reached == total;
    }

    // ---------- 画面 ----------

    private static void Render(Game g)
    {
        var preview = BuildCells(g.NextShape);
        for (int i = 0; i < g.NextTurns; i++) preview = Rotate(preview);

        if (_rich)
        {
            try { Console.SetCursorPosition(0, 0); }
            catch { _rich = false; }
        }

        for (int y = 0; y < Height; y++)
        {
            if (_rich) Console.ResetColor();
            Console.Write("|");

            for (int x = 0; x < Width; x++)
            {
                int v = g.Board[y, x];
                if (v == 0 && Overlaps(g.Piece, x, y)) v = g.Piece.Shape + 1;
                DrawCell(v);
            }

            if (_rich) Console.ResetColor();
            Console.Write("|  ");

            if (y >= 1 && y <= Box)
            {
                for (int c = 0; c < Box; c++)
                    DrawCell(preview[y - 1, c] ? g.NextShape + 1 : 0);
                Console.Write("    ");
            }
            else
            {
                Console.Write(HudText(g, y).PadRight(14));
            }

            if (_rich) Console.ResetColor();
            Console.WriteLine();
        }

        if (!_rich) Console.WriteLine();
    }

    private static void DrawCell(int v)
    {
        if (_rich)
        {
            if (v == 0)
            {
                Console.ResetColor();
                Console.Write("  ");
            }
            else
            {
                Console.BackgroundColor = Colors[(v - 1) % Colors.Length];
                Console.Write("  ");
                Console.ResetColor();
            }
        }
        else
        {
            Console.Write(v == 0 ? " ." : "[]");
        }
    }

    private static string HudText(Game g, int y) => y switch
    {
        0 => "下一块",
        6 => $"分数 {g.Score}",
        8 => $"消行 {g.Lines}",
        10 => $"等级 {g.Level}",
        12 => "A D   移动",
        13 => "Q E   旋转",
        14 => "S     加速",
        15 => "空格  落底",
        16 => "P     暂停",
        17 => "Esc   退出",
        18 => g.Paused ? "== 已暂停 ==" : g.Message,
        _ => "",
    };
}
