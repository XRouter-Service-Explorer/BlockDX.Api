using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlockDX.Api.Core.Models
{
    public class DXAtomicSwap
    {
        public long Timestamp { get; set; }
        [JsonProperty(PropertyName = "fee_txid")]
        public string FeeTxId { get; set; }
        public string NodePubKey { get; set; }
        public string Taker { get; set; }
        public string Id { get; set; }
        public string Maker { get; set; }
        [JsonProperty(PropertyName = "maker_size")]
        public decimal MakerSize { get; set; }
        [JsonProperty(PropertyName = "taker_size")]
        public decimal TakerSize { get; set; }
    }
}
