using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using System;

namespace PDFMerge
{
    public class PDFMerge
    {
        private static string OUTPUT_FOLDER;
        private static string [] FILES_TO_MERGE = new string[0];
        private static string FILENAME;
        static void Main(string[] args)
        {
            DateTime startTime = DateTime.Now;
            foreach (string item in args)
            {
                if (item.Contains("--files")) {
                    string filesString = item.Split("=")[1];
                    FILES_TO_MERGE = filesString.Split(',');
                }
                if (item.Contains("--outdir")) {
                    string outdir = item.Split("=")[1];
                    OUTPUT_FOLDER = outdir;
                }
                if (item.Contains("--filename")) {
                    string filename = item.Split("=")[1];
                    FILENAME = filename;
                }
            }
            if (String.IsNullOrEmpty(OUTPUT_FOLDER) || FILES_TO_MERGE?.Length < 1 || String.IsNullOrEmpty(FILENAME)) {
                Console.WriteLine("Merge PDF failed. Please provide all of required arguments.");
                Console.WriteLine("Example usage: --files=file1.pdf,file2.pdf --outdir=/outdir --filename=/outputFilename.pdf");
                Environment.Exit(1);
            }

            PdfWriter writer = new PdfWriter(OUTPUT_FOLDER + "/" + FILENAME);
            writer.SetSmartMode(true);
            PdfDocument pdfDocument = new PdfDocument(writer);
            PdfMerger merger = new PdfMerger(pdfDocument);

            var i = 1;
            foreach (string item in FILES_TO_MERGE)
            {
                Console.WriteLine("Merging: " + item + " (" + i + " of " + FILES_TO_MERGE.Length + ")");
                PdfDocument mergingDocument = new PdfDocument(new PdfReader(item));
                merger.Merge(mergingDocument, 1, mergingDocument.GetNumberOfPages());
                mergingDocument.Close();
                i++;
            }
            pdfDocument.Close();
            DateTime endTime = DateTime.Now;
            TimeSpan diff = endTime.Subtract(startTime);
            Console.WriteLine("Merging pdf done, created " + OUTPUT_FOLDER + "/" + FILENAME + ", time: " + diff);

        }
    }
}