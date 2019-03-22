using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SiteDownloader
{
    class Program
    {
        static string baseUrl = @"https://doc.lagout.org/";
        static string basePath = @"e:\o";
        static StringBuilder logs = new StringBuilder();
        static HashSet<string> visitedFileUrls = new HashSet<string>();
        static HashSet<string> visitedPageUrls = new HashSet<string>();

        static void Log(string line)
        {
            logs.AppendLine(line);
            Console.WriteLine(line);
        }

        static void Main(string[] args)
        {
            ServicePointManager.ServerCertificateValidationCallback += ValidateRemoteCertificate;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var startingPageUrl = @"https://doc.lagout.org/";

            Log("Begin downloading site. Starting at: " + startingPageUrl);
            DownloadFilesOnPage(startingPageUrl);
            Log("Site download complete.");
            File.WriteAllText(basePath + @"\download logs.txt", logs.ToString());
            Console.ReadKey();
        }

        static void DownloadFilesOnPage(string pageUrl)
        {
            if (visitedPageUrls.Contains(pageUrl))
                return;

            visitedPageUrls.Add(pageUrl);

            if (ShouldIgnoreUrl(pageUrl))
                return;
            var pageUrlDecoded = DecodeUrlString(pageUrl);
            Log("Begin downloading files on page: " + pageUrlDecoded);

            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(pageUrl);
            var fileUrls = new HashSet<string>();
            var pageUrls = new HashSet<string>();

            foreach (HtmlNode a in doc.DocumentNode.SelectNodes("//a[@href]"))
            {
                var href = a.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrEmpty(href) || href == @"../" || href.StartsWith("mailto:"))
                    continue;

                var fullUrl = getFullUrl(pageUrl, href);
                if (!fullUrl.StartsWith(baseUrl))
                    continue;

                var contentType = GetContentType(fullUrl);
                if (contentType.ToLowerInvariant().Contains("html"))
                    pageUrls.Add(fullUrl);
                else
                    fileUrls.Add(fullUrl);
            }

            foreach(var file in fileUrls)
                DownloadFile(file);

            Log("Finished downloading files on page: " + pageUrlDecoded);

            foreach (var page in pageUrls)
                DownloadFilesOnPage(page);
        }

        static string getFullUrl(string pageUrl, string href)
        {
            var hrefLower = href.ToLowerInvariant();
            if (hrefLower.StartsWith(@"http://") || hrefLower.StartsWith(@"https://"))
                return href;

            return pageUrl.TrimEnd('/') + @"/" + href.TrimStart('/');
        }

        static void DownloadFile(string fullUrl)
        {
            if (visitedFileUrls.Contains(fullUrl))
                return;

            visitedFileUrls.Add(fullUrl);

            var fullPath = basePath + @"\" + fullUrl.Replace(baseUrl, "");
            fullPath = DecodeUrlString(fullPath).Replace(@"/", @"\");
            if (ShouldIgnoreUrl(fullPath))
                return;

            var fileName = Path.GetFileName(fullPath);
            fileName = fileName.Replace(":", "");
            fileName = fileName.Replace("/w", "with");
            fileName = fileName.Replace("/", " ");
            fileName = fileName.Replace("®", "");
            fileName = fileName.Replace("?", "");
            fileName = fileName.Trim();

            fullPath = Path.GetDirectoryName(fullPath) + @"\" + fileName;
            var fileExtension = Path.GetExtension(fullPath);
            fullPath = fullPath.Substring(0, Math.Min(250, fullPath.Length - fileExtension.Length)) + fileExtension;//250 max path length

            Log(fullPath);
            var folderPath = Path.GetDirectoryName(fullPath);
            if (folderPath == fullPath.TrimEnd('\\'))
                return;

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            else if (File.Exists(fullPath))
                return;

            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
                webClient.DownloadFile(fullUrl, fullPath);
            }
        }

        static bool ShouldIgnoreUrl(string url)
        {
            //Do not download long binaries..
            var urlLower = url.ToLowerInvariant();
            if (
                urlLower.EndsWith(@"/distribution/") ||
                urlLower.EndsWith(@"/mame/") ||
                urlLower.EndsWith(@"/Belgian Hackers Zone/") || 
                urlLower.EndsWith(@".iso") ||
                urlLower.EndsWith(@".md5") ||
                urlLower.EndsWith(@".md5sum") ||
                urlLower.EndsWith(@".sig") ||
                urlLower.EndsWith(@".tar.xz") ||
                urlLower.EndsWith(@".gz") ||
                urlLower.EndsWith(@".bz2") ||
                urlLower.EndsWith(@".icm") ||
                urlLower.EndsWith(@".deb") ||
                urlLower.EndsWith(@"-checksum") ||
                urlLower.EndsWith(@".checksum")
                )
                return true;

            return false;
        }

        static string DecodeUrlString(string url)
        {
            string newUrl;
            while ((newUrl = Uri.UnescapeDataString(url)) != url)
                url = newUrl;
            return newUrl;
        }

        static bool ValidateRemoteCertificate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors error)
        {
            return true;
        }

        static string GetContentType(string url)
        {
            var contentType = @"text/html";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            try
            {
                HttpWebResponse res = (HttpWebResponse)request.GetResponse();
                using (Stream rstream = res.GetResponseStream())
                {
                    contentType = res.Headers["Content-Type"];
                }
                res.Close();
            }
            catch { }

            return contentType;
        }
        
    }
}
