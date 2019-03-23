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
        static string startingPageUrl = @"https://doc.lagout.org/";
        static string baseDiskPath = @"e:\o";
        static long maxFileSize = 1024 * 1024 * 1024 / 2;//max half GB

        //static StringBuilder logs = new StringBuilder();
        //static StringBuilder errorLogs = new StringBuilder();

        static HashSet<string> visitedFileUrls = new HashSet<string>();
        static HashSet<string> visitedPageUrls = new HashSet<string>();

        static void Log(string line)
        {
            //File.AppendAllText(baseDiskPath + @"\download logs.txt", line + @"\n");
            //Console.WriteLine(line);
        }

        static void LogError(string line)
        {
            //File.AppendAllText(baseDiskPath + @"\download error logs.txt", line + @"\n");
            Console.WriteLine(line);
        }
        static void Main(string[] args)
        {
            ServicePointManager.ServerCertificateValidationCallback += ValidateRemoteCertificate;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Log("Begin downloading site. Starting at: " + startingPageUrl);
            DownloadFilesOnPage(startingPageUrl);
            Log("Site download complete. Started at: " + startingPageUrl);

            Console.ReadKey();
        }

        static void DownloadFilesOnPage(string pageUrl)
        {
            if (visitedPageUrls.Contains(pageUrl))
                return;

            visitedPageUrls.Add(pageUrl);

            var pageUrlDecoded = DecodeUrlString(pageUrl);
            if (ShouldSkipUrl(pageUrlDecoded))
                return;

            Log("Begin downloading files on page: " + pageUrlDecoded);

            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(pageUrl);
            var fileUrls = new HashSet<string>();
            var pageUrls = new HashSet<string>();

            Parallel.ForEach(doc.DocumentNode.SelectNodes("//a[@href]"), new ParallelOptions { MaxDegreeOfParallelism = 8 }, a =>
            {
                try
                {
                    if (a == null)
                    {
                        return;
                    }
                    var href = a.GetAttributeValue("href", string.Empty);
                    if (string.IsNullOrEmpty(href) ||
                            href == @"../" ||
                            href.StartsWith("mailto:") ||
                            href.StartsWith("javascript:") ||
                            href.StartsWith("#") ||
                            href.StartsWith(@"ftp://")
                            )
                        return;

                    var fullUrl = getFullUrl(pageUrl, href);
                    if (!fullUrl.StartsWith(baseUrl))
                        return;

                    var fullPath = GetFullPath(fullUrl);
                    fullPath = FixInvalidLengthAndCharactersInFullPath(fullPath);
                    if (ShouldSkipUrl(fullUrl) || File.Exists(fullPath))
                        return;

                    var contentTypeAndSize = GetContentTypeAndSize(fullUrl);
                    var contentType = contentTypeAndSize.Item1;
                    var size = contentTypeAndSize.Item2;
                    if (contentType.ToLowerInvariant().Contains("html"))
                        pageUrls.Add(fullUrl);
                    else if (size < maxFileSize)
                        fileUrls.Add(fullUrl);
                }
                catch (Exception e)
                {
                    LogError(e.Message);
                }

            });

            Parallel.ForEach(fileUrls, new ParallelOptions { MaxDegreeOfParallelism = 8 }, url =>
            {
                DownloadFile(url);
            });

            Log("Finished downloading files on page: " + pageUrlDecoded);

            foreach (var url in pageUrls)
                DownloadFilesOnPage(url);
        }

        static string getFullUrl(string pageUrl, string href)
        {
            var hrefLower = href.ToLowerInvariant();
            if (hrefLower.StartsWith(@"http://") || hrefLower.StartsWith(@"https://"))
                return href;

            return pageUrl.TrimEnd('/') + @"/" + href.TrimStart('/');
        }

        static string GetFullPath(string fullUrl)
        {
            var fullPath = baseDiskPath + @"\" + fullUrl.Replace(baseUrl, "");
            fullPath = DecodeUrlString(fullPath).Replace(@"/", @"\");
            return fullPath;
        }

        static string FixInvalidLengthAndCharactersInFullPath(string fullPath)
        {
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

            return fullPath;
        }

        static void DownloadFile(string fullUrl)
        {
            if (visitedFileUrls.Contains(fullUrl))
                return;

            visitedFileUrls.Add(fullUrl);

            var fullPath = GetFullPath(fullUrl);
            if (ShouldSkipUrl(fullPath))
                return;

            fullPath = FixInvalidLengthAndCharactersInFullPath(fullPath);

            Log(fullPath);
            var folderPath = Path.GetDirectoryName(fullPath);
            if (folderPath == fullPath.TrimEnd('\\'))
                return;

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            else if (File.Exists(fullPath))
                return;
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    webClient.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
                    webClient.DownloadFile(fullUrl, fullPath);
                }
            }
            catch (Exception e)
            {
                LogError(fullPath);
                LogError(e.Message);
                File.Delete(fullPath);
            }

        }

        static bool ShouldSkipUrl(string url)
        {
            var urlLower = url.ToLowerInvariant();
            if (
                urlLower.EndsWith(@"/distribution/") ||
                urlLower.EndsWith(@"/network/") ||
                urlLower.EndsWith(@"/s8zone_fichiers/") ||
                urlLower.EndsWith(@"/mame/") ||
                urlLower.EndsWith(@"/belgian hackers zone/") ||
                urlLower.EndsWith(@"/0_computer history/") ||
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
        //static string DownloadPage(string url)
        //{
        //    using (WebClient webClient = new WebClient())
        //    {
        //        webClient.Headers.Add("user -agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
        //        var stream = webClient.OpenRead(url);
        //        using (StreamReader sr = new StreamReader(stream))
        //        {
        //            var page = sr.ReadToEnd();
        //            return page;
        //        }
        //    }
        //}

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

        static Tuple<string, long> GetContentTypeAndSize(string url)
        {
            var contentType = @"text/html";
            long size = 0;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            try
            {
                HttpWebResponse res = (HttpWebResponse)request.GetResponse();
                using (Stream rstream = res.GetResponseStream())
                {
                    contentType = res.Headers["Content-Type"];
                    size = Convert.ToInt64(res.Headers["Content-Length"]);
                }
                res.Close();
            }
            catch (Exception ex)
            {
            }

            return new Tuple<string, long>(contentType, size);
        }

    }
}
