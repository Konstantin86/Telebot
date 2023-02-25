using System.Diagnostics.Metrics;
using System.Text;
using Telebot.Utilities;

namespace Telebot.Trading
{
    public class PriceLevelNotification
    {
        public double PriceLevel { get; set; }
        public DateTime LastNotifiedOn { get; set; }
        public PriceLevelNotificationType NotificaitonType { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Price ");

            var rounder = PriceLevel.GetThreeDigitsRounder();
            var levelPriceRounded = Math.Round(PriceLevel / rounder) * rounder;

            if (NotificaitonType == PriceLevelNotificationType.PriceGoesUpToTheLevel)
            {
                sb.Append($"goes up to the price level {levelPriceRounded.ToString("G5")}$ at {LastNotifiedOn.ToString("g")}");
            }
            else if (NotificaitonType == PriceLevelNotificationType.PriceGoesDownToTheLevel)
            {
                sb.Append($"goes down to the price level {levelPriceRounded.ToString("G5")}$ at {LastNotifiedOn.ToString("g")}");
            }
            else if (NotificaitonType == PriceLevelNotificationType.PriceBouncesUpFromTheLevel)
            {
                sb.Append($"bounces up from the price level {levelPriceRounded.ToString("G5")}$ at {LastNotifiedOn.ToString("g")}");
            }
            else if (NotificaitonType == PriceLevelNotificationType.PriceBouncesDownFromTheLevel)
            {
                sb.Append($"bounces down from the price level {levelPriceRounded.ToString("G5")}$ at {LastNotifiedOn.ToString("g")}");
            }

            return sb.ToString();
        }
    }

    public enum PriceLevelNotificationType
    {
        PriceGoesUpToTheLevel,
        PriceGoesDownToTheLevel,
        PriceBouncesUpFromTheLevel,
        PriceBouncesDownFromTheLevel
    }
}
