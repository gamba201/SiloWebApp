using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SiloWebApp.Models
{
    public class LevelListModel
    {
        public string Overview { get; set; }
        public string Id { get; set; }
        public DateTime Reg_Time { get; set; }
        public string safe { get; set; }
        public string Concern { get; set; }
        public string Caution { get; set; }
        public string Danger { get; set; }
    }
}