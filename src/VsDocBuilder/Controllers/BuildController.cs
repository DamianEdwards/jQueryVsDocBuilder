using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using System.Xml.Linq;
using VsDocBuilder.Models;

namespace VsDocBuilder.Controllers
{
    public class BuildController : Controller
    {
        readonly static string _defaultVersion = "1.8.1";
        readonly static string[] _versions = new[] { "1.4.2", "1.4.3", "1.4.4", "1.5", "1.5.1", "1.5.2", "1.6", "1.6.1", "1.6.2", "1.6.3", "1.6.4", "1.7", "1.7.1", "1.8.0", "1.8.1" };

        public ActionResult Index(string ver, string newLineMethod = "xml")
        {
            var model = new BuildModel();
            var versions = new List<SelectListItem>();
            model.Version = string.IsNullOrEmpty(ver) ? _defaultVersion : ver;
            foreach (var v in _versions) {
                versions.Add(new SelectListItem { Text = v, Value = v, Selected = (v == model.Version) });
            }
            model.Versions = versions;
            model.NewLineMethods = new List<SelectListItem>
            {
                new SelectListItem { Text = "XML entities", Value = "xml", Selected = newLineMethod == "xml" },
                new SelectListItem { Text = "<para> tags", Value = "para", Selected = newLineMethod == "para" }
            };
            return View(model);
        }

        public JsonResult jQueryDoc()
        {
            var entries = GetjQueryDocFromCacheOrjQueryWebsite()
                .Element("entries")
                .Elements("entry")
                .Where(e => e.Attribute("type") != null && e.Attribute("type").Value == "method");
            var doc = entries
                .Distinct(new EntryComparer())
                .Select(e => new
                {
                    name = e.Attribute("name").Value,
                    returns = DisambiguateType(BuildReturns(e, entries)),
                    summary = BuildSummary(e, entries),
                    parameters = BuildParameters(e, entries)
                });
            
            return Json(doc, JsonRequestBehavior.AllowGet);
        }

        public ActionResult jQueryDocTypes()
        {
            var types = GetjQueryDocFromCacheOrjQueryWebsite()
                .Element("entries")
                .Elements("entry")
                .SelectMany(e => e.Elements("signature"))
                .SelectMany(s => s.Elements("argument"))
                .Select(a => a.Attribute("type").Value)
                .Distinct();
            return View(types);
        }

        private class EntryComparer : IEqualityComparer<XElement>
        {
            public bool Equals(XElement x, XElement y)
            {
                return x.Attribute("name").Value == y.Attribute("name").Value;
            }

            public int GetHashCode(XElement obj)
            {
                return obj.Attribute("name").Value.GetHashCode();
            }
        }

        private XElement GetjQueryDocFromCacheOrjQueryWebsite()
        {
            var result = HttpContext.Cache["jQueryDocXml"] as XElement;
            if (result == null)
            {
                // Check file system
                var apiFilePath = Server.MapPath("~/api.xml");
                if (!System.IO.File.Exists(apiFilePath))
                {
                    // Download from api.jquery.com and save
                    using (var wc = new WebClient())
                    {
                        wc.DownloadFile("http://api.jquery.com/api", apiFilePath);
                    }
                }
                using (var fileStream = new FileStream(apiFilePath, FileMode.Open))
                {
                    result = XElement.Load(fileStream);
                }
                
                HttpContext.Cache.Add("jQueryDocXml", result, null, Cache.NoAbsoluteExpiration, TimeSpan.FromHours(1),
                                      CacheItemPriority.Normal, null);
            }
            return result;
        }

