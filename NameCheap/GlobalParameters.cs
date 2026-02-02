namespace NameCheap
{
    /// <summary>
    /// Represents the global parameters required for Namecheap API calls.
    /// </summary>
    public class GlobalParameters
    {
        /// <summary>
        /// Gets or sets the API user name.
        /// </summary>
        public string ApiUser { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the API key.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user name.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client IP address.
        /// </summary>
        public string CLientIp { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to use the sandbox environment.
        /// </summary>
        public bool IsSandBox { get; set; }
    }
}
