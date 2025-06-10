using System.Text.Json;
using Neo4j.Driver;

public class Episode15 : EpisodeBase
{
    public override string Name => "S03E05 — Bazy grafowe (Episode15)";
    public override string Description => "Droga od Rafała do Barbary. Zapisz dane z SQL do bazy grafowej i znajdź najkrótszą ścieżkę.";

    private const string ApiDbUrl = "https://c3ntrala.ag3nts.org/apidb";
    private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";

    public class ResponseModel
    {
        public int Code { get; set; }
        public string Message { get; set; }
    }

    public class DbResponse
    {
        public List<Dictionary<string, object>> Reply { get; set; }
        public string Error { get; set; }
    }

    public override async Task RunAsync()
    {
        var apiKey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY");
        var httpService = new HttpService();

        // 1. Pobierz users
        var usersQuery = new
        {
            task = "database",
            apikey = apiKey,
            query = "SELECT id, username FROM users;"
        };
        var usersResponse = await httpService.PostJsonAsync<DbResponse>(ApiDbUrl, usersQuery);
        var users = usersResponse.Reply.ToDictionary(
            row => int.Parse(((JsonElement)row["id"]).GetString()),
            row => ((JsonElement)row["username"]).GetString()
        );

        // 2. Pobierz connections
        var connectionsQuery = new
        {
            task = "database",
            apikey = apiKey,
            query = "SELECT user1_id, user2_id FROM connections;"
        };
        var connectionsResponse = await httpService.PostJsonAsync<DbResponse>(ApiDbUrl, connectionsQuery);
        var connections = connectionsResponse.Reply
            .Select(row => (
                int.Parse(((JsonElement)row["user1_id"]).GetString()),
                int.Parse(((JsonElement)row["user2_id"]).GetString())
            ))
            .ToList();

        // 3. Połącz się z Neo4j i zbuduj graf
        var neoUri = "bolt://localhost:7687";
        var neoUser = "neo4j";
        var neoPass = "neo4j";

        using var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "sedes123"));
        await using var session = driver.AsyncSession();
        var result2 = await session.RunAsync("RETURN 1");
        var record3 = await result2.SingleAsync();
        Console.WriteLine(record3[0].As<int>());


        // Wyczyszczenie starego grafu
        await session.RunAsync("MATCH (n) DETACH DELETE n");

        // Tworzenie węzłów
        foreach (var (id, username) in users)
        {
            var query = "CREATE (:Person {userId: $id, username: $username})";
            await session.RunAsync(query, new { id, username });
        }

        // Tworzenie relacji
        foreach (var (id1, id2) in connections)
        {
            var query = @"
                MATCH (a:Person {userId: $id1}), (b:Person {userId: $id2})
                CREATE (a)-[:KNOWS]->(b)";
            await session.RunAsync(query, new { id1, id2 });
        }

        // 4. Znajdź najkrótszą ścieżkę (po username, nie name)
        var pathQuery = @"
            MATCH (start:Person {username: 'Rafał'}), (end:Person {username: 'Barbara'})
            MATCH path = shortestPath((start)-[:KNOWS*]-(end))
            RETURN [n IN nodes(path) | n.username] AS names";

        var result = await session.RunAsync(pathQuery);
        var record = await result.SingleAsync();
        var names = record["names"].As<List<string>>();
        var answer = string.Join(",", names);

        // 5. Wyślij odpowiedź
        var reportPayload = new
        {
            task = "connections",
            apikey = apiKey,
            answer = answer
        };

        var response = await httpService.PostJsonAsync<object>(ReportUrl, reportPayload);
        Console.WriteLine($"Zgłoszono wynik: {answer}");
    }
}
