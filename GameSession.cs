using Raylib_cs;

public static class GameSession
{
    public static double startTime = 0;
    public static double endTime = 0;
    public static bool isGameOver = false;
    public static bool didWin = false;

    public static void StartGame()
    {
        startTime = Raylib.GetTime();
        endTime = 0;
        isGameOver = false;
        didWin = false;
    }

    public static void EndGame(bool won)
    {
        if (isGameOver) return;
        endTime = Raylib.GetTime();
        isGameOver = true;
        didWin = won;
    }

    public static double ElapsedSeconds => isGameOver ? (endTime - startTime) : (Raylib.GetTime() - startTime);

    public static string FormatElapsed()
    {
        double s = ElapsedSeconds;
        int minutes = (int)(s / 60);
        int seconds = (int)(s % 60);
        int ms = (int)((s * 100) % 100);
        return $"{minutes:00}:{seconds:00}.{ms:00}";
    }
}