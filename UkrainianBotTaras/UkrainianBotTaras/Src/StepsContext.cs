using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkrainianBotTaras
{
    public class StepsContext
    {
        public string BaseUrl { get; set; }
        public string DataDirectory { get; set; }
        public string KeyPassword { get; set; }
        public string DayOfBirth { get; set; }
        public string MonthOfBirth { get; set; }
        public string YearOfBirth { get; set; }
        public string Country { get; set; }
        public string ConsulateName { get; set; }
        public string ServiceName { get; set; }
        public string KeyFileName { get; set; }
        public string ForWho { get; set; }
        public Kid[] Kids { get; set; }
    }

    public class Kid
    {
        public string ChildName { get; set; }
        public string ChildMiddleName { get; set; }
        public string ChildLastName { get; set; }
    }
}