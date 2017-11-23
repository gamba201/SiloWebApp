using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SiloWebApp.Models
{
    public class UserHistoryModel
    {
        public string ID { get; set; }
        public string IP { get; set; }
        public DateTime Time { get; set; }
    }
}