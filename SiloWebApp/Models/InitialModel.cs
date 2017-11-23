using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SiloWebApp.Models
{
    public class InitialModel
    {
        public int SiloNo { get; set; }
        public string Direction { get; set; }
        public string Channel { get; set; }
        public float Value1 { get; set; }
        public float Value2 { get; set; }
        public float Value3 { get; set; }
    }
}