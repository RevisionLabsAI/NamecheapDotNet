namespace NameCheap
{
    public class DomainCheckResult
    {
        public string DomainName { get; set; }
        public bool IsAvailable { get; set; }
        
        public bool IsPremiumName { get; set; }
        public double IcannFee { get; set; }
        public double PremiumRegistrationPrice { get; set; }
    }
}
