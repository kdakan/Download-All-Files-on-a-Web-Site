# Download all file links recursively inside the site domain
Downloads all file links inside the site domain. 
Starts on a page and follows all links recursively.
Creates the same folder structure on disk.
Uses ```HtmlAgilityPack``` nuget package to parse link hrefs on the pages.

I wrote and tested this small program to download all books and documents hosted under https://doc.lagout.org

You can change the following values to download from another site domain, start from another home page, and change the root disk location for downloads:
```cs
static string baseUrl = @"https://doc.lagout.org/";
static string startingPageUrl = @"https://doc.lagout.org/";
static string baseDiskPath = @"e:\o";
```
