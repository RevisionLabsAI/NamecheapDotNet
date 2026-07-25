using System;
using System.Xml.Serialization;

namespace NameCheap
{
    [XmlRoot("Tlds")]
    public class TldListResult
    {
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// Gets or sets the registrar this list came from. Null on lists captured before this
        /// field existed.
        ///
        /// Every registrar publishes its own catalog, and they differ substantially — both in
        /// which TLDs they carry and in the per-TLD capability flags on <see cref="Tld"/>. Without
        /// an owner recorded, a store holding lists from several registrars cannot tell them
        /// apart, so the most recently written list is served to every registrar and one
        /// registrar's catalog silently becomes another's. Not serialized to XML: it is stamped by
        /// the caller that made the request rather than returned by the API.
        /// </summary>
        [XmlIgnore]
        public string Registrar { get; set; }

        [XmlElement("Tld")]
        public Tld[] Tlds { get; set; }
    }
}
