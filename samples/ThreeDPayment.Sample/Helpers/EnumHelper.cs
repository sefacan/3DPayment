/*
   Support: fsefacan@gmail.com
*/

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ThreeDPayment.Sample.Helpers
{
    public static class EnumHelper
    {
        public static string GetDisplayName(this Enum value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            DisplayAttribute attribute = value.GetType().GetField(value.ToString())
                .GetCustomAttributes(typeof(DisplayAttribute), false).Cast<DisplayAttribute>().FirstOrDefault();

            if (attribute == null)
            {
                return value.ToString();
            }

            string propValue = attribute.Name;
            return propValue.ToString();
        }
    }
}