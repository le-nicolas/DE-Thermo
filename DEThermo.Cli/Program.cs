namespace DEThermo.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return 1;
            }

            var command = args[0].Trim().ToLowerInvariant();
            var options = OptionMap.Parse(args.Skip(1).ToArray());

            switch (command)
            {
                case "simulate":
                    CliCommands.RunSimulate(options);
                    break;
                case "batch":
                    CliCommands.RunBatch(options);
                    break;
                case "optimize":
                    CliCommands.RunOptimize(options);
                    break;
                case "monte-carlo":
                case "montecarlo":
                    CliCommands.RunMonteCarlo(options);
                    break;
                case "help":
                case "--help":
                case "-h":
                    PrintHelp();
                    break;
                default:
                    throw new ArgumentException($"Unknown command '{command}'. Run 'help' for usage.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            DE-Thermo CLI (C#)

            Commands:
              simulate      Run one scenario and emit trajectory + summary
              batch         Run many scenarios from a JSON file
              optimize      Find minimum HTC required to meet freeze deadline
              monte-carlo   Run uncertainty analysis

            Example:
              dotnet run --project DEThermo.Cli -- simulate --mass-kg 0.35 --initial-temp-c 90 --ambient-temp-c -18 --area-m2 0.03 --htc-w-m2k 8
            """);
    }
}
