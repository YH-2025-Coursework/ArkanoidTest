using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;

class Program
{
    const int W = 60, H = 24;
    const int PaddleW = 9, TopMargin = 2;

    static int paddleX = (W - PaddleW) / 2, paddleY = H - 2;

    static int ballX = W / 2, ballY = H / 2, dx = 1, dy = -1;
    static bool[,] bricks;
    static bool running = true;
    static int ballTick;


    static void Main()
    {
        Console.CursorVisible = false;
        Console.OutputEncoding = Encoding.UTF8;
        Console.TreatControlCAsInput = true;

        InitBricks(cols: 10, rows: 5);
        var sw = new Stopwatch();
        var targetDt = TimeSpan.FromMilliseconds(33); // ~30 FPS

        // Pre-size console for a clean area
        try { Console.SetWindowSize(Math.Max(Console.WindowWidth, W + 2), Math.Max(Console.WindowHeight, H + 2)); } catch { }

        sw.Start();
        var last = sw.Elapsed;

        while (running)
        {
            // Fixed timestep
            var now = sw.Elapsed;
            while (now - last >= targetDt)
            {
                Input();
                Update();
                last += targetDt;
            }
            Render();
            var sleep = targetDt - (sw.Elapsed - now);
            if (sleep > TimeSpan.Zero) Thread.Sleep(sleep);
        }

        Console.SetCursorPosition(0, H + 1);
        Console.CursorVisible = true;
    }

    static void InitBricks(int cols, int rows)
    {
        bricks = new bool[cols, rows];
        for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                bricks[c, r] = true;
    }

    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    static bool IsKeyDown(int vKey) => (GetAsyncKeyState(vKey) & 0x8000) != 0;

    const int VK_LEFT = 0x25, VK_RIGHT = 0x27, VK_ESCAPE = 0x1B;

    static void Input()
    {
        // Drain any buffered keypresses to avoid lag buildup
        while (Console.KeyAvailable) Console.ReadKey(true);

        int speed = 2; // tweak as needed
        if (IsKeyDown(VK_LEFT)) paddleX = Math.Max(1, paddleX - speed);
        if (IsKeyDown(VK_RIGHT)) paddleX = Math.Min(W - PaddleW - 1, paddleX + speed);
        if (IsKeyDown(VK_ESCAPE)) running = false;
    }

    static void Update()
    {
        // slow ball: run logic 1/3 ticks
        ballTick++;
        if (ballTick % 3 != 0) return;

        int nx = ballX + dx;
        int ny = ballY + dy;

        // walls (horizontal)
        if (nx <= 1 || nx >= W - 2)
        {
            dx = -dx;
            nx = ballX + dx;
        }

        // walls (top)
        if (ny <= TopMargin)
        {
            dy = -dy;
            ny = ballY + dy;
        }

        // paddle
        if (dy > 0 &&
            ny >= paddleY &&
            nx >= paddleX && nx < paddleX + PaddleW)
        {
            dy = -dy;
            // vary outgoing horizontal speed based on where the paddle was hit
            int hitPos = Math.Clamp(nx - paddleX, 0, PaddleW - 1);
            int newDx = Math.Clamp(hitPos - PaddleW / 2, -2, 2);
            if (newDx == 0)
                newDx = (ballX < W / 2) ? 1 : -1;
            dx = newDx;

            ny = paddleY - 1;
        }

        // ===== per-axis brick collision =====

        // X-axis
        if (nx != ballX)
        {
            var (hitX, cx, rx) = BrickAt(nx, ballY);
            if (hitX)
            {
                bricks[cx, rx] = false;
                dx = -dx;
                nx = ballX + dx;
            }
        }

        // Y-axis (use new nx)
        if (ny != ballY)
        {
            var (hitY, cy, ry) = BrickAt(nx, ny);
            if (hitY)
            {
                bricks[cy, ry] = false;
                dy = -dy;
                ny = ballY + dy;
            }
        }

        // ==================================

        ballX = nx;
        ballY = ny;

        // temporary: reflect from bottom instead of game over
        if (ballY >= H - 2)
        {
            dy = -dy;
            ballY = H - 3;
        }

        if (AllBricksCleared())
            running = false;
    }


    static (bool hit, int c, int r) BrickAt(int x, int y)
    {
        int cols = bricks.GetLength(0), rows = bricks.GetLength(1);
        int brickTop = TopMargin + 1, brickBottom = TopMargin + 1 + rows;
        if (y < brickTop || y >= brickBottom) return (false, -1, -1);

        int r = y - brickTop;
        // EXACTLY the same mapping used in Render():
        int c = (x - 1) * cols / (W - 2);
        c = Math.Clamp(c, 0, cols - 1);

        return (bricks[c, r], c, r);
    }


    static (bool hit, bool reflectX, bool reflectY) HitBrick(int nx, int ny)
    {
        var (hit, c, r) = BrickAt(nx, ny);
        if (!hit) return (false, false, false);

        bricks[c, r] = false;
        // keep your current simple vertical reflection
        return (true, false, true);
    }


    static bool AllBricksCleared()
    {
        int cols = bricks.GetLength(0);
        int rows = bricks.GetLength(1);
        for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                if (bricks[c, r]) return false;
        return true;
    }

    static void Render()
    {
        var sb = new StringBuilder((W + 1) * (H + 1));
        // Frame top line
        sb.Append('┌'); sb.Append('─', W - 2); sb.Append('┐').Append('\n');
        for (int y = 1; y < H - 1; y++)
        {
            sb.Append('│');
            for (int x = 1; x < W - 1; x++)
            {
                char ch = ' ';
                // Bricks
                int cols = bricks.GetLength(0), rows = bricks.GetLength(1);
                int brickTop = TopMargin + 1, brickBottom = TopMargin + 1 + rows;
                if (y >= brickTop && y < brickBottom)
                {
                    int r = y - brickTop;
                    int c = (x - 1) * cols / (W - 2);
                    if (bricks[c, r]) ch = '█';
                }
                // Paddle
                if (y == paddleY && x >= paddleX && x < paddleX + PaddleW) ch = '█';
                // Ball
                if (x == ballX && y == ballY) ch = '●';

                sb.Append(ch);
            }
            sb.Append('│').Append('\n');
        }
        // Frame bottom
        sb.Append('└'); sb.Append('─', W - 2); sb.Append('┘');

        Console.SetCursorPosition(0, 0);
        Console.Write(sb.ToString());
    }
}
