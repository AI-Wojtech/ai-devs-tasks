using Microsoft.Extensions.Configuration;
public static class ConfigHelper
{
    private static IConfigurationRoot _config;

    static ConfigHelper()
    {
        _config = new ConfigurationBuilder()
            .AddJsonFile("config.json", optional: false, reloadOnChange: false)
            .Build();
    }

    //EXAMPLE
    //string openAImodel = ConfigHelper.GetValue<string>("OpenAPI:MODEL");
    //string centralaApiKey = ConfigHelper.GetValue<string>("CENTRALA_API_KEY");
    public static T GetValue<T>(string sectionPath)
    {
        var section = _config.GetSection(sectionPath);
        if (!section.Exists())
            throw new ArgumentException($"Section '{sectionPath}' not found in config.json");

        var value = section.Get<T>();

        if (value == null)
        {
            throw new InvalidOperationException($"Value at section '{sectionPath}' could not be mapped to type {typeof(T).Name} or is null.");
        }

        return value;
    }


}