using System;
using System.Collections.Generic;

namespace NameCheap
{
    public class PricingDetails
    {
        public int Duration { get; set; }
        public string DurationType { get; set; }
        public double Price { get; set; }
        public double RegularPrice { get; set; }
        public double YourPrice { get; set; }
        public string Currency { get; set; }
    }

    public class Product
    {
        public string ProductName { get; set; }
        public List<PricingDetails> Prices { get; set; } = new List<PricingDetails>();
    }

    public class ProductAction
    {
        public string ActionName { get; set; }
        public List<Product> Products { get; set; } = new List<Product>();
    }

    public class DomainPricingResult
    {
        public string ProductType { get; set; }
        public List<ProductAction> ProductActions { get; set; } = new List<ProductAction>();
    }
}
