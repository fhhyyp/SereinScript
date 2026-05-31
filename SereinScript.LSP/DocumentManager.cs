using System.Collections.Concurrent;

namespace SereinScript.LSP
{
    /// <summary>
    /// 文档管理器，用于维护所有打开文档的状态
    /// </summary>
    public class DocumentManager
    {
        // 存储文档内容，使用字符串路径作为键
        private readonly ConcurrentDictionary<string, string> _documents = new();

        /// <summary>
        /// 获取文档内容
        /// </summary>
        /// <param name="uri">文档 URI 字符串</param>
        /// <returns>文档内容</returns>
        public string GetDocumentContent(string uri)
        {
            if (_documents.TryGetValue(uri, out var content))
            {
                return content;
            }
            return string.Empty;
        }

        /// <summary>
        /// 更新文档内容
        /// </summary>
        /// <param name="uri">文档 URI 字符串</param>
        /// <param name="content">新的文档内容</param>
        public void UpdateDocumentContent(string uri, string content)
        {
            _documents[uri] = content;
        }

        /// <summary>
        /// 删除文档
        /// </summary>
        /// <param name="uri">文档 URI 字符串</param>
        public void RemoveDocument(string uri)
        {
            _documents.TryRemove(uri, out _);
        }

        /// <summary>
        /// 检查文档是否存在
        /// </summary>
        /// <param name="uri">文档 URI 字符串</param>
        /// <returns>是否存在</returns>
        public bool ContainsDocument(string uri)
        {
            return _documents.ContainsKey(uri);
        }
    }
}