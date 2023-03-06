using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telebot.Utilities
{
    public static class NumericExtensions
    {
        public static bool IsInRange(this double price, double min, double max)
        {
            return price >= min && price <= max;
        }

        public static double GetThreeDigitsRounder(this double number)
        {
            return Convert.ToDouble(100 / Math.Pow(10, 5 - ((int)Math.Floor(Math.Log10(Math.Abs(number))) + 1)));
        }

        public static double PercentileOf(this double value, double[] values)
        {
            Array.Sort(values);
            int index = Array.BinarySearch(values, value);
            if (index < 0)
            {
                index = ~index;
                if (index == 0)
                {
                    return 0;
                }
                else if (index == values.Length)
                {
                    return 1;
                }
                else
                {
                    double lowerValue = values[index - 1];
                    double upperValue = values[index];
                    double interpolation = (value - lowerValue) / (upperValue - lowerValue);
                    return ((index - 1) + interpolation) / (values.Length - 1);
                }
            }
            else
            {
                return (double)index / (values.Length - 1);
            }
        }

        public static decimal PercentileOf(this decimal value, decimal[] values)
        {
            Array.Sort(values);
            int index = Array.BinarySearch(values, value);
            if (index < 0)
            {
                index = ~index;
                if (index == 0)
                {
                    return 0;
                }
                else if (index == values.Length)
                {
                    return 1;
                }
                else
                {
                    decimal lowerValue = values[index - 1];
                    decimal upperValue = values[index];
                    decimal interpolation = (value - lowerValue) / (upperValue - lowerValue);
                    return ((index - 1) + interpolation) / (values.Length - 1);
                }
            }
            else
            {
                return (decimal)index / (values.Length - 1);
            }
        }

        public static decimal PercentileOf(this int number, List<int> range)
        {
            range.Sort();
            var distinctRange = range.Distinct().ToList();
            int index = distinctRange.IndexOf(number);
            if (index == 0) index = 1;
            return index / (decimal)distinctRange.Count;
        }

        public static double FindClosestValue(this double value, List<double> collection)
        {
            collection.Sort();  // Sort the list of values

            int index = collection.BinarySearch(value);  // Find the index of the target value
            if (index >= 0)
            {
                return collection[index];  // The target value is in the list
            }
            else
            {
                int nearestIndex = ~index;  // The target value is not in the list

                if (nearestIndex == 0)
                {
                    return collection[0];  // The target value is less than all values in the list
                }
                else if (nearestIndex == collection.Count)
                {
                    return collection[collection.Count - 1];  // The target value is greater than all values in the list
                }
                else
                {
                    double lowerValue = collection[nearestIndex - 1];
                    double upperValue = collection[nearestIndex];

                    if (value - lowerValue < upperValue - value)
                    {
                        return lowerValue;  // The lower value is closer to the target value
                    }
                    else
                    {
                        return upperValue;  // The upper value is closer to the target value
                    }
                }
            }
        }
    }
}
