using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NameCheap
{
    /// <summary>Set of functions responsible for domain management: creating, info, renewal, contacts, etc.</summary>
    public class DomainsApi
    {
        private readonly XNamespace _ns = XNamespace.Get("http://api.namecheap.com/xml.response");
        private readonly GlobalParameters _params;

        internal DomainsApi(GlobalParameters globalParams)
        {
            _params = globalParams;
        }

        /// <summary>
        ///  Checks the availability of domains.
        /// </summary>
        /// <param name="domains">Domains to check.</param>
        /// <returns>List of results for each parameter. Order is not guaranteed to match the order of parameters.</returns>
        /// <exception cref="ApplicationException">
        /// Exception when the following problems are encountered:
        /// - 3031510	Error response from Enom when the error count != 0
        /// - 3011511	Unknown response from the provider
        /// - 2011169	Only 50 domains are allowed in a single check command
        /// </exception>
        public DomainCheckResult[] AreAvailable(params string[] domains)
        {
            return AreAvailableAsync(domains).GetAwaiter().GetResult();
        }

        public async Task<DomainCheckResult[]> AreAvailableAsync(string[] domains, CancellationToken cancellationToken = default)
        {
            // Input validation
            if (domains == null || domains.Length == 0)
            {
                return Array.Empty<DomainCheckResult>();
            }

            if (domains.Length > 50)
            {
                throw new ArgumentException("Only 50 domains are allowed in a single check command", nameof(domains));
            }

            // Validate domain names
            var validDomains = domains.Where(IsValidDomainName).ToArray();
            if (validDomains.Length != domains.Length)
            {
                Console.WriteLine($"Warning: {domains.Length - validDomains.Length} invalid domain names were filtered out");
            }

            if (validDomains.Length == 0)
            {
                return Array.Empty<DomainCheckResult>();
            }

            try
            {
                XDocument doc = await new Query(_params)
                    .AddParameter("DomainList", string.Join(",", validDomains))
                    .ExecuteAsync("namecheap.domains.check", cancellationToken)
                    .ConfigureAwait(false);

                var commandResponse = doc.Root?.Element(_ns + "CommandResponse");
                if (commandResponse == null)
                {
                    throw new ApplicationException("Invalid response structure: CommandResponse element not found");
                }

                return commandResponse.Elements()
                    .Select(ParseDomainCheckResult)
                    .Where(result => result != null)
                    .ToArray();
            }
            catch (System.Xml.XmlException xmlEx)
            {
                Console.WriteLine($"DomainsApi.AreAvailableAsync: XML EXCEPTION: {xmlEx.Message}");
                Console.WriteLine("This usually indicates invalid characters in the API response.");
                throw new ApplicationException("Failed to parse API response due to invalid XML", xmlEx);
            }
            catch (ApplicationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DomainsApi.AreAvailableAsync: EXCEPTION: {ex.Message}");
                throw new ApplicationException($"Unexpected error checking domain availability: {ex.Message}", ex);
            }
        }

        private DomainCheckResult ParseDomainCheckResult(XElement element)
        {
            try
            {
                return new DomainCheckResult()
                {
                    DomainName = GetAttributeValue(element, "Domain"),
                    IsAvailable = GetBooleanAttributeValue(element, "Available"),
                    IsPremiumName = GetBooleanAttributeValue(element, "IsPremiumName"),
                    IcannFee = GetDoubleAttributeValue(element, "IcannFee"),
                    PremiumRegistrationPrice = GetDoubleAttributeValue(element, "PremiumRegistrationPrice")
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing domain check result: {ex.Message}");
                return null;
            }
        }

        private string GetAttributeValue(XElement element, string attributeName, string defaultValue = "")
        {
            return element?.Attribute(attributeName)?.Value ?? defaultValue;
        }

        private bool GetBooleanAttributeValue(XElement element, string attributeName, bool defaultValue = false)
        {
            string value = GetAttributeValue(element, attributeName);
            return !string.IsNullOrEmpty(value) && 
                   value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private double GetDoubleAttributeValue(XElement element, string attributeName, double defaultValue = 0.0)
        {
            string value = GetAttributeValue(element, attributeName);
            return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result) 
                ? result 
                : defaultValue;
        }

        private static bool IsValidDomainName(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return false;

            // Basic domain validation
            if (domain.Length > 253 || domain.Length < 3)
                return false;

            // Check for valid characters and structure
            var domainRegex = new Regex(@"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*$");
            return domainRegex.IsMatch(domain);
        }
        
        public DomainPricingResult GetPricing(
            string productType = "DOMAIN", 
            string productCategory = null,
            string actionName = null,
            string productName = null)
        {
            return GetPricingAsync(productType, productCategory, actionName, productName).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Registers a new domain.
        /// </summary>
        /// <param name="domain">Information about domain to register.</param>
        /// <returns>Information about the created domain.</returns>
        /// <exception cref="ApplicationException">
        /// Exception when the following problems are encountered:
        /// - 2033409	Possibly a logical error at the authentication phase. The order chargeable for the Username is not found
        /// - 2033407, 2033270	Cannot enable Whoisguard when AddWhoisguard is set to NO
        /// - 2015182	Contact phone is invalid. The phone number format is +NNN.NNNNNNNNNN
        /// - 2015267	EUAgreeDelete option should not be set to NO
        /// - 2011170	Validation error from PromotionCode
        /// - 2011280	Validation error from TLD
        /// - 2015167	Validation error from Years
        /// - 2030280	TLD is not supported in API
        /// - 2011168	Nameservers are not valid
        /// - 2011322	Extended Attributes are not valid
        /// - 2010323	Check the required field for billing domain contacts
        /// - 2528166	Order creation failed
        /// - 3019166, 4019166	Domain not available
        /// - 3031166	Error while getting information from the provider
        /// - 3028166	Error from Enom ( Errcount <> 0 )
        /// - 3031900	Unknown response from the provider
        /// - 4023271	Error while adding a free PositiveSSL for the domain
        /// - 3031166	Error while getting a domain status from Enom
        /// - 4023166	Error while adding a domain
        /// - 5050900	Unknown error while adding a domain to your account
        /// - 4026312	Error in refunding funds
        /// - 5026900	Unknown exceptions error while refunding funds
        /// - 2515610	Prices do not match
        /// - 2515623	Domain is premium while considered regular or is regular while considered premium
        /// - 2005	Country name is not valid
        /// </exception>
        public DomainCreateResult Create(DomainCreateRequest domain)
        {
            return CreateAsync(domain).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets contact information for the requested domain.
        /// </summary>
        /// <param name="domain">Domain to get contacts.</param>
        /// <returns>All the contacts, Admin, AuxBilling, Registrant, and Tech for the domain.</returns>
        /// <exception cref="ApplicationException">
        /// Exception when the following problems are encountered:
        /// - 2019166	Domain not found
        /// - 2016166	Domain is not associated with your account
        /// - 4019337	Unable to retrieve domain contacts
        /// - 3016166	Domain is not associated with Enom
        /// - 3019510	This domain has expired/ was transferred out/ is not associated with your account
        /// - 3050900	Unknown response from provider
        /// - 5050900	Unknown exceptions
        /// </exception>
        public DomainContactsResult GetContacts(string domain)
        {
            return GetContactsAsync(domain).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Returns information about the requested domain.
        /// </summary>
        /// <param name="domain">Domain name for which domain information needs to be requested.</param>
        /// <exception cref="ApplicationException">
        /// Exception when the following problems are encountered:
        /// - 5019169	Unknown exceptions
        /// - 2030166	Domain is invalid
        /// - 4011103 - DomainName not Available; or UserName not Available; or Access denied
        /// </exception>
        public DomainInfoResult GetInfo(string domain)
        {
            return GetInfoAsync(domain).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Returns a list of domains for the particular user.
        /// </summary>
        /// <exception cref="ApplicationException">
        /// Exception when the following problems are encountered:
        /// 5050169	Unknown exceptions
        /// </exception>
        public DomainListResult GetList()
        {
            return GetListAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets the Registrar Lock status for the requested domain.
        /// </summary>
        /// <param name="domain">Domain name to get status for.</param>
        /// <returns>true if the domain is locked for registrar transfer, false if unlocked.</returns>
        /// <exception cref="ApplicationException">
        /// Exception when the following problems are encountered:
        /// - 2019166	Domain not found
        /// - 2016166	Domain is not associated with your account
        /// - 3031510	Error response from provider when errorcount !=0
        /// - 3050900	Unknown error response from Enom
        /// - 5050900	Unknown exceptions
        /// </exception>
        public bool GetRegistrarLock(string domain)
        {
            return GetRegistrarLockAsync(domain).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Locks the domain for registrar transfer.
        /// </summary>
        /// <param name="domain">Domain name to lock.</param>
        /// <exception cref="ApplicationException">
        /// Exception when the following problems are encountered:
        /// - 2015278	Invalid data specified for LockAction
        /// - 2019166	Domain not found
        /// - 2016166	Domain is not associated with your account
        /// - 3031510	Error from Enom when Errorcount != 0
        /// - 2030166	Edit permission for domain is not supported
        /// - 3050900	Unknown error response from Enom
        /// - 5050900	Unknown exceptions
        /// </exception>
        public void SetRegistrarLock(string domain)
        {
            SetRegistrarLockAsync(domain).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Unlocks (opens) the domain for registrar transfer.
        /// </summary>
        /// <param name="domain">Domain name to unlock.</param>
        /// <exception cref="ApplicationException">
        /// Exception when the following problems are encountered:
        /// - 2015278	Invalid data specified for LockAction
        /// - 2019166	Domain not found
        /// - 2016166	Domain is not associated with your account
        /// - 3031510	Error from Enom when Errorcount != 0
        /// - 2030166	Edit permission for domain is not supported
        /// - 3050900	Unknown error response from Enom
        /// - 5050900	Unknown exceptions
        /// </exception>
        public void SetRegistrarUnlock(string domain)
        {
            SetRegistrarUnlockAsync(domain).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Returns a list of TLD - top level domains.
        /// </summary>
        /// <exception cref="ApplicationException">
        /// Exception when the following problems are encountered:
        /// - 2011166	UserName is invalid
        /// - 3050900	Unknown response from provider
        /// </exception>
        public TldListResult GetTldList()
        {
            return GetTldListAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Renews an expiring domain.
        /// </summary>
        /// <param name="domain">Domain name to renew.</param>
        /// <param name="years">Number of years to renew.</param>
        /// <returns>information about the renewal, such as the charged amount, or the order Id.</returns>
        /// <exception cref="ApplicationException">
        /// Exception when the following problems are encountered:
        /// - 2033409	Possibly a logical error at the authentication phase. The order chargeable for the Username is not found.
        /// - 2011170	Validation error from PromotionCode
        /// - 2011280	TLD is invalid
        /// - 2528166	Order creation failed
        /// - 2020166	Domain has expired. Please reactivate your domain.
        /// - 3028166	Failed to renew, error from Enom
        /// - 3031510	Error from Enom ( Errcount != 0 )
        /// - 3050900	Unknown error from Enom
        /// - 2016166	Domain is not associated with your account
        /// - 4024167	Failed to update years for your domain
        /// - 4023166	Error occurred during the domain renewal
        /// - 4022337	Error in refunding funds
        /// - 2015170	Promotion code is not allowed for premium domains
        /// - 2015167	Premium domain can be renewed for 1 year only
        /// - 2015610	Premium prices cannot be zero for premium domains
        /// - 2515623	You are trying to renew a premium domain. Premium price should be added to request to renew the premium domain.
        /// - 2511623	Domain name is not premium
        /// - 2515610	Premium price is incorrect. It should be (premium renewal price value).
        /// </exception>
        public DomainRenewResult Renew(string domain, int years)
        {
            return RenewAsync(domain, years).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Reactivates an expired domain.
        /// </summary>
        /// <param name="domain">Domain to reactivate.</param>
        /// <returns>information about the renewal, such as the charged amount, or the order Id.</returns>
        /// <exception cref="ApplicationException">
        /// Exception when the following problems are encountered:
        /// - 2033409	Possibly a logical error at the authentication phase. The order chargeable for the Username is not found.
        /// - 2019166	Domain not found
        /// - 2030166	Edit permission for the domain is not supported
        /// - 2011170	Promotion code is invalid
        /// - 2011280	TLD is invalid
        /// - 2528166	Order creation failed
        /// - 3024510	Error response from Enom while updating the domain
        /// - 3050511	Unknown error response from Enom
        /// - 2020166	Domain does not meet the expiration date for reactivation
        /// - 2016166	Domain is not associated with your account
        /// - 5050900	Unhandled exceptions
        /// - 4024166	Failed to update the domain in your account
        /// - 2015170	Promotion code is not allowed for premium domains
        /// - 2015167	Premium domain can be reactivated for 1 year only
        /// - 2015610	Premium prices cannot be zero for premium domains
        /// - 2515623	You are trying to reactivate a premium domain. Premium price should be added to the request to reactivate the premium domain.
        /// - 2511623	Domain name is not premium
        /// - 2515610	Premium price is incorrect. It should be (premium renewal price value).
        /// </exception>
        public DomainReactivateResult Reactivate(string domain)
        {
            return ReactivateAsync(domain).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sets contact information for the requested domain.
        /// </summary>
        /// <param name="contacts">
        /// The contact information to be set.
        /// All 4 parameters, Registrant, Tech, Admin, and Aux Billig
        /// need to be present. The required fields for each address
        /// are: FirstName, LastName, Address1, StateProvince,
        /// PostalCode, Country, Phone, and EmailAddress.</param>
        /// <exception cref="ApplicationException">
        /// Exception when the following problems are encountered:
        /// - 2019166	Domain not found
        /// - 2030166	Edit permission for domain is not supported
        /// - 2010324	Registrant contacts such as firstname, lastname etc. are missing
        /// - 2010325	Tech contacts such as firstname, lastname etc. are missing
        /// - 2010326	Admin contacts such as firstname, lastname etc. are missing
        /// - 2015182	The contact phone is invalid. The phone number format is +NNN.NNNNNNNNNN
        /// - 2010327	AuxBilling contacts such as firstname, lastname etc. are missing
        /// - 2016166	Domain is not associated with your account
        /// - 2011280	Cannot see the contact information for your TLD
        /// - 4022323	Error retrieving domain Contacts
        /// - 2011323	Error retrieving domain Contacts from Enom (invalid errors)
        /// - 3031510	Error from Enom when error count != 0
        /// - 3050900	Unknown error from Enom
        /// </exception>
        public void SetContacts(DomainContactsRequest contacts)
        {
            SetContactsAsync(contacts).GetAwaiter().GetResult();
        }

        private Dictionary<string, string> GetNamesAndValuesFromProperties(object obj)
        {
            Dictionary<string, string> queryParams = new Dictionary<string, string>();

            if (obj == null)
            {
                return queryParams;
            }

            try
            {
                foreach (System.Reflection.PropertyInfo property in obj.GetType().GetProperties())
                {
                    object value = property.GetValue(obj, null);

                    if (value is ContactInformation contactInformation)
                    {
                        foreach (System.Reflection.PropertyInfo cProperty in contactInformation.GetType().GetProperties())
                        {
                            object cValue = cProperty.GetValue(contactInformation, null);

                            if (cValue != null)
                            {
                                queryParams.Add(property.Name + cProperty.Name, cValue.ToString());
                            }
                        }
                    }
                    else if (value != null)
                    {
                        queryParams.Add(property.Name, value.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetNamesAndValuesFromProperties error: {ex.Message}");
            }

            return queryParams;
        }

        // Async counterparts
        public async Task<DomainPricingResult> GetPricingAsync(
            string productType = "DOMAIN",
            string productCategory = null,
            string actionName = null,
            string productName = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                Query query = new Query(_params)
                    .AddParameter("ProductType", productType);

                if (!string.IsNullOrEmpty(productCategory))
                {
                    query.AddParameter("ProductCategory", productCategory);
                }

                if (!string.IsNullOrEmpty(actionName))
                {
                    query.AddParameter("ActionName", actionName);
                }

                if (!string.IsNullOrEmpty(productName))
                {
                    query.AddParameter("ProductName", productName);
                }

                XDocument doc = await query.ExecuteAsync("namecheap.users.getPricing", cancellationToken).ConfigureAwait(false);
                XElement root = doc.Root;
                if (root == null)
                {
                    throw new ApplicationException("Invalid response: Root element is null");
                }

                XElement commandResponse = root.Element(_ns + "CommandResponse");
                if (commandResponse == null)
                {
                    throw new ApplicationException("Invalid response structure: CommandResponse element not found");
                }

                XElement userGetPricingResult = commandResponse.Element(_ns + "UserGetPricingResult");
                if (userGetPricingResult == null)
                {
                    throw new ApplicationException("Invalid response structure: UserGetPricingResult element not found");
                }

                XElement productTypeElement = userGetPricingResult.Element(_ns + "ProductType");
                if (productTypeElement == null)
                {
                    throw new ApplicationException("Invalid response structure: ProductType element not found");
                }

                XAttribute productTypeNameAttr = productTypeElement.Attribute("Name");
                string productTypeName = productTypeNameAttr?.Value ?? string.Empty;

                DomainPricingResult result = new DomainPricingResult
                {
                    TimeStamp = DateTime.UtcNow,
                    ProductType = productTypeName
                };

                foreach (XElement productCategoryElement in productTypeElement.Elements(_ns + "ProductCategory"))
                {
                    XAttribute categoryNameAttr = productCategoryElement.Attribute("Name");
                    string categoryName = categoryNameAttr?.Value ?? string.Empty;

                    ProductAction responseCategory = new ProductAction
                    {
                        ActionName = categoryName
                    };

                    foreach (XElement productElement in productCategoryElement.Elements(_ns + "Product"))
                    {
                        XAttribute productNameAttr = productElement.Attribute("Name");
                        string prodName = productNameAttr?.Value ?? string.Empty;

                        Product product = new Product
                        {
                            ProductName = prodName
                        };

                        foreach (XElement priceElement in productElement.Elements(_ns + "Price"))
                        {
                            int duration;
                            double price;
                            double regularPrice;
                            double yourPrice;
                            XAttribute durationAttr = priceElement.Attribute("Duration");
                            XAttribute durationTypeAttr = priceElement.Attribute("DurationType");
                            XAttribute priceAttr = priceElement.Attribute("Price");
                            XAttribute regularPriceAttr = priceElement.Attribute("RegularPrice");
                            XAttribute yourPriceAttr = priceElement.Attribute("YourPrice");
                            XAttribute currencyAttr = priceElement.Attribute("Currency");

                            if (durationAttr == null || !int.TryParse(durationAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out duration))
                            {
                                continue; // Skip malformed entry
                            }

                            if (priceAttr == null || !double.TryParse(priceAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out price))
                            {
                                continue;
                            }

                            if (regularPriceAttr == null || !double.TryParse(regularPriceAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out regularPrice))
                            {
                                continue;
                            }

                            if (yourPriceAttr == null || !double.TryParse(yourPriceAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out yourPrice))
                            {
                                continue;
                            }

                            PricingDetails pricingDetails = new PricingDetails
                            {
                                Duration = duration,
                                DurationType = durationTypeAttr?.Value ?? string.Empty,
                                Price = price,
                                RegularPrice = regularPrice,
                                YourPrice = yourPrice,
                                Currency = currencyAttr?.Value ?? string.Empty
                            };

                            product.Prices.Add(pricingDetails);
                        }

                        responseCategory.Products.Add(product);
                    }

                    result.ProductActions.Add(responseCategory);
                }

                return result;
            }
            catch (ApplicationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DomainsApi.GetPricingAsync: EXCEPTION: {ex.Message}");
                throw new ApplicationException($"Unexpected error while getting pricing: {ex.Message}", ex);
            }
        }

        public async Task<DomainCreateResult> CreateAsync(DomainCreateRequest domain, CancellationToken cancellationToken = default)
        {
            if (domain == null)
            {
                throw new ArgumentNullException(nameof(domain));
            }

            try
            {
                Query query = new Query(_params);

                foreach (KeyValuePair<string, string> item in GetNamesAndValuesFromProperties(domain))
                {
                    query.AddParameter(item.Key, item.Value);
                }

                XDocument doc = await query.ExecuteAsync("namecheap.domains.create", cancellationToken).ConfigureAwait(false);
                XElement root = doc.Root;
                if (root == null)
                {
                    throw new ApplicationException("Invalid response: Root element is null");
                }

                XElement commandResponse = root.Element(_ns + "CommandResponse");
                if (commandResponse == null)
                {
                    throw new ApplicationException("Invalid response structure: CommandResponse element not found");
                }

                XElement resultElement = commandResponse.Element(_ns + "DomainCreateResult");
                if (resultElement == null)
                {
                    throw new ApplicationException("Invalid response structure: DomainCreateResult element not found");
                }

                XmlSerializer serializer = new XmlSerializer(typeof(DomainCreateResult), _ns.NamespaceName);
                using (System.Xml.XmlReader reader = resultElement.CreateReader())
                {
                    object deserialized = serializer.Deserialize(reader);
                    DomainCreateResult domainCreateResult = deserialized as DomainCreateResult;
                    if (domainCreateResult == null)
                    {
                        throw new ApplicationException("Failed to deserialize DomainCreateResult");
                    }

                    return domainCreateResult;
                }
            }
            catch (ApplicationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DomainsApi.CreateAsync: EXCEPTION: {ex.Message}");
                throw new ApplicationException($"Unexpected error while creating domain: {ex.Message}", ex);
            }
        }

        public async Task<DomainContactsResult> GetContactsAsync(string domain, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                throw new ArgumentException("Domain cannot be null or empty", nameof(domain));
            }

            try
            {
                XDocument doc = await new Query(_params)
                    .AddParameter("DomainName", domain)
                    .ExecuteAsync("namecheap.domains.getContacts", cancellationToken)
                    .ConfigureAwait(false);

                XElement root = doc.Root;
                if (root == null)
                {
                    throw new ApplicationException("Invalid response: Root element is null");
                }

                XElement commandResponse = root.Element(_ns + "CommandResponse");
                if (commandResponse == null)
                {
                    throw new ApplicationException("Invalid response structure: CommandResponse element not found");
                }

                XElement resultElement = commandResponse.Element(_ns + "DomainContactsResult");
                if (resultElement == null)
                {
                    throw new ApplicationException("Invalid response structure: DomainContactsResult element not found");
                }

                XmlSerializer serializer = new XmlSerializer(typeof(DomainContactsResult), _ns.NamespaceName);
                using (System.Xml.XmlReader reader = resultElement.CreateReader())
                {
                    object deserialized = serializer.Deserialize(reader);
                    DomainContactsResult contactsResult = deserialized as DomainContactsResult;
                    if (contactsResult == null)
                    {
                        throw new ApplicationException("Failed to deserialize DomainContactsResult");
                    }

                    return contactsResult;
                }
            }
            catch (ApplicationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DomainsApi.GetContactsAsync: EXCEPTION: {ex.Message}");
                throw new ApplicationException($"Unexpected error while getting contacts: {ex.Message}", ex);
            }
        }

        public async Task<DomainInfoResult> GetInfoAsync(string domain, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                throw new ArgumentException("Domain cannot be null or empty", nameof(domain));
            }

            try
            {
                XDocument doc = await new Query(_params)
                    .AddParameter("DomainName", domain)
                    .ExecuteAsync("namecheap.domains.getInfo", cancellationToken)
                    .ConfigureAwait(false);

                XElement root = doc.Root;
                if (root == null)
                {
                    throw new ApplicationException("Invalid response: Root element is null");
                }

                XElement commandResponse = root.Element(_ns + "CommandResponse");
                if (commandResponse == null)
                {
                    throw new ApplicationException("Invalid response structure: CommandResponse element not found");
                }

                XElement infoResult = commandResponse.Element(_ns + "DomainGetInfoResult");
                if (infoResult == null)
                {
                    throw new ApplicationException("Invalid response structure: DomainGetInfoResult element not found");
                }

                int id = 0;
                bool isOwner = false;
                XAttribute idAttr = infoResult.Attribute("ID");
                XAttribute ownerNameAttr = infoResult.Attribute("OwnerName");
                XAttribute isOwnerAttr = infoResult.Attribute("IsOwner");
                XElement domainDetails = infoResult.Element(_ns + "DomainDetails");
                XElement dnsDetails = infoResult.Element(_ns + "DnsDetails");

                if (idAttr != null)
                {
                    int.TryParse(idAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out id);
                }

                if (isOwnerAttr != null)
                {
                    bool.TryParse(isOwnerAttr.Value, out isOwner);
                }

                string ownerName = ownerNameAttr?.Value ?? string.Empty;

                DateTime createdDate = DateTime.MinValue;
                DateTime expiredDate = DateTime.MinValue;

                if (domainDetails != null)
                {
                    XElement createdDateElement = domainDetails.Element(_ns + "CreatedDate");
                    XElement expiredDateElement = domainDetails.Element(_ns + "ExpiredDate");
                    if (createdDateElement != null)
                    {
                        createdDate = createdDateElement.Value.ParseNameCheapDate();
                    }
                    if (expiredDateElement != null)
                    {
                        expiredDate = expiredDateElement.Value.ParseNameCheapDate();
                    }
                }

                string dnsProviderType = string.Empty;
                if (dnsDetails != null)
                {
                    XAttribute providerTypeAttr = dnsDetails.Attribute("ProviderType");
                    dnsProviderType = providerTypeAttr?.Value ?? string.Empty;
                }

                DomainInfoResult result = new DomainInfoResult()
                {
                    ID = id,
                    OwnerName = ownerName,
                    IsOwner = isOwner,
                    CreatedDate = createdDate,
                    ExpiredDate = expiredDate,
                    DnsProviderType = dnsProviderType
                };

                return result;
            }
            catch (ApplicationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DomainsApi.GetInfoAsync: EXCEPTION: {ex.Message}");
                throw new ApplicationException($"Unexpected error while getting domain info: {ex.Message}", ex);
            }
        }

        public async Task<DomainListResult> GetListAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                XDocument doc = await new Query(_params).ExecuteAsync("namecheap.domains.getList", cancellationToken).ConfigureAwait(false);
                XElement root = doc.Root;
                if (root == null)
                {
                    throw new ApplicationException("Invalid response: Root element is null");
                }

                XElement commandResponse = root.Element(_ns + "CommandResponse");
                if (commandResponse == null)
                {
                    throw new ApplicationException("Invalid response structure: CommandResponse element not found");
                }

                XmlSerializer serializer = new XmlSerializer(typeof(DomainListResult), _ns.NamespaceName);

                using (System.Xml.XmlReader reader = commandResponse.CreateReader())
                {
                    object deserialized = serializer.Deserialize(reader);
                    DomainListResult listResult = deserialized as DomainListResult;
                    if (listResult == null)
                    {
                        throw new ApplicationException("Failed to deserialize DomainListResult");
                    }

                    return listResult;
                }
            }
            catch (ApplicationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DomainsApi.GetListAsync: EXCEPTION: {ex.Message}");
                throw new ApplicationException($"Unexpected error while getting domain list: {ex.Message}", ex);
            }
        }

        public async Task<bool> GetRegistrarLockAsync(string domain, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                throw new ArgumentException("Domain cannot be null or empty", nameof(domain));
            }

            try
            {
                XDocument doc = await new Query(_params)
                    .AddParameter("DomainName", domain)
                    .ExecuteAsync("namecheap.domains.getRegistrarLock", cancellationToken)
                    .ConfigureAwait(false);

                XElement root = doc.Root;
                if (root == null)
                {
                    throw new ApplicationException("Invalid response: Root element is null");
                }

                XElement commandResponse = root.Element(_ns + "CommandResponse");
                if (commandResponse == null)
                {
                    throw new ApplicationException("Invalid response structure: CommandResponse element not found");
                }

                XElement resultElement = commandResponse.Element(_ns + "DomainGetRegistrarLockResult");
                if (resultElement == null)
                {
                    throw new ApplicationException("Invalid response structure: DomainGetRegistrarLockResult element not found");
                }

                XAttribute statusAttr = resultElement.Attribute("RegistrarLockStatus");
                bool status;
                if (statusAttr == null || !bool.TryParse(statusAttr.Value, out status))
                {
                    throw new ApplicationException("Invalid or missing RegistrarLockStatus attribute");
                }

                return status;
            }
            catch (ApplicationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DomainsApi.GetRegistrarLockAsync: EXCEPTION: {ex.Message}");
                throw new ApplicationException($"Unexpected error while getting registrar lock: {ex.Message}", ex);
            }
        }

        public async Task SetRegistrarLockAsync(string domain, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                throw new ArgumentException("Domain cannot be null or empty", nameof(domain));
            }

            try
            {
                await new Query(_params)
                    .AddParameter("DomainName", domain)
                    .ExecuteAsync("namecheap.domains.setRegistrarLock", cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DomainsApi.SetRegistrarLockAsync: EXCEPTION: {ex.Message}");
                throw new ApplicationException($"Unexpected error while setting registrar lock: {ex.Message}", ex);
            }
        }

        public async Task SetRegistrarUnlockAsync(string domain, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                throw new ArgumentException("Domain cannot be null or empty", nameof(domain));
            }

            try
            {
                await new Query(_params)
                    .AddParameter("DomainName", domain)
                    .AddParameter("LockAction", "UNLOCK")
                    .ExecuteAsync("namecheap.domains.setRegistrarLock", cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DomainsApi.SetRegistrarUnlockAsync: EXCEPTION: {ex.Message}");
                throw new ApplicationException($"Unexpected error while unlocking registrar lock: {ex.Message}", ex);
            }
        }

        public async Task<TldListResult> GetTldListAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                XDocument doc = await new Query(_params).ExecuteAsync("namecheap.domains.getTldList", cancellationToken).ConfigureAwait(false);
                XElement root = doc.Root;
                if (root == null)
                {
                    throw new ApplicationException("Invalid response: Root element is null");
                }

                XElement commandResponse = root.Element(_ns + "CommandResponse");
                if (commandResponse == null)
                {
                    throw new ApplicationException("Invalid response structure: CommandResponse element not found");
                }

                XElement tldsElement = commandResponse.Element(_ns + "Tlds");
                if (tldsElement == null)
                {
                    throw new ApplicationException("Invalid response structure: Tlds element not found");
                }

                XmlSerializer serializer = new XmlSerializer(typeof(TldListResult), _ns.NamespaceName);

                using (System.Xml.XmlReader reader = tldsElement.CreateReader())
                {
                    object deserialized = serializer.Deserialize(reader);
                    TldListResult tldsResult = deserialized as TldListResult;
                    if (tldsResult == null)
                    {
                        throw new ApplicationException("Failed to deserialize TldListResult");
                    }

                    return tldsResult;
                }
            }
            catch (ApplicationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DomainsApi.GetTldListAsync: EXCEPTION: {ex.Message}");
                throw new ApplicationException($"Unexpected error while getting TLD list: {ex.Message}", ex);
            }
        }

        public async Task<DomainRenewResult> RenewAsync(string domain, int years, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                throw new ArgumentException("Domain cannot be null or empty", nameof(domain));
            }
            if (years <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(years), "Years must be greater than zero");
            }

            try
            {
                XDocument doc = await new Query(_params)
                 .AddParameter("DomainName", domain)
                 .AddParameter("Years", years.ToString(CultureInfo.InvariantCulture))
                 .ExecuteAsync("namecheap.domains.renew", cancellationToken)
                 .ConfigureAwait(false);

                XElement root = doc.Root;
                if (root == null)
                {
                    throw new ApplicationException("Invalid response: Root element is null");
                }

                XElement commandResponse = root.Element(_ns + "CommandResponse");
                if (commandResponse == null)
                {
                    throw new ApplicationException("Invalid response structure: CommandResponse element not found");
                }

                XElement resultElement = commandResponse.Element(_ns + "DomainRenewResult");
                if (resultElement == null)
                {
                    throw new ApplicationException("Invalid response structure: DomainRenewResult element not found");
                }

                XmlSerializer serializer = new XmlSerializer(typeof(DomainRenewResult), _ns.NamespaceName);

                using (System.Xml.XmlReader reader = resultElement.CreateReader())
                {
                    object deserialized = serializer.Deserialize(reader);
                    DomainRenewResult renewResult = deserialized as DomainRenewResult;
                    if (renewResult == null)
                    {
                        throw new ApplicationException("Failed to deserialize DomainRenewResult");
                    }

                    return renewResult;
                }
            }
            catch (ApplicationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DomainsApi.RenewAsync: EXCEPTION: {ex.Message}");
                throw new ApplicationException($"Unexpected error while renewing domain: {ex.Message}", ex);
            }
        }

        public async Task<DomainReactivateResult> ReactivateAsync(string domain, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                throw new ArgumentException("Domain cannot be null or empty", nameof(domain));
            }

            try
            {
                XDocument doc = await new Query(_params)
                 .AddParameter("DomainName", domain)
                 .ExecuteAsync("namecheap.domains.reActivate", cancellationToken)
                 .ConfigureAwait(false);

                XElement root = doc.Root;
                if (root == null)
                {
                    throw new ApplicationException("Invalid response: Root element is null");
                }

                XElement commandResponse = root.Element(_ns + "CommandResponse");
                if (commandResponse == null)
                {
                    throw new ApplicationException("Invalid response structure: CommandResponse element not found");
                }

                XElement resultElement = commandResponse.Element(_ns + "DomainReactivateResult");
                if (resultElement == null)
                {
                    throw new ApplicationException("Invalid response structure: DomainReactivateResult element not found");
                }

                XmlSerializer serializer = new XmlSerializer(typeof(DomainReactivateResult), _ns.NamespaceName);

                using (System.Xml.XmlReader reader = resultElement.CreateReader())
                {
                    object deserialized = serializer.Deserialize(reader);
                    DomainReactivateResult reactivateResult = deserialized as DomainReactivateResult;
                    if (reactivateResult == null)
                    {
                        throw new ApplicationException("Failed to deserialize DomainReactivateResult");
                    }

                    return reactivateResult;
                }
            }
            catch (ApplicationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DomainsApi.ReactivateAsync: EXCEPTION: {ex.Message}");
                throw new ApplicationException($"Unexpected error while reactivating domain: {ex.Message}", ex);
            }
        }

        public async Task SetContactsAsync(DomainContactsRequest contacts, CancellationToken cancellationToken = default)
        {
            if (contacts == null)
            {
                throw new ArgumentNullException(nameof(contacts));
            }

            try
            {
                Query query = new Query(_params);

                foreach (KeyValuePair<string, string> item in GetNamesAndValuesFromProperties(contacts))
                {
                    query.AddParameter(item.Key, item.Value);
                }

                await query.ExecuteAsync("namecheap.domains.setContacts", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DomainsApi.SetContactsAsync: EXCEPTION: {ex.Message}");
                throw new ApplicationException($"Unexpected error while setting contacts: {ex.Message}", ex);
            }
        }
    }
}