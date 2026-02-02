namespace NameCheap
{
    /// <summary>
    /// Represents the result of a domain availability check.
    /// </summary>
    public class DomainCheckResult
    {
        /// <summary>
        /// Gets or sets the domain name.
        /// </summary>
        public string DomainName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the domain is available.
        /// </summary>
        public bool IsAvailable { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the domain is a premium name.
        /// </summary>
        public bool IsPremiumName { get; set; }

        /// <summary>
        /// Gets or sets the ICANN fee for the domain.
        /// </summary>
        public double IcannFee { get; set; }

        /// <summary>
        /// Gets or sets the premium registration price.
        /// </summary>
        public double PremiumRegistrationPrice { get; set; }

        /// <summary>
        /// Gets or sets any error or warning message returned for this specific domain.
        /// </summary>
        public string? Message { get; set; }
    }
}
