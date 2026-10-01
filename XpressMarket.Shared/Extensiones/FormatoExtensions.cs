using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace XpressMarket.Shared.Extensions
{
    public static class FormatoExtensions
    {
        private static readonly NumberFormatInfo FormatoBoliviano = new NumberFormatInfo
        {
            NumberDecimalSeparator = ",",
            NumberGroupSeparator = ".",
            NumberDecimalDigits = 2
        };

        public static string Bs(this decimal valor)
        {
            return $"Bs {valor.ToString("N2", FormatoBoliviano)}";
        }
    }
}
