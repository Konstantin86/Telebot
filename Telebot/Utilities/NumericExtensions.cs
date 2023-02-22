using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telebot.Utilities
{
    public static class NumericExtensions
    {
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
    }
}