        private static string BuildSummary(XElement entry, IEnumerable<XElement> entries)
        {
            var name = entry.Attribute("name").Value;
            var summary = "";

            // Need to deal with multiple entries, e.g. jQuery itself has 3
            // Detect and merge into the 1., 2. format?
            var matchingEntries = entries.Where(e => e.Attribute("name").Value == name);

            var entryNumber = 0;
            var entryCount = matchingEntries.Count();
            foreach (var ent in matchingEntries)
            {
                entryNumber++;
                var desc = ent.Element("desc") != null ? ent.Element("desc").Value : "";
                desc = desc.Trim();
                if (entryNumber > 1)
                    summary += "\r\n";
                if (entryCount > 1)
                    summary += entryNumber + ": ";
                summary += desc + "\r\n";

                var signatureNumber = 0;
                var signatureCount = ent.Elements("signature").Count();
                if (entryCount <= 1 && signatureCount <= 1)
                    continue;
                foreach (var signature in ent.Elements("signature"))
                {
                    signatureNumber++;
                    var sigSummary = string.Format("{0} - {1}({2})",
                        signatureNumber,
                        name,
                        string.Join(", ", signature.Elements("argument").Select(e => e.Attribute("name").Value))
                        );
                    if (entryCount > 1)
                        sigSummary = string.Format("    {0}.{1}", entryNumber, sigSummary);
                    summary += (signatureNumber > 1 ? " \r\n" : "") + sigSummary;
                }
            }

            return summary;
        }

        private static IEnumerable<object> BuildParameters(XElement entry, IEnumerable<XElement> entries)
        {
            // Need to deal with multiple entries, e.g. jQuery itself has 3
            var matchingEntries = entries.Where(e => e.Attribute("name").Value == entry.Attribute("name").Value);

            var signatures = matchingEntries.SelectMany(e => e.Elements("signature"));

            //var signatures = entry.Elements("signature");
            var signatureToUse =
                signatures.Where(s => s.Elements("argument").Count() ==
                    signatures.Max(e => e.Elements("argument").Count()))
                .FirstOrDefault();

            // TODO: Add support for <options> on "Map" types here
            // <signature>
            //  <added>1.5</added>
            //  <argument name="url" type="String">
            //   <desc>A string containing the URL to which the request is sent.</desc>
            //  </argument>
            //  <argument name="settings" type="Map">
            //   <desc></desc>
            //  </argument>
            // </signature>
            // <signature>
            //  <added>1.0</added>
            //  <argument name="settings" type="Map">
            //   <desc></desc>
            //   <option default="depends on DataType" name="accepts" type="Map">
            //    <desc>blah</desc>
            //   </option>
            //  </argument>
            // </signature>
            
            // For each parameter, look through all signatures for parameters with same
            // name and type of "Map" and that have <option> children, and build summary
            // from that.
            
            
            return signatureToUse.Elements("argument")
                .Select(a => new {
                                     name = a.Attribute("name").Value,
                                     type = DisambiguateType((a.Attribute("type") ?? new XAttribute("dummy", "")).Value),
                                     summary = a.Element("desc").Value
                                 }
                    );
        }

        private static string BuildReturns(XElement entry, IEnumerable<XElement> entries)
        {
            // Need to deal with multiple entries, e.g. jQuery itself has 3
            var matchingEntries = entries.Where(e => e.Attribute("name").Value == entry.Attribute("name").Value);

            // Get candidate return types
            var returnTypes = matchingEntries.Select(e => e.Attribute("return").Value).Distinct();

            // Use return type 'jQuery' if exists, otherwise first one
            return returnTypes.SingleOrDefault(s => s == "jQuery") ??
                returnTypes.First();
        }

        private static string DisambiguateType(string type)
        {
            if (type.Contains(","))
                return type.Split(',').Last().Trim();

            if (type.Contains("/"))
                return type.Split('/').Last().Trim();

            if (type == "Callback")
                return "Function";

            if ((new [] { "Options", "Map", "Any" }).Contains(type))
                return "Object";

            if (type.Equals("selector", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("HTML", StringComparison.OrdinalIgnoreCase))
                return "String";

            if (type == "Integer")
                return "Number";

            if (type == "Elements")
                return "Array";

            return type;
        }
    }
}