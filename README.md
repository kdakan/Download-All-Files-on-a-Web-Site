# Download all file links recursively inside the site domain
Downloads all file links inside the site domain. 
Starts on a page and follows all links recursively.
Creates the same folder structure on disk.
Uses ```HtmlAgilityPack``` nuget package to parse link hrefs on the pages.

I wrote and tested this small program to download all books and documents hosted under https://doc.lagout.org

It downloads files simultaneously up to 8 jobs in parallel. 

You can change the following values to download from another site domain, start from another home page, change the root disk location for downloads, and limit the size of files to download:
```cs
static string baseUrl = @"https://doc.lagout.org/";
static string startingPageUrl = @"https://doc.lagout.org/";
static string baseDiskPath = @"e:\o";
static long maxFileSize = 1024 * 1024 * 1024 / 2;//max half GB
```

I've also placed some file extensions and folder names which I don't want to download, inside ```ShouldSkipUrl()```. You can change or remove them.
