using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Xml;
using System.Linq;
using System.Xml.Linq;


namespace MiniBlogFormatter
{
    public class WordpressFormatter
    {
        private Regex wordPressUploadsRegex = new Regex("(href|src)=\"(([^\"]+)(/wp-content/uploads/)[\\d]{4}/[\\d]{2}/([^\"]+))\"", RegexOptions.IgnoreCase);
        private Regex blogSpotUploadsRegex = new Regex("(href|src)=\"(([^\"]+)(blogspot)([^\"]+)/([^\"]+))\"");
        private Regex gistRegex = new Regex("\\[gist id=(\\d*)\\]");


        public void Format(string originalFolderPath, string targetFolderPath)
        {
            FormatPosts(originalFolderPath, targetFolderPath);
        }

        private void FormatPosts(string originalFolderPath, string targetFolderPath)
        {
            var oldPostList = new Dictionary<string, string>();
            foreach (string file in Directory.GetFiles(originalFolderPath, "*.xml"))
            {
                XmlDocument docOrig = LoadDocument(file);
                XmlNamespaceManager nsm = LoadNamespaceManager(docOrig);
                XmlNamespaceManager namespaceManager = new XmlNamespaceManager(docOrig.NameTable);
                namespaceManager.AddNamespace("content", docOrig.DocumentElement.GetNamespaceOfPrefix("content"));
                namespaceManager.AddNamespace("dc", docOrig.DocumentElement.GetNamespaceOfPrefix("dc"));
                namespaceManager.AddNamespace("wp", docOrig.DocumentElement.GetNamespaceOfPrefix("wp"));


                foreach (XmlNode entry in docOrig.SelectNodes("//item", nsm))
                {
                    // Only process if there is content
                    var content = entry.SelectSingleNode("content:encoded", namespaceManager).InnerText;
                    var postId = entry.SelectSingleNode("wp:post_id", namespaceManager).InnerText;
                    var parentPost = int.Parse(entry.SelectSingleNode("wp:post_parent", namespaceManager).InnerText);

                    Console.WriteLine("Processing post #{0}", postId);

                    if (string.IsNullOrEmpty(content) || parentPost != 0)
                    {
                        Console.WriteLine("Skipping post #{0} - No content", postId);
                        continue;
                    }

                    var existingUrl = entry.SelectSingleNode("link", namespaceManager).InnerText;
                    string oldUrl = null;

                    if (existingUrl != null)
                    {
                        oldUrl = existingUrl.Replace("http://www.gregpakes.co.uk", string.Empty);
                    }


                    Post post = new Post();
                    XmlNodeList categories = entry.SelectNodes("category[@domain='category']");
                    List<string> resultCategories = new List<string>();
                    foreach (XmlNode category in categories)
                    {
                        resultCategories.Add(category.InnerText);
                    }

                    post.Categories = resultCategories.ToArray();
                    post.Title = entry.SelectSingleNode("title").InnerText;
                    post.Slug = FormatterHelpers.FormatSlug(post.Title);
                    post.PubDate = DateTime.Parse(entry.SelectSingleNode("pubDate").InnerText);
                    post.LastModified = DateTime.Parse(entry.SelectSingleNode("pubDate").InnerText);

                    var formattedContent = FormatFileReferences(content, post.ID);

                    post.Images = formattedContent.ImageList;
                    post.Content = formattedContent.Content;
                    post.Author = entry.SelectSingleNode("dc:creator", namespaceManager).InnerText;
                    post.IsPublished = ReadValue(entry.SelectSingleNode("wp:status", namespaceManager), "publish") == "publish";

                    // FormatComments()
                    foreach (XmlNode comment in entry.SelectNodes("wp:comment", namespaceManager))
                    {
                        FomartComment(ref post, comment, namespaceManager);
                    }

                    string newFile = Path.Combine(targetFolderPath, postId + ".xml");
                    Storage.Save(post, newFile);

                    var uploadPath = Path.Combine(targetFolderPath, "files", post.ID);
                    Storage.SaveImages(post.Images, uploadPath);

                    if (oldUrl != null)
                    {
                        oldPostList[oldUrl] = postId;
                    }
                }
            }
            SaveOldPostMap(targetFolderPath, oldPostList);
        }

        private void SaveOldPostMap(string targetFolderPath, Dictionary<string, string> oldPostList)
        {
            var mapElement = new XElement("OldPostMap");
            foreach (var key in oldPostList.Keys)
            {
                mapElement.Add(
                    new XElement("OldPost",
                        new XAttribute("oldUrl", key),
                        new XAttribute("postId", oldPostList[key])
                    )
                );
            }
            var doc = new XDocument(mapElement);
            doc.Save(Path.Combine(targetFolderPath, "oldPosts.map"));
        }

