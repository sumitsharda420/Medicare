using java.io;
using org.apache.tika.extractor;
using org.apache.tika.metadata;
using org.xml.sax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medicare
{
    public class PortfolioEmbeddedDocumentExtractor : EmbeddedDocumentExtractor
    {
        private string outputDir;
        public PortfolioEmbeddedDocumentExtractor(string outputDir)
        {
            this.outputDir = outputDir;
        }

        public bool ShouldParseEmbedded(Metadata metadata)
        {
            // Only parse PDF embedded files (portfolio attachments)
            string mimetype = metadata.get("Content-Type");
            return mimetype != null && mimetype.Equals("application/pdf");
        }

        public void ParseEmbedded(InputStream stream, ContentHandler handler, Metadata metadata, bool outputHtml)
        {
            string name = metadata.get("resourceName") ?? "portfolio_file";
            string path = Path.Combine(outputDir, name);
            using (var fs = new FileStream(path, FileMode.Create))
            {
                byte[] buffer = new byte[8192];
                int read;
                while ((read = stream.read(buffer)) != -1)
                    fs.Write(buffer, 0, read);
            }
        }

        bool EmbeddedDocumentExtractor.shouldParseEmbedded(Metadata m)
        {
            throw new NotImplementedException();
        }

        void EmbeddedDocumentExtractor.parseEmbedded(InputStream @is, ContentHandler ch, Metadata m, bool b)
        {
            throw new NotImplementedException();
        }
    }
}
