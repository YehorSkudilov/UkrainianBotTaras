using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkrainianBotTaras
{
    public class BotContext
    {
        public string BaseUrl { get; set; }
        public string KeyFilePath { get; set; }
        public string KeyPassword { get; set; }
        public string DayOfBirth { get; set; }
        public string MonthOfBirth {get; set;}
        public string YearOfBirth { get; set; }
    }
}
