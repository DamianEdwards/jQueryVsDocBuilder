using System;
using System.Linq;
using System.Collections.Generic;
using System.Web.Mvc;

namespace VsDocBuilder.Models
{
    public class BuildModel
    {
        public IEnumerable<SelectListItem> Versions { get; set; }
        public IEnumerable<SelectListItem> NewLineMethods { get; set; }
        public string Version { get; set; }
    }
}