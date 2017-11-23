using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SiloWebApp.Models
{
    public class User2Model
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime Entry { get; set; }
        public DateTime Modify { get; set; }
        public int Level { get; set; }
    }
}