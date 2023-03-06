using HtmlAgilityPack;
using Telebot.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Telebot.TradingView
{
    public class TradingViewClient
    {
        public AssetIdeasSentiment GetIdeaSentiment(string asset)
        {
            //if (asset is in top 10 caps) call /https://www.tradingview.com/symbols/BTCUSDT/ideas/page-3/ and collect ideas in loop

            Console.WriteLine($"Loading ideas for {asset}...");

            var url = $"https://www.tradingview.com/symbols/{asset}USDT/ideas/";
            var web = new HtmlWeb();

            HtmlDocument doc = null;

            int retries = 5;

            while (retries-- > 0)
            {
                try
                {
                    doc = web.Load(url);
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to retieve ideas for {asset}, will retry in 3 seconds, number of retires left: {retries}");
                    retries--;
                    Thread.Sleep(3000);
                }
            }

            if (doc == null)
            {
                throw new Exception("Failed to load ideas web page");
            }

            var ideasNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'tv-widget-idea js-userlink-popup-anchor')]");

            if (ideasNodes == null)
            {
                return null;
            }

            var ideaSentiment = new AssetIdeasSentiment();

            foreach (var ideaNode in ideasNodes)
            {
                var title = ideaNode
                        .SelectNodes(".//a[contains(@class, 'tv-widget-idea__title')]");

                var link = "https://www.tradingview.com" + title.First().Attributes["href"].Value;
                var linkCaption = title.First().InnerText;

                var timeFrame = ideaNode
                        .SelectNodes(".//span[contains(@class, 'tv-widget-idea__timeframe')]");

                var timeFrameText = timeFrame.Last().InnerText;

                var longs = ideaNode
                        .SelectNodes(".//span[text()='Long']");

                var shorts = ideaNode
                        .SelectNodes(".//span[text()='Short']");

                var time = ideaNode
                        .SelectNodes(".//span[contains(@class,'tv-card-stats__time')]");

                var votesCount = ideaNode
                        .SelectNodes(".//span[contains(@class,'tv-card-social-item__count')]");

                DateTimeOffset ? dateTimeOffset = null;
                if (time != null)
                {
                    var epochTime = time.First(m => m.Attributes.Contains("data-timestamp")).Attributes["data-timestamp"].Value;
                    dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(epochTime.Substring(0, epochTime.Length - 2))).ToLocalTime();
                }

                if (longs != null || shorts != null)
                {
                    if ((DateTime.Now - dateTimeOffset.Value).TotalDays <= 7)
                    {
                        var idea = new Idea();
                        idea.Asset = asset;
                        idea.Link = link;
                        idea.LinkCaption = linkCaption;
                        idea.IsLong = longs != null;
                        idea.Time = dateTimeOffset.Value.DateTime;
                        idea.TimeFrameText = timeFrameText;
                        idea.Rating = Convert.ToInt32(votesCount.First().InnerText);

                        ideaSentiment.Ideas.Add(idea);
                    }
                    else
                    {

                    }
                }
            }

            return ideaSentiment;
        }

        public static double Percentile(IEnumerable<double> seq, double percentile)
        {
            var elements = seq.ToArray();
            Array.Sort(elements);
            double realIndex = percentile * (elements.Length - 1);
            int index = (int)realIndex;
            double frac = realIndex - index;
            if (index + 1 < elements.Length)
                return elements[index] * (1 - frac) + elements[index + 1] * frac;
            else
                return elements[index];
        }
    }
}
