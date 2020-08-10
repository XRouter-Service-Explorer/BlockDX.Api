using System.Collections.Generic;

namespace BlockDX.Api.Core.Models
{
    public class CoinExchangeRate
    {
        public string Coin { get; set; }
        public List<ExchangeRate> Rates { get; set; }
    }

    public class ExchangeRate
    {
        public string Quote { get; set; }
        public decimal Rate { get; set; }
    }
}
