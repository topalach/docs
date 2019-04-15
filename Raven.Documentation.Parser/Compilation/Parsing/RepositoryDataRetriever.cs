using Raven.Documentation.Parser.Helpers;

namespace Raven.Documentation.Parser.Compilation.Parsing
{
    internal class RepositoryDataRetriever
    {
        private readonly IProvideGitFileInformation _repoAnalyzer;

        public RepositoryDataRetriever(IProvideGitFileInformation repoAnalyzer)
        {
            _repoAnalyzer = repoAnalyzer;
        }

        public RepositoryData GetForFile(string fileFullName)
        {
            var caseSensitiveFileName = PathHelper.GetProperFilePathCapitalization(fileFullName);

            var fullName = caseSensitiveFileName ?? fileFullName;

            var repoRelativePath = _repoAnalyzer.MakeRelativePathInRepository(fullName);

            var repositoryRelativePath = repoRelativePath.Replace(@"\", @"/");

            var lastCommit = _repoAnalyzer.GetLastCommitThatAffectedFile(repoRelativePath);

            return new RepositoryData
            {
                LastCommitSha = lastCommit,
                RepositoryRelativePath = repositoryRelativePath
            };
        }

        public class RepositoryData
        {
            public string RepositoryRelativePath { get; set; }
            public string LastCommitSha { get; set; }
        }
    }
}
