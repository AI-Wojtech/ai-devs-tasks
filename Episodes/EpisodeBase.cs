public abstract class EpisodeBase : IEpisode
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual string NameCode => GetType().Name;

    public abstract Task RunAsync();

}