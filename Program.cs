class Program
{
    static async Task Main(string[] args)
    {
        var episodes = EpisodesHelper.DiscoverEpisodes();

        if (args.Length == 0)
        {
            Console.WriteLine("AVAILABLE EPISODES:");
            foreach (var ep in episodes)
            {
                Console.WriteLine($"- {ep.Name}");
            }

            Console.WriteLine("\nCOMMANDS:\n  dotnet run -- Episode01          # run epizod");
            Console.WriteLine("  dotnet run -- Episode01 --desc   # display description");
            return;
        }

        string episodeName = args[0];
        bool showDescriptionOnly = args.Length > 1 && args[1] == "--desc";

        var selected = episodes.FirstOrDefault(e =>
            e.NameCode.Equals(episodeName, StringComparison.OrdinalIgnoreCase));

        if (selected == null)
        {
            Console.WriteLine($"Episode '{episodeName}' doesn't exist.");
            return;
        }

        if (showDescriptionOnly)
        {
            Console.WriteLine($"{selected.Name} - {selected.Description}");
        }
        else
        {
            Console.WriteLine($"Launching: {selected.Name}");
            await selected.RunAsync();
        }
    }
}