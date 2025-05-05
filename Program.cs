using Docker.DotNet;
using Serilog;

namespace LION_Docker_Monitor
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
#if DEBUG
                .MinimumLevel.Debug()
#else
                .MinimumLevel.Information()
#endif
                .CreateLogger();

            try
            {
                Log.Information("Docker Container Monitor starting...");

                // Парсим конфиг контейнеров
                var configParser = new MonitorConfigParser();
                var monitoredContainers = configParser.Parse();

                // Получаем токен бота и chat_id
                string telegramBotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
                string telegramChatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");

#if DEBUG
                // Тестовые данные для отладки
                telegramBotToken ??= "{TOKEN}";
                telegramChatId ??= "{ID}";
                if (monitoredContainers.Count == 0)
                {
                    monitoredContainers["{CONTAINER_NAME}"] = new ContainerMonitorConfig { InitialAlertMinutes = 1, RepeatAlertMinutes = 3 };
                    Log.Debug("DEBUG MODE: Добавлен тестовый контейнер test_container для мониторинга.");
                }
#endif

                if (string.IsNullOrEmpty(telegramBotToken) || string.IsNullOrEmpty(telegramChatId))
                {
                    Log.Error("TELEGRAM_BOT_TOKEN или TELEGRAM_CHAT_ID не заданы!");
                    return;
                }

                if (monitoredContainers.Count == 0)
                {
                    Log.Warning("Нет контейнеров для мониторинга!");
                }
                else
                {
                    Log.Information("Будут мониториться контейнеры:");
                    foreach (var container in monitoredContainers)
                    {
                        Log.Information(" - {Name}: Первое уведомление через {Initial} мин., повтор каждые {Repeat} мин.",
                            container.Key, container.Value.InitialAlertMinutes, container.Value.RepeatAlertMinutes);
                    }
                }
#if DEBUG
                string dockerUri = Environment.OSVersion.Platform == PlatformID.Win32NT
                    ? "npipe://./pipe/docker_engine"
                    : "unix:///var/run/docker.sock";
                var dockerClient = new DockerClientConfiguration(new Uri(dockerUri)).CreateClient();
#else 
                var dockerClient = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock")).CreateClient();
#endif

                var notifier = new TelegramNotifier(telegramBotToken, telegramChatId);

                // Стартовое уведомление
                await notifier.SendMessageAsync("🚀 <b>LION Docker Container Monitor запущен!</b>\n\nМониторятся контейнеры:\n" +
                    string.Join("\n", monitoredContainers.Keys.Select(x => $"— {x}")));
                Log.Information("Стартовое уведомление отправлено.");

                // Запуск мониторинга
                var monitor = new DockerMonitor(dockerClient, notifier, monitoredContainers);
                await monitor.MonitorAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Ошибка при запуске приложения");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
