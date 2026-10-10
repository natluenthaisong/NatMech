namespace AnoMech.Standalone;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new Form { Text = "NatMech Practice" });
        return 0;
    }
}
