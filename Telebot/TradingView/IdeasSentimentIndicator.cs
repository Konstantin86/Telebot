using Telebot.Model;
using Telebot.TradingView;
using Telebot.Utilities;

namespace Telebot.Indicators
{
    public class IdeasSentimentIndicator
    {
        private TradingViewClient tradingViewClient;
        private IEnumerable<string> assets;

        public Dictionary<string, AssetIdeasSentiment> AssetIdeaSentimentMap { get; set; }

        public IdeasSentimentIndicator(TradingViewClient tradingViewClient, IEnumerable<string> assets)
        {
            AssetIdeaSentimentMap = new Dictionary<string, AssetIdeasSentiment>();

            this.tradingViewClient = tradingViewClient;
            this.assets = assets;

            LoadInitialData();
        }

        private void LoadInitialData()
        {
            RefreshIdeasSentimentData();
        }

        private void RefreshIdeasSentimentData()
        {
            Parallel.ForEach(assets, new ParallelOptions { MaxDegreeOfParallelism = 3 }, asset => 
            {
                AssetIdeasSentiment assetIdeasSentiment = tradingViewClient.GetIdeaSentiment(asset);

                if (assetIdeasSentiment != null && assetIdeasSentiment.Ideas.Count > 0)
                {
                    AssetIdeaSentimentMap[asset] = assetIdeasSentiment;
                }
                else
                {
                    if (AssetIdeaSentimentMap.ContainsKey(asset))
                    {
                        AssetIdeaSentimentMap.Remove(asset);
                    }
                }
            }); 

            //foreach (var asset in assets)
            //{
            //    AssetIdeasSentiment assetIdeasSentiment = tradingViewClient.GetIdeaSentiment(asset);

            //    if (assetIdeasSentiment != null && assetIdeasSentiment.Ideas.Count > 0)
            //    {
            //        AssetIdeaSentimentMap[asset] = assetIdeasSentiment;
            //    }
            //    else
            //    {
            //        if (AssetIdeaSentimentMap.ContainsKey(asset))
            //        {
            //            AssetIdeaSentimentMap.Remove(asset);
            //        }
            //    }
            //}
        }

        public void SubscribeOnUpdates()
        {
            var ideasSentimentUpdatesTimer = new System.Timers.Timer(24 * 60 * 1000); // once per day
            ideasSentimentUpdatesTimer.Elapsed += FundamentalsUpdatesHandler;
            ideasSentimentUpdatesTimer.AutoReset = true;
            ideasSentimentUpdatesTimer.Enabled = true;
        }

        private void FundamentalsUpdatesHandler(object sender, System.Timers.ElapsedEventArgs e)
        {
            RefreshIdeasSentimentData();
        }

        public decimal GetRelativeSentimentStrength(string asset)
        {
            return NumericExtensions.PercentileOf(AssetIdeaSentimentMap[asset.ToUpper()].Ideas.Sum(m => m.Rating), AssetIdeaSentimentMap.Values.Select(m => m != null && m.Ideas != null ? m.Ideas.Sum(m => m.Rating) : 0).ToList());
        }
    }
}
