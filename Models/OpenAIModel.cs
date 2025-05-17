public class OpenAIModel
{
    public OpenAIModel(string question)
    {
        Question = question;
    }

    public string SystemPrompt { get; set; }
    public string Question { get; set; }
}