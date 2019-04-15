using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace Raven.Documentation.Parser.Compilation.Parsing
{
    internal class TextContentParser
    {
        public Result Extract(HtmlDocument htmlDocument)
        {
            var relatedArticles =
                htmlDocument.DocumentNode.ChildNodes.FirstOrDefault(
                    x => x.InnerText.Equals("Related articles", StringComparison.OrdinalIgnoreCase));

            if (relatedArticles == null)
            {
                return new Result
                {
                    TextContent = htmlDocument.DocumentNode.InnerText
                };
            }

            var nodeToRemove = relatedArticles;
            var nodesToRemove = new List<HtmlNode>();
            while (nodeToRemove != null)
            {
                nodesToRemove.Add(nodeToRemove);
                nodeToRemove = nodeToRemove.NextSibling;
            }

            foreach (var node in nodesToRemove)
            {
                htmlDocument.DocumentNode.RemoveChild(node);
            }

            var result = new Result();

            result.RelatedArticlesHtmlContent = string.Join(Environment.NewLine, nodesToRemove.Skip(1).Select(x => x.OuterHtml));
            result.TextContent = htmlDocument.DocumentNode.InnerText;

            return result;
        }

        public class Result
        {
            public string TextContent { get; set; }
            public string RelatedArticlesHtmlContent { get; set; }
        }
    }
}
