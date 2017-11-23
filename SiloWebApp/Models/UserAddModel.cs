using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SiloWebApp.Models
{
    public class UserAddModel
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string PW { get; set; }
        public int Level { get; set; }
    }
}