using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MiniBlogFormatter
{
    public static class FormatterHelpers
    {
        public static string FormatSlug(string slug)
        {
            string text = slug.ToLowerInvariant().Replace(" ", "-");
            return Regex.Replace(text, @"([^0-9a-z-\(\)])", string.Empty).Trim();
        }

        public static Image GetImageFromUrl(string url)
        {
            Console.WriteLine("Downloading image: {0}", url);
            var httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(url);

            using (var httpWebReponse = (HttpWebResponse)httpWebRequest.GetResponse())
            {
                using (Stream stream = httpWebReponse.GetResponseStream())
                {
                    return Image.FromStream(stream);
                }
            }

        }
    }
}
