using Serilog;
using System.Text;
using System.Text.Json;

namespace LION_Docker_Monitor
{
    public class TelegramNotifier
    {
        private readonly string _botToken;
        private readonly string _chatId;
        private static readonly HttpClient _httpClient = new();

        public TelegramNotifier(string botToken, string chatId)
        {
            _botToken = botToken;
            _chatId = chatId;
        }

        public async Task SendMessageAsync(string message)
        {
//#if DEBUG
//            Log.Debug("DEBUG MODE: Telegram message: {Message}", message);
//            return;
//#endif
            try
            {
                string url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

                var content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        chat_id = _chatId,
                        text = message,
                        parse_mode = "HTML"
                    }),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    Log.Error("Ошибка отправки Telegram: Status: {Status}, Response: {Response}", response.StatusCode, responseContent);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при отправке сообщения в Telegram");
            }
        }
    }
}
