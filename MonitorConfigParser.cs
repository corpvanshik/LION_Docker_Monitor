using Serilog;

namespace LION_Docker_Monitor
{
    public class MonitorConfigParser
    {
        public Dictionary<string, ContainerMonitorConfig> Parse()
        {
            var result = new Dictionary<string, ContainerMonitorConfig>();
            int containerIndex = 1;

            while (true)
            {
                string envVar = $"CONTAINER_{containerIndex}";
                string value = Environment.GetEnvironmentVariable(envVar);

                if (string.IsNullOrEmpty(value))
                    break;

                // Пример value: name=di_graphs;initial=1;repeat=3
                var parts = value.Split(';');
                string name = null;
                int initial = 5, repeat = 60; // дефолты

                foreach (var part in parts)
                {
                    var kv = part.Split('=', 2);
                    if (kv.Length != 2) continue;

                    var key = kv[0].Trim().ToLower();
                    var val = kv[1].Trim();

                    if (key == "name") name = val;
                    else if (key == "initial" && int.TryParse(val, out int i)) initial = i;
                    else if (key == "repeat" && int.TryParse(val, out int r)) repeat = r;
                }

                if (!string.IsNullOrEmpty(name))
                {
                    result[name] = new ContainerMonitorConfig
                    {
                        InitialAlertMinutes = initial,
                        RepeatAlertMinutes = repeat
                    };
                }
                else
                {
                    Log.Warning("CONTAINER_{Index} не содержит поле 'name'!", containerIndex);
                }

                containerIndex++;
            }

            return result;
        }
    }
}
