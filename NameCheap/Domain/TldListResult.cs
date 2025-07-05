using System;
using System.Xml.Serialization;

namespace NameCheap
{
    [XmlRoot("Tlds")]
    public class TldListResult
    {
        public DateTime TimeStamp { get; set; }
        [XmlElement("Tld")]
        public Tld[] Tlds { get; set; }
    }
}
