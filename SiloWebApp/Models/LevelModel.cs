using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SiloWebApp.Models
{
    public class LevelModel
    {
        public string Overview { get; set; }
        public int Concern { get; set; }
        public int Caution { get; set; }
        public int Danger { get; set; }

    }
}