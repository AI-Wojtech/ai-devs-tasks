using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public class Episode13 : EpisodeBase
{
    private readonly HttpClient _httpClient = new HttpClient();
    private const string DataBaseApiUrl = "https://c3ntrala.ag3nts.org/apidb";
    private const string ReportUrl = "https://c3ntrala.ag3nts.org/report";
    public override string Name => "S03E03 — Wyszukiwanie hybrydowe (Episode13)";
    public override string Description => "Centrala wystawiła dla Ciebie specjalne API, które umożliwi Ci wykonanie niemal dowolnych zapytań wyciągających dane ze wspomnianej bazy. Wiemy, że znajdują się tam tabele o nazwach users, datacenters oraz connections. Niekoniecznie potrzebujesz teraz wszystkich z nich. Twoim zadaniem jest zwrócenie nam numerów ID czynnych datacenter, które zarządzane są przez menadżerów, którzy aktualnie przebywają na urlopie (są nieaktywni). To pozwoli nam lepiej wytypować centra danych bardziej podatne na atak. Nazwa zadania to database.";

    public override async Task RunAsync()
    {
        var ApiKey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY");
        var httpService = new HttpService();
        var openAiService = new OpenAIService();

        // Krok 1: Pobierz listę tabel
        var tablesPayload = new { task = "database", apikey = ApiKey, query = "SHOW TABLES" };
        var tablesResponse = await httpService.PostJsonAsync<ApiResponse<List<TableInfo>>>(DataBaseApiUrl, tablesPayload);
        var tables = tablesResponse?.Reply?.Select(t => t.Tables_in_banan).ToList();


        if (tables == null || !tables.Contains("datacenters") || !tables.Contains("users"))
        {
            Console.WriteLine("Nie znaleziono wymaganych tabel.");
            return;
        }


        var datacentersPayload = new { task = "database", apikey = ApiKey, query = "SHOW CREATE TABLE datacenters" };
        var datacentersSchemaResponse = await httpService.PostJsonAsync<ApiResponse<List<TableSchema>>>(DataBaseApiUrl, datacentersPayload);

        var usersPayload = new { task = "database", apikey = ApiKey, query = "SHOW CREATE TABLE users" };
        var usersSchemaResponse = await httpService.PostJsonAsync<ApiResponse<List<TableSchema>>>(DataBaseApiUrl, usersPayload);

        if (datacentersSchemaResponse?.Reply == null || usersSchemaResponse?.Reply == null)
        {
            Console.WriteLine("Nie udało się pobrać schematów tabel.");
            return;
        }

        string datacentersSchema = datacentersSchemaResponse.Reply.FirstOrDefault()?.CreateTable ?? "";
        string usersSchema = usersSchemaResponse.Reply.FirstOrDefault()?.CreateTable ?? "";

        // Krok 3: Wygeneruj zapytanie SQL za pomocą LLM
        string llmPrompt = $@"Based on the following database schemas, write an SQL query that returns the DC_ID of active datacenters managed by inactive managers (from the users table). Return only the raw SQL query text, without any explanations or formatting.

        Datacenters schema:
        {datacentersSchema}

        Users schema:
        {usersSchema}";

        string sqlQuery;
        try
        {
            sqlQuery = await openAiService.GetAnswerAsync(llmPrompt);
            Console.WriteLine($"Wygenerowane zapytanie SQL: {sqlQuery}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd podczas generowania zapytania SQL: {ex.Message}");
            return;
        }

        // Krok 4: Wykonaj zapytanie SQL
        var queryPayload = new { task = "database", apikey = ApiKey, query = sqlQuery };
        try
        {
            var queryResponse = await httpService.PostJsonAsync<ApiResponse<List<DatacenterResult>>>(DataBaseApiUrl, queryPayload);

            var datacenterIds = queryResponse?.Reply?
                .Select(r => int.Parse(r.DcId))
                .ToList() ?? new List<int>();

            var answer = await httpService.SendAnswerAsync(datacenterIds, "database");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SQL error: {ex.Message}");
            return;
        }

    }

    public class ApiResponse<T>
    {
        public T Reply { get; set; }
        public string Error { get; set; }
    }

    public class TableInfo
    {
        public string Tables_in_banan { get; set; }
    }

    public class TableSchema
    {
        public string Table { get; set; }

        [JsonPropertyName("Create Table")]
        public string CreateTable { get; set; }
    }

    public class DatacenterResult
    {
        [JsonPropertyName("dc_id")]
        public string DcId { get; set; }
    }


}