using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telebot.Trading.TA
{
    public class TaLibManager
    {
        public TaMaResult Ma(IEnumerable<double> close, TALib.Core.MAType maType = default, int interval = 30)
        {
            double[] closeArray = close.ToArray();
            double[] outMa = new double[closeArray.Length];
            int startIdx;
            int nbElement;

            TALib.Core.Ma(closeArray, 0, closeArray.Length - 1, outMa, out startIdx, out nbElement, maType, interval);
            return new TaMaResult(outMa, startIdx, nbElement, interval);
        }
    }

    public class TaMaResult
    {
        public TaMaResult(double[] outMacd, int startIdx, int nbElement, int interval)
        {
            OutMa = outMacd;
            StartIdx = startIdx;
            NbElement = nbElement;
            Interval = interval;
        }

        public double Current => OutMa[NbElement - 1];
        public double Previous => OutMa[NbElement - 2];

        public int Interval { get; set; }

        public double FromLast(int index)
        {
            return OutMa[NbElement - index];
        }

        public double[] OutMa { get; }
        public int StartIdx { get; }
        public int NbElement { get; }

        public int Count()
        {
            return OutMa.Where(m => m > 0).Count();
        }

        public List<double> TakeLast(int num)
        {
            var result = new List<double>();

            for (int i = num; i > 0; i--)
            {
                result.Add(OutMa[NbElement - i]);
            }

            return result;
        }

        public double this[int index] => OutMa[index];
    }
}
