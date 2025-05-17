public static class EpisodesHelper
{
    public static List<IEpisode> DiscoverEpisodes()
    {
        var type = typeof(IEpisode);
        var implementations = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => type.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(t => (IEpisode)Activator.CreateInstance(t)!)
            .OrderBy(e => e.Name)
            .ToList();

        return implementations;
    }
}