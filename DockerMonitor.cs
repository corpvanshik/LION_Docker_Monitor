using Docker.DotNet.Models;
using Docker.DotNet;
using Serilog;

namespace LION_Docker_Monitor
{
    public class DockerMonitor
    {
        private readonly DockerClient _dockerClient;
        private readonly TelegramNotifier _notifier;
        private readonly Dictionary<string, ContainerMonitorConfig> _configs;
        private readonly Dictionary<string, DateTime?> _containerExitTimes = new();
        private readonly Dictionary<string, DateTime?> _lastNotificationTimes = new();

        public DockerMonitor(DockerClient dockerClient, TelegramNotifier notifier, Dictionary<string, ContainerMonitorConfig> configs)
        {
            _dockerClient = dockerClient;
            _notifier = notifier;
            _configs = configs;
        }

        public async Task MonitorAsync()
        {
            while (true)
            {
                try
                {
                    var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters { All = true });
                    var currentTime = DateTime.Now;

                    foreach (var containerConfig in _configs)
                    {
                        string containerName = containerConfig.Key;
                        var config = containerConfig.Value;

                        var container = containers.FirstOrDefault(c =>
                            c.Names.Contains("/" + containerName) ||
                            (c.Names.Count > 0 && c.Names[0].EndsWith("/" + containerName)));

                        if (container == null)
                        {
                            Log.Warning("Контейнер '{ContainerName}' не найден", containerName);
                            continue;
                        }

                        bool isExited = container.State == "exited";

                        if (isExited)
                        {
                            if (!_containerExitTimes.ContainsKey(containerName) || _containerExitTimes[containerName] == null)
                            {
                                var inspect = await _dockerClient.Containers.InspectContainerAsync(container.ID);
                                DateTime finishedAt = DateTime.Parse(inspect.State.FinishedAt, null, System.Globalization.DateTimeStyles.RoundtripKind);
                                _containerExitTimes[containerName] = finishedAt;

                                Log.Information("Контейнер '{ContainerName}' остановлен. Время: {Time}", containerName, finishedAt);
                            }

                            TimeSpan exitedTime = currentTime - _containerExitTimes[containerName].Value;

                            // Первичное уведомление
                            if (exitedTime.TotalMinutes >= config.InitialAlertMinutes &&
                                (!_lastNotificationTimes.ContainsKey(containerName) || _lastNotificationTimes[containerName] == null))
                            {
                                await SendContainerExitNotification(containerName, container.State, exitedTime);
                                _lastNotificationTimes[containerName] = currentTime;
                            }
                            // Повторное уведомление
                            else if (_lastNotificationTimes.ContainsKey(containerName) &&
                                     _lastNotificationTimes[containerName] != null)
                            {
                                TimeSpan timeSinceLastNotification = currentTime - _lastNotificationTimes[containerName].Value;

                                if (timeSinceLastNotification.TotalMinutes >= config.RepeatAlertMinutes)
                                {
                                    await SendContainerExitNotification(containerName, container.State, exitedTime);
                                    _lastNotificationTimes[containerName] = currentTime;
                                }
                            }
                        }
                        else
                        {
                            // Если контейнер снова запущен, сбрасываем таймеры
                            if (_containerExitTimes.ContainsKey(containerName) && _containerExitTimes[containerName] != null)
                            {
                                await SendContainerIsRunningNotification(containerName, container.State);
                                _containerExitTimes[containerName] = null;
                                _lastNotificationTimes[containerName] = null;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка во время мониторинга");
                }

                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }

        private async Task SendContainerExitNotification(string containerName, string state, TimeSpan exitedTime)
        {
            string message = $"⚠️ Контейнер '{containerName}' находится в состоянии <b>{state}</b> уже {Math.Floor(exitedTime.TotalMinutes)} мин!";
            await _notifier.SendMessageAsync(message);
        }

        private async Task SendContainerIsRunningNotification(string containerName, string state)
        {
            string message = $"✅ Контейнер '{containerName}' снова запущен (статус: <b>{state})</b>";
            await _notifier.SendMessageAsync(message);
        }
    }
}
