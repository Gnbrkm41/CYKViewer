using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CYKViewer
{
    class UrlValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value is not string str)
            {
                return new ValidationResult(false, $"Value is not a string, type: {value.GetType()}");
            }

            if (!Uri.TryCreate(str, UriKind.Absolute, out _))
            {
                return new ValidationResult(false, "Provided URL is not a valid absolute URL");
            }

            return ValidationResult.ValidResult;
        }
    }

    class PathValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value is not string)
            {
                return new ValidationResult(false, $"Value is not a string, type: {value.GetType()}");
            }

            // Validate Path

            return ValidationResult.ValidResult;
        }
    }
}
