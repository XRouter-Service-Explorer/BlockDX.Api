using BlockDX.Api.Controllers.ViewModels;
using BlockDX.Api.Core.Models;
using BlockDX.Api.Enums;
using Blocknet.Lib.Services.Coins.Blocknet;
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
        private readonly IXBridgeService _xBridgeService;

        public BlockDXController(
            IHttpClientFactory httpClientFactory,
            IXBridgeService xBridgeService)
        {
            _httpClientFactory = httpClientFactory;
            _xBridgeService = xBridgeService;
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetOpenOrdersPerMarket()
        {
            //var orders = _xBridgeService.dxGetOrders();
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
        public IActionResult GetTotalTradesCount(TimeInterval timeInterval)
        {
            var assetWhiteList = _xBridgeService.dxGetNetworkTokens();

            // 1 BLOCK is 60 seconds.

            var blocks = timeIntervalToBlockAmount(timeInterval);

            var tradeHistoryResponse = _xBridgeService.dxGetTradingData(blocks, false);

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
        public async Task<IActionResult> GetTotalVolumePerCoin(string units, TimeInterval timeInterval)
        {
            var unitList = units.Split(",").ToList();
            if (unitList.Count == 0)
                return BadRequest("No units specified");

            return Ok(await getTotalVolumePerCoin(unitList, timeInterval));
        }

        private async Task<List<TokenTradeStatistics>> getTotalVolumePerCoin(List<string> units, TimeInterval timeInterval)
        {
            var assetWhiteList = _xBridgeService.dxGetNetworkTokens();

            var blocks = timeIntervalToBlockAmount(timeInterval);

            var tradeHistoryResponse = _xBridgeService.dxGetTradingData(blocks, false);

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
        public async Task<IActionResult> GetTotalVolume(string coin, string units, TimeInterval timeInterval)
        {
            var unitList = units.Split(",").ToList();
            if (string.IsNullOrEmpty(coin))
                return BadRequest("No coins specified");
            if (unitList.Count == 0)
                return BadRequest("No units specified");

            var oneDayTotalVolumePerCoin = await getTotalVolumePerCoin(unitList, timeInterval);

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
        public IActionResult GetTotalCompletedOrders(TimeInterval timeInterval)
        {
            var assetWhiteList = _xBridgeService.dxGetNetworkTokens();

            var blocks = timeIntervalToBlockAmount(timeInterval);

            var tradeHistoryResponse = _xBridgeService.dxGetTradingData(blocks, false);

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

        private int timeIntervalToBlockAmount(TimeInterval timeInterval)
        {
            // 1 block is one minute for BLOCK
            int blocks;

            switch (timeInterval)
            {
                case TimeInterval.FifteenMinutes:
                    blocks = 15;
                    break;
                case TimeInterval.Hour:
                    blocks = 60;
                    break;
                case TimeInterval.Day:
                    blocks = 60 * 24;
                    break;
                case TimeInterval.Week:
                    blocks = 60 * 24 * 7;
                    break;
                case TimeInterval.Month:
                    blocks = 60 * 24 * 7 * 4;
                    break;
                case TimeInterval.Year:
                    blocks = 60 * 24 * 7 * 52;
                    break;
                default:
                    blocks = 0;
                    break;
            }
            return blocks;
        }
    }
}
