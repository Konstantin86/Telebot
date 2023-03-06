using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telebot.Utilities;

namespace Telebot.Model
{
    public class AssetIdeasSentiment
    {
        // TODO thing of filtering too old ideas / limiting number of ideas
        public List<Idea> Ideas { get; set; } = new List<Idea>();

        public string GetSentiment()
        {
            return (float)Ideas.Where(m => m.IsLong).Sum(m => m.Rating) / (float)Ideas.Sum(m => m.Rating) > 0.5 ? "Long" : "Short";

            //return (float)Ideas.Count(m => m.IsLong) / Ideas.Count > 0.5 ? "Long" : "Short";
        }

        public float GetPercentage()
        {
            var sentiment = GetSentiment() == "Long";
            var sum = Ideas.Where(m => sentiment ? m.IsLong : !m.IsLong).Sum(m => m.Rating);
            var sumOverall = Ideas.Sum(m => m.Rating);

            return sum == 0 || sumOverall == 0
                ? (float)0.5
                : (float)sum / (float)sumOverall;

            return (float)Ideas.Where(m => sentiment ? m.IsLong : !m.IsLong).Sum(m => m.Rating) / (float)Ideas.Sum(m => m.Rating);
            //return (float)Ideas.Count(m => sentiment ? m.IsLong : !m.IsLong) / Ideas.Count;
        }

        public double GetFreshness()
        {
            return Ideas.Select(m => (DateTime.Now - m.Time).TotalDays).Percentile(0.5);
        }

        public override string ToString()
        {
            return $"{GetSentiment()} ({GetPercentage().ToString("P0")})";
        }

        public string ToString(string format)
        {
            if (format == "long")
            {
                var sb = new StringBuilder();

                sb.AppendLine(ToString());
                sb.AppendLine("________");
                sb.AppendLine(string.Join(Environment.NewLine, Ideas.OrderByDescending(m => m.Time).Select(m => $"{(m.IsLong ? "Long " : "Short")} ({m.Rating})   {m.Time.ToString("g")}")));

                return sb.ToString();
            }
            else
                return ToString();
        }
    }
    public class Idea
    {
        public bool IsLong { get; set; }
        public DateTime Time { get; set; }
        public int Rating { get; set; }
        public string Asset { get; set; }
        public string Link { get; set; }
        public string LinkCaption { get; set; }
        public string TimeFrameText { get; set; }

        public string ToString(bool showDate = true, bool showAsset = true)
        {
            return $"<a href=\"{Link}\">{(showAsset ? $"{Asset} " : "")}{(IsLong ? "Long" : "Short")} ({Rating})</a> ({(showDate ? Time.ToString("g") : Time.ToString("t"))}) (<a href=\"{Asset.ToTradingViewAssetChartLink(240)}\">4h</a>, <a href=\"{Asset.ToTradingViewAssetChartLink(1440)}\">1d</a>)";
        }
    }
}
