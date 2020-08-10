using BlockDX.Api.Controllers.ViewModels;
using BlockDX.Api.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace BlockDX.Api.Controllers
{
    [ApiController]
    [Route("api/dx")]
    public class BlockDXController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public BlockDXController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetOpenOrdersPerMarket()
        {
            string baseUrl = "https://data.blocknet.co/api/v2.0/dxgetorders";

            var client = _httpClientFactory.CreateClient();

            var getOrdersTask = client.GetStringAsync(baseUrl);
            var orders = JsonConvert.DeserializeObject<List<OpenOrder>>(await getOrdersTask);

            var activeMarkets = orders.GroupBy(o => new { o.Maker, o.Taker })
                    .Select(group => new
                    {
                        Market = group.Key,
                        Count = group.Count()
                    }).OrderByDescending(am => am.Count).ToList();

            return Ok(activeMarkets);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetOneDayTotalTradesCount()
        {
            var assetWhiteList = await GetBlockDXAssets();

            var tradeHistoryResponse = await GetOneDayTradeHistory();

            var tradeHistories = tradeHistoryResponse
                .Where(p =>
                {
                    var trade = new List<string> { p.Maker, p.Taker };
                    var result = trade.All(ta => assetWhiteList.Contains(ta));
                    return result;
                })
                .ToList();

            return Ok(tradeHistories.Count);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetOneDayTotalVolumePerCoin([FromQuery]string units)
        {
            var unitList = units.Split(",").ToList();
            if (unitList.Count == 0)
                return BadRequest("No units specified");

            return Ok(await getOneDayTotalVolumePerCoin(unitList));
        }

        private async Task<List<TokenTradeStatistics>> getOneDayTotalVolumePerCoin(List<string> units)
        {
            var assetWhiteList = await GetBlockDXAssets();

            var tradeHistoryResponse = await GetOneDayTradeHistory();

            var tradeStatisticsTokens = new List<TokenTradeStatistics>();

            if (tradeHistoryResponse.Count == 0)
                return tradeStatisticsTokens;

            var tradeHistories = tradeHistoryResponse
                .Where(p =>
                {
                    var trade = new List<string> { p.Maker, p.Taker };
                    var result = trade.All(ta => assetWhiteList.Contains(ta));
                    return result;
                })
                .ToList();

            var coins = tradeHistoryResponse.Select(r => r.Maker).Distinct().ToList();

            var unitSet = units.Union(coins).Distinct().ToList();

            var client = _httpClientFactory.CreateClient("coininfo");

            string url = "GetExchangeRates?coins=" + string.Join(",", coins) + "&units=" + string.Join(",", unitSet);

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            httpRequest.Headers.Add("apiName", "CryptoCompare");

            var multiPriceTask = await client.SendAsync(httpRequest);
            var multiPriceResponse = await multiPriceTask.Content.ReadAsStringAsync();
            var multiPrice = JsonConvert.DeserializeObject<List<CoinExchangeRate>>(multiPriceResponse);

            foreach (var coin in multiPrice)
            {
                var sumMaker = tradeHistories.Where(th => th.Maker.Equals(coin.Coin)).Sum(th => th.MakerSize);

                var tokenVolumesPerUnit = new List<TokenVolumeViewModel>();

                var unitsOfCoin = new HashSet<string>(units);
                unitsOfCoin.Add(coin.Coin);
                foreach (var unit in unitsOfCoin)
                {
                    var coinPrice = coin.Rates.FirstOrDefault(r => r.Quote.Equals(unit));
                    if (coinPrice == null)
                    {
                        tokenVolumesPerUnit.Add(new TokenVolumeViewModel
                        {
                            Unit = unit,
                            Volume = (sumMaker) * 1
                        });
                    }
                    else
                    {
                        tokenVolumesPerUnit.Add(new TokenVolumeViewModel
                        {
                            Unit = unit,
                            Volume = (sumMaker) * coinPrice.Rate
                        });
                    }

                }
                var countCoinTrades = tradeHistories.Count(th => th.Maker.Equals(coin.Coin));
                tradeStatisticsTokens.Add(new TokenTradeStatistics
                {
                    Coin = coin.Coin,
                    TradeCount = countCoinTrades,
                    Volumes = tokenVolumesPerUnit
                });
            }
            return tradeStatisticsTokens;
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetOneDayTotalVolume([FromQuery] string coin, [FromQuery]string units)
        {
            var unitList = units.Split(",").ToList();
            if (string.IsNullOrEmpty(coin))
                return BadRequest("No coins specified");
            if (unitList.Count == 0)
                return BadRequest("No units specified");

            var oneDayTotalVolumePerCoin = await getOneDayTotalVolumePerCoin(unitList);

            var totalVolumePerUnit = new List<TokenVolumeViewModel>();

            if (!oneDayTotalVolumePerCoin.Any(vc => vc.Coin.Equals(coin)) && !coin.Equals("0"))
            {
                foreach (var unit in unitList)
                {
                    totalVolumePerUnit.Add(new TokenVolumeViewModel
                    {
                        Unit = unit,
                        Volume = 0
                    });
                }
                return Ok(totalVolumePerUnit);
            }

            foreach (var unit in unitList)
            {
                var sumVolume = oneDayTotalVolumePerCoin.Sum(vc => vc.Volumes.Where(vc => vc.Unit.Equals(unit)).Sum(vc => vc.Volume));

                totalVolumePerUnit.Add(new TokenVolumeViewModel
                {
                    Unit = unit,
                    Volume = sumVolume
                });
            }

            return Ok(totalVolumePerUnit);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetOneDayCompletedOrders()
        {
            var assetWhiteList = await GetBlockDXAssets();

            var tradeHistoryResponse = await GetOneDayTradeHistory();

            var tradeStatisticsTokens = new List<TokenTradeStatistics>();

            var tradeHistories = tradeHistoryResponse
                .Where(p =>
                {
                    var trade = new List<string> { p.Maker, p.Taker };
                    var result = trade.All(ta => assetWhiteList.Contains(ta));
                    return result;
                })
                .ToList();

            var coins = tradeHistoryResponse
                .SelectMany(coin => new[] { coin.Taker, coin.Maker })
                .GroupBy(c => c).Select(group => new
                {
                    Coin = group.Key,
                    Count = group.Count()
                })
                .ToList();

            return Ok(coins);
        }

        private async Task<List<DXAtomicSwap>> GetOneDayTradeHistory()
        {
            string baseUrl = "https://data.blocknet.co/api/v2.0/history";

            var client = _httpClientFactory.CreateClient();

            var getTradeHistoryTask = client.GetStringAsync(baseUrl);
            return JsonConvert.DeserializeObject<List<DXAtomicSwap>>(await getTradeHistoryTask);
        }

        private async Task<List<string>> GetBlockDXAssets()
        {
            string baseUrl = "https://data.blocknet.co/api/v2.0/dxgetnetworktokens";

            var client = _httpClientFactory.CreateClient();

            var getAssetsTask = client.GetStringAsync(baseUrl);
            var assets = JsonConvert.DeserializeObject<List<string>>(await getAssetsTask);

            return assets;
        }
    }
}
