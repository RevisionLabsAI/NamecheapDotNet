using System;

namespace NameCheap
{
    public class DomainPricingResult
    {
        public int ID { get; set; }
        public bool IsOwner { get; set; }
        public string OwnerName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ExpiredDate { get; set; }
        public string DnsProviderType { get; set; }
    }
}
