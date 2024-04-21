using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pinger.Models
{
	public class ConfigModel
	{
        public string Address { get; set; }
        public int Timeout { get; set; }
        public int MaxData { get; set; }
        public int RequestInterval { get; set; }
        public ConfigModel()
        {
            Address = "8.8.8.8";
            Timeout = 100;
            MaxData = 100;
            RequestInterval = 500;
        }
    }
}
