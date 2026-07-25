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
        /// Gets or sets the premium renewal price — what the registry charges to renew this
        /// specific name each year. Premium names renew at their own price, not the TLD's standard
        /// renewal price, and the gap can be enormous (a premium .com may register at $2,500 and
        /// renew at $300 while standard .com renews at ~$13). Zero when not supplied.
        /// </summary>
        public double PremiumRenewalPrice { get; set; }

        /// <summary>
        /// Gets or sets the premium transfer price for this specific name. Zero when not supplied.
        /// </summary>
        public double PremiumTransferPrice { get; set; }

        /// <summary>
        /// Gets or sets the premium restore (redemption) price for this specific name.
        /// Zero when not supplied.
        /// </summary>
        public double PremiumRestorePrice { get; set; }

        /// <summary>
        /// Gets or sets the ISO 4217 currency the premium prices are quoted in. Null when the
        /// registrar does not report one, in which case the caller treats the amounts as being in
        /// its own billing currency. Registrars quote premiums in varying currencies, so an amount
        /// without its currency cannot be safely charged.
        /// </summary>
        public string? PremiumCurrency { get; set; }

        /// <summary>
        /// Gets or sets any error or warning message returned for this specific domain.
        /// </summary>
        public string? Message { get; set; }
    }
}
