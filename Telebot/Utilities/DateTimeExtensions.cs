using Binance.Net.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telebot.Utilities
{
    public static class DateTimeExtensions
    {
        public static DateTime ToServerTime(this DateTime dt)
        {
            return DateTimeOffset.Now.Offset.TotalHours == 3 ? dt : dt.AddHours(-3);
        }

        public static string ToCompactFormat(this DateTime dt)
        {
            return dt.ToString("M/d/yy HH:mm");
        }

        public static DateTime ToClientTime(this DateTime dt)
        {
            return DateTimeOffset.Now.Offset.TotalHours == 3 ? dt : dt.AddHours(3);
        }

        public static DateTime StartOfDay(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0);
        }

        public static DateTime EndOfDay(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59);
        }

        public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }

        public static DateTime StartOfMonth(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, 1);
        }

        public static DateTime EndOfMonth(this DateTime dt)
        {
            return StartOfMonth(dt).AddMonths(1).AddSeconds(-1);
        }

        public static DateTime RewindPeriodsBack(this DateTime dateTime, KlineInterval interval, int periodsNum)
        {
            TimeSpan ts = ParseTimeSpan(interval);

            return dateTime.Subtract(ts * periodsNum);
        }

        public static DateTime RewindPeriodsForward(this DateTime dateTime, KlineInterval interval, int periodsNum)
        {
            TimeSpan ts = ParseTimeSpan(interval);

            return dateTime.Add(ts * periodsNum);
        }

        private static TimeSpan ParseTimeSpan(KlineInterval interval)
        {
            TimeSpan ts = TimeSpan.FromMinutes(1);

            switch (interval)
            {
                case KlineInterval.OneMinute:
                    ts = TimeSpan.FromMinutes(1);
                    break;
                case KlineInterval.ThreeMinutes:
                    ts = TimeSpan.FromMinutes(3);
                    break;
                case KlineInterval.FiveMinutes:
                    ts = TimeSpan.FromMinutes(5);
                    break;
                case KlineInterval.FifteenMinutes:
                    ts = TimeSpan.FromMinutes(15);
                    break;
                case KlineInterval.ThirtyMinutes:
                    ts = TimeSpan.FromMinutes(30);
                    break;
                case KlineInterval.OneHour:
                    ts = TimeSpan.FromHours(1);
                    break;
                case KlineInterval.TwoHour:
                    ts = TimeSpan.FromHours(2);
                    break;
                case KlineInterval.FourHour:
                    ts = TimeSpan.FromHours(4);
                    break;
                case KlineInterval.SixHour:
                    ts = TimeSpan.FromHours(6);
                    break;
                case KlineInterval.EightHour:
                    ts = TimeSpan.FromHours(8);
                    break;
                case KlineInterval.TwelveHour:
                    ts = TimeSpan.FromHours(12);
                    break;
                case KlineInterval.OneDay:
                    ts = TimeSpan.FromDays(1);
                    break;
                case KlineInterval.ThreeDay:
                    ts = TimeSpan.FromDays(3);
                    break;
                case KlineInterval.OneWeek:
                    ts = TimeSpan.FromDays(7);
                    break;
                case KlineInterval.OneMonth:
                    ts = TimeSpan.FromDays(30);
                    break;
            }

            return ts;
        }
    }
}
