public interface IEpisode
{
    string Name { get; }
    string NameCode { get; }
    string Description { get; }
    Task RunAsync();
}