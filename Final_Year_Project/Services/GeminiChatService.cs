public class GeminiChatService
{
    private readonly string _apiKey;

    public GeminiChatService(IConfiguration config)
    {
        _apiKey = config["Gemini:ApiKey"];
    }

    //public async Task<string> GenerateReply(string userMessage)
    //{
    //    var gen = new TextGenerationModel("models/gemini-1.5-flash", _apiKey);

    //    var response = await gen.GenerateContent(userMessage);

    //    return response.Text;
    //}
}
