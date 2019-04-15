using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HtmlAgilityPack;
using MarkdownDeep;
using Raven.Documentation.Parser.Compilation.Parsing;
using Raven.Documentation.Parser.Data;
using Raven.Documentation.Parser.Helpers;

namespace Raven.Documentation.Parser.Compilation
{
    public class DocumentCompiler : DocumentCompiler<DocumentationPage>
    {
        public DocumentCompiler(Markdown parser, ParserOptions options, IProvideGitFileInformation repoAnalyzer)
            : base(parser, options, repoAnalyzer)
        {
        }

        protected override DocumentationPage CreatePage(CreatePageParams parameters)
        {
            return new DocumentationPage
            {
                Key = parameters.Key,
                Title = parameters.Title,
                Version = parameters.DocumentationVersion,
                HtmlContent = parameters.HtmlContent,
                TextContent = parameters.TextContent,
                Language = parameters.Language,
                Category = parameters.Category,
                Images = parameters.Images,
                LastCommitSha = parameters.LastCommitSha,
                RelativePath = parameters.RepositoryRelativePath,
                Mappings = parameters.Mappings,
                Metadata = parameters.Metadata,
                SeoMetaProperties = parameters.SeoMetaProperties,
                RelatedArticlesContent = parameters.RelatedArticlesContent,
                DiscussionId = parameters.DiscussionId
            };
        }
    }

    public abstract class DocumentCompiler<TPage> where TPage : DocumentationPage
    {
        private readonly Markdown _parser;
        protected readonly ParserOptions Options;
        private readonly RepositoryDataRetriever _repositoryDataRetriever;

        private readonly TextContentParser _textContentParser = new TextContentParser();

        protected DocumentCompiler(Markdown parser, ParserOptions options, IProvideGitFileInformation repoAnalyzer)
        {
            _parser = parser;
            Options = options;
            _repositoryDataRetriever = new RepositoryDataRetriever(repoAnalyzer);
        }

        protected abstract TPage CreatePage(CreatePageParams parameters);

        protected class CreatePageParams
        {
            public string Key { get; set; }
            public string Title { get; set; }
            public string DocumentationVersion { get; set; }
            public string HtmlContent { get; set; }
            public string TextContent { get; set; }
            public Language Language { get; set; }
            public Category Category { get; set; }
            public HashSet<DocumentationImage> Images { get; set; }
            public string LastCommitSha { get; set; }
            public string RepositoryRelativePath { get; set; }
            public List<DocumentationMapping> Mappings { get; set; }
            public string RelatedArticlesContent { get; set; }
            public Dictionary<string, string> Metadata { get; set; }
            public Dictionary<string, string> SeoMetaProperties { get; set; }
            public string DiscussionId { get; set; }
        }

        public TPage Compile(CompilationUtils.Parameters parameters)
        {
            var file = parameters.File;
            var page = parameters.Page;
            var documentationVersion = parameters.DocumentationVersion;
            var sourceDocumentationVersion = parameters.SourceDocumentationVersion;
            var mappings = parameters.Mappings;

            try
            {
                var key = ExtractKey(file, page, documentationVersion);
                var category = CategoryHelper.ExtractCategoryFromPath(key);
                var images = new HashSet<DocumentationImage>();

                _parser.PrepareImage = (tag, b) => PrepareImage(images, file.DirectoryName, Options.ImageUrlGenerator, documentationVersion, page.Language, tag, key);

                var content = File.ReadAllText(file.FullName);

                var builder = new DocumentBuilder(_parser, Options, sourceDocumentationVersion, content);
                builder.TransformRawHtmlBlocks();
                builder.TransformLegacyBlocks(file);
                builder.TransformBlocks();

                content = builder.Build(page);

                var htmlDocument = HtmlHelper.ParseHtml(content);

                var title = ExtractTitle(page, htmlDocument);

                ValidateTitle(title);

                var textContentResult = _textContentParser.Extract(htmlDocument);

                var repositoryData = _repositoryDataRetriever.GetForFile(file.FullName);

                var createPageParams = new CreatePageParams
                {
                    Key = key,
                    Title = title,
                    DocumentationVersion = documentationVersion,
                    HtmlContent = htmlDocument.DocumentNode.OuterHtml,
                    TextContent = textContentResult.TextContent,
                    Language = page.Language,
                    Category = category,
                    Images = images,
                    LastCommitSha = repositoryData.LastCommitSha,
                    RepositoryRelativePath = repositoryData.RepositoryRelativePath,
                    Mappings = mappings.OrderBy(x => x.Version).ToList(),
                    RelatedArticlesContent = textContentResult.RelatedArticlesHtmlContent,
                    Metadata = page.Metadata,
                    SeoMetaProperties = page.SeoMetaProperties,
                    DiscussionId = page.DiscussionId
                };

                return CreatePage(createPageParams);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(string.Format("Could not compile '{0}'.", file.FullName), e);
            }
        }

        private static bool PrepareImage(ICollection<DocumentationImage> images, string directory,
            ParserOptions.GenerateImageUrl generateImageUrl, string documentationVersion, Language lang, HtmlTag tag, string key)
        {
            string src;
            if (tag.attributes.TryGetValue("src", out src))
            {
                var imagePath = Path.Combine(directory, src);

                if (File.Exists(imagePath) == false)
                    throw new InvalidOperationException($"Could not find image in '{imagePath}' for article '{key}'.");

                src = src.Replace('\\', '/');
                if (src.StartsWith("."))
                    throw new InvalidOperationException($"Invalid image path '{src}' in article '{key}'. It cannot start from dot ('.').");

                if (src.StartsWith("images/", StringComparison.InvariantCultureIgnoreCase))
                    src = src.Substring(7);

                var fileName = Path.GetFileName(src);
                var imageUrl = generateImageUrl(documentationVersion, lang, key, fileName);

                tag.attributes["src"] = imageUrl;

                images.Add(new DocumentationImage
                {
                    ImagePath = imagePath,
                    ImageKey = $"{documentationVersion}/{src}"
                });
            }

            tag.attributes["class"] = "img-responsive img-thumbnail";

            return true;
        }

        protected virtual string ExtractKey(FileInfo file, FolderItem page, string documentationVersion)
        {
            var pathToDocumentationPagesDirectory = Options.GetPathToDocumentationPagesDirectory(documentationVersion);
            var key = file.FullName.Substring(pathToDocumentationPagesDirectory.Length, file.FullName.Length - pathToDocumentationPagesDirectory.Length);
            key = key.Substring(0, key.Length - file.Extension.Length);
            key = key.Replace(@"\", @"/");
            key = key.StartsWith(@"/") ? key.Substring(1) : key;

            var extension = FileExtensionHelper.GetLanguageFileExtension(page.Language);
            if (string.IsNullOrEmpty(extension) == false)
            {
                key = key.Substring(0, key.Length - extension.Length);
            }

            return key;
        }
        
        protected virtual string ExtractTitle(FolderItem page, HtmlDocument htmlDocument)
        {
            var node = htmlDocument.DocumentNode.ChildNodes.FirstOrDefault(x => x.Name == "h1");
            if (node == null)
                return "No title";

            return node.InnerText;
        }

        private void ValidateTitle(string title)
        {
            if (title.Contains(" :"))
                throw new InvalidOperationException("Please remove space before the colon (\" :\") in the markdown file heading.");
        }
    }
}