        private void FomartComment(ref Post post, XmlNode entry, XmlNamespaceManager namespaceManager)
        {
            Comment comment = new Comment();
            comment.Author = ReadValue(entry.SelectSingleNode("wp:comment_author", namespaceManager), "n/a");
            comment.Email = ReadValue(entry.SelectSingleNode("wp:comment_author_email", namespaceManager), "");
            comment.Ip = entry.SelectSingleNode("wp:comment_author_IP", namespaceManager).InnerText;
            comment.Website = entry.SelectSingleNode("wp:comment_author_url", namespaceManager).InnerText;
            comment.Content = entry.SelectSingleNode("wp:comment_content", namespaceManager).InnerText;
            comment.PubDate = DateTime.Parse(ReadValue(entry.SelectSingleNode("wp:comment_date", namespaceManager), ""));
            comment.ID = entry.SelectSingleNode("wp:comment_id", namespaceManager).InnerText;
            comment.UserAgent = "n/a";
            post.Comments.Add(comment);
        }

        private ContentWithAttachments FormatFileReferences(string content, string id)
        {
            var imageList = new List<DownloadedImage>();
            foreach (Match match in wordPressUploadsRegex.Matches(content))
            {
                var fileName = match.Groups[5].Value;
                if (IsImageExtension(Path.GetExtension(fileName)))
                {
                    imageList.Add(new DownloadedImage(FormatterHelpers.GetImageFromUrl(match.Groups[2].Value), Uri.UnescapeDataString(fileName)));
                    content = content.Replace(match.Groups[2].Value, "/posts/files/" + id + "/" + fileName);
                }
            }

            foreach (Match match in blogSpotUploadsRegex.Matches(content))
            {
                var fileName = match.Groups[6].Value;

                if (IsImageExtension(Path.GetExtension(fileName)))
                {
                    imageList.Add(new DownloadedImage(FormatterHelpers.GetImageFromUrl(match.Groups[2].Value), Uri.UnescapeDataString(fileName)));
                    content = content.Replace(match.Groups[2].Value, "/posts/files/" + id + "/" + fileName);
                }
            }

            // Locate the Gists and replace them
            foreach (Match match in gistRegex.Matches(content))
            {
                content = content.Replace(match.Groups[0].Value, "<script src=\"https://gist.github.com/gregpakes/" + match.Groups[1].Value + ".js\"></script>");
            }

            return new ContentWithAttachments(content, imageList);
        }

        private bool IsImageExtension(string extension)
        {
            switch (extension.ToLower())
            {
                case ".jpg":
                case ".png":
                case ".gif":
                    return true;
                default:
                    return false;
            }
        }

        private static XmlNamespaceManager LoadNamespaceManager(XmlDocument docOrig)
        {
            XmlNamespaceManager nsm = new XmlNamespaceManager(docOrig.NameTable);
            nsm.AddNamespace("xsi", "http://www.w3.org/2001/XMLSchema-instance");
            nsm.AddNamespace("xsd", "http://www.w3.org/2001/XMLSchema");
            return nsm;
        }

        private static XmlDocument LoadDocument(string file)
        {
            string doc = File.ReadAllText(file).Replace(" xmlns=\"urn:newtelligence-com:dasblog:runtime:data\"", string.Empty);

            XmlDocument docOrig = new XmlDocument();
            docOrig.LoadXml(doc);
            return docOrig;
        }

        private static string ReadValue(XmlNode node, string defaultValue = "")
        {
            if (node != null)
                return node.InnerText;

            return defaultValue;
        }

        private string FormatSlug(XmlNode node)
        {
            return FormatterHelpers.FormatSlug(node.InnerText);
        }

        private IEnumerable<string> FormatCategories(XmlNode catNode)
        {
            if (catNode == null || string.IsNullOrEmpty(catNode.InnerText))
                yield break;

            string[] categories = catNode.InnerText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string category in categories)
            {
                yield return category;
            }
        }


        private class ContentWithAttachments
        {
            private readonly string _content;
            private readonly List<DownloadedImage> _imageList;

            public ContentWithAttachments(string content, List<DownloadedImage> imageList)
            {
                _content = content;
                _imageList = imageList;
            }

            public List<DownloadedImage> ImageList
            {
                get { return _imageList; }
            }

            public string Content
            {
                get { return _content; }
            }
        }
    }

    public class DownloadedImage
    {
        private readonly Image _image;
        private readonly string _fileName;

        public DownloadedImage(Image image, string fileName)
        {
            _image = image;
            _fileName = fileName;
        }

        public Image Image
        {
            get { return _image; }
        }

        public string FileName
        {
            get { return _fileName; }
        }
    }


}
