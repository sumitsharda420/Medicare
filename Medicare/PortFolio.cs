using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;




namespace Medicare
{
    public static class PortFolio
    {
       
        public static void ExtractPordtfolioFiles(string pdfPath, string outputDir)
        {
            using (iText.Kernel.Pdf.PdfReader reader = new iText.Kernel.Pdf.PdfReader(pdfPath))
            using (iText.Kernel.Pdf.PdfDocument pdfDoc = new iText.Kernel.Pdf.PdfDocument(reader))
            {
                
                iText.Kernel.Pdf.PdfDictionary catalog = pdfDoc.GetCatalog().GetPdfObject();
                if (!catalog.ContainsKey(iText.Kernel.Pdf.PdfName.Collection))
                {
                    var embeddedFiles = pdfDoc.GetCatalog().GetNameTree(iText.Kernel.Pdf.PdfName.EmbeddedFiles);
                    var names = embeddedFiles.GetNames();
                    foreach (var entry in names)
                    {
                        string fileName = entry.Key.ToString();
                        iText.Kernel.Pdf.PdfDictionary fileSpec = (iText.Kernel.Pdf.PdfDictionary)entry.Value;
                        iText.Kernel.Pdf.PdfDictionary efDict = fileSpec.GetAsDictionary(iText.Kernel.Pdf.PdfName.EF);

                        if (efDict != null)
                        {
                            iText.Kernel.Pdf.PdfStream stream = efDict.GetAsStream(iText.Kernel.Pdf.PdfName.F);
                            if (stream != null)
                            {
                                byte[] bytes = stream.GetBytes();
                                string outPath = Path.Combine(outputDir, fileName);

                                File.WriteAllBytes(outPath, bytes);
                                Console.WriteLine($"Extracted: {fileName}");
                            }
                        }
                    }
                }

             
            }
        }
    }
}
