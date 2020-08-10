using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlockDX.Api.Controllers.ViewModels
{
    public class TokenTradeStatistics
    {
        public string Coin { get; set; }
        public List<TokenVolumeViewModel> Volumes { get; set; }
        public int TradeCount { get; set; }
    }
}
