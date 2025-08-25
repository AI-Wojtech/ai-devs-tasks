using System;
using System.IO;
using System.Text.Json;

public class FineTuningService
{
    private const string CorrectFilePath = @"D:\ai-dev\ai-devs-zadania-code\ai-devs-tasks\Episodes\S04E02\lab_data\correct.txt";
    private const string IncorrectFilePath = @"D:\ai-dev\ai-devs-zadania-code\ai-devs-tasks\Episodes\S04E02\lab_data\incorect.txt";
    private const string OutputFilePath = @"D:\ai-dev\ai-devs-zadania-code\ai-devs-tasks\Episodes\S04E02\lab_data\finetune_data.jsonl";

    public void GenerateJsonl()
    {
        try
        {
            // Upewnij się, że katalog docelowy istnieje
            string? outputDir = Path.GetDirectoryName(OutputFilePath);
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir!);
            }

            using StreamWriter writer = new StreamWriter(OutputFilePath, false);
            ProcessFile(CorrectFilePath, "1", writer);
            ProcessFile(IncorrectFilePath, "0", writer);

            Console.WriteLine($"✅ Plik JSONL został zapisany pod: {OutputFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Wystąpił błąd: {ex.Message}");
        }
    }

    private void ProcessFile(string path, string label, StreamWriter writer)
    {
        foreach (var line in File.ReadLines(path))
        {
            var record = new
            {
                messages = new[]
                {
                    new { role = "system", content = "validate data" },
                    new { role = "user", content = line },
                    new { role = "assistant", content = label }
                }
            };

            string jsonLine = JsonSerializer.Serialize(record);
            writer.WriteLine(jsonLine);
        }
    }
}
