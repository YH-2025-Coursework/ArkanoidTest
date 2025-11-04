using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;

class Program
{
    const int W = 60, H = 24;
    const int TopMargin = 2;

    const int PaddleW = 9;
    static int paddleX = (W - PaddleW) / 2;
    static int paddleTopY = H - 3;         // top visible row
    static int PaddleLowerY => paddleTopY + 1; // collision row (also drawn)


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
        int nextX = ballX + dx;
        int nextY = ballY + dy;

        {
            ballTick++;
            if (ballTick % 3 != 0) return; // skip every second update -> slower ball
        }

            // Walls
            if (nextX <= 1 || nextX >= W - 2) { dx = -dx; nextX = ballX + dx; }
        if (nextY <= TopMargin) { dy = -dy; nextY = ballY + dy; }

        // Paddle collision: bounce on the LOWER row only
        if (dy > 0 &&
            nextY >= PaddleLowerY &&
            nextX >= paddleX && nextX < paddleX + PaddleW)
        {
            dy = -dy;

            int hitPos = Math.Clamp(nextX - paddleX, 0, PaddleW - 1);
            dx = Math.Clamp(hitPos - PaddleW / 2, -2, 2);
            if (dx == 0) dx = (ballX < W / 2) ? -1 : 1;

            // Snap just above LOWER row -> visually touches TOP row
            nextY = PaddleLowerY - 1; // equals paddleTopY
        }

        // Brick collision
        var (hit, reflectX, reflectY) = HitBrick(nextX, nextY);
        if (hit)
        {
            if (reflectX) dx = -dx;
            if (reflectY) dy = -dy;
            nextX = ballX + dx;
            nextY = ballY + dy;
        }

        ballX = nextX;
        ballY = nextY;

        // Temp: bounce off bottom instead of game over
        if (nextY >= H - 2)
        {
            dy = -dy;
            nextY = ballY + dy;
        }
        // Win check
        if (AllBricksCleared()) running = false;
    }

    static (bool hit, bool reflectX, bool reflectY) HitBrick(int nx, int ny)
    {
        // Map console coords to brick grid (uniform cell mapping)
        int cols = bricks.GetLength(0);
        int rows = bricks.GetLength(1);

        // Brick area bounds
        int left = 1, right = W - 2;
        int top = TopMargin + 1, bottom = TopMargin + 1 + rows; // 1 row per brick visually

        if (ny >= top && ny < bottom)
        {
            int r = ny - top;
            int c = (nx - left) * cols / (right - left);
            c = Math.Clamp(c, 0, cols - 1);

            if (bricks[c, r])
            {
                bricks[c, r] = false;
                // Simple reflection heuristic: reflect vertical by default
                return (true, false, true);
            }
        }
        return (false, false, false);
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

                // Paddle (two rows)
                if (y == paddleTopY && x >= paddleX && x < paddleX + PaddleW) ch = '▀';
                if (y == PaddleLowerY && x >= paddleX && x < paddleX + PaddleW) ch = '▄';


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
