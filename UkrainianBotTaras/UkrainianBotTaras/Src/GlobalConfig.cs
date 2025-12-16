using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkrainianBotTaras
{
    internal class GlobalConfig
    {
        public int LaunchDelayMs { get; set; } = 3000;
        public int BotsCount { get; set; } = 1;
        public StepsContext? TemplateContext { get; set; }
    }
}
