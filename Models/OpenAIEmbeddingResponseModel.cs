    public class OpenAIEmbeddingResponseModel
    {
        public string Object { get; set; }
        public EmbeddingData[] Data { get; set; }
        public string Model { get; set; }
        public Usage Usage { get; set; }
    }

    public class EmbeddingData
    {
        public string Object { get; set; }
        public float[] Embedding { get; set; }
        public int Index { get; set; }
    }

    public class Usage
    {
        public int PromptTokens { get; set; }
        public int TotalTokens { get; set; }
    }