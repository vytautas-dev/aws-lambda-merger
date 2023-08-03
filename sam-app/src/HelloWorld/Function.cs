using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using System.IO;
using System;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.S3;
using Amazon.S3.Model;
using System.Net;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HelloWorld
{
  public class Function
  {
    private static string ACCESS_KEY = "Q3AM3UQ867SPQQA43P2F";
    private static string SECRET_KEY = "zuf+tfteSlswRu7BJ86wekitnifILbZam1KYY3TG";
    private static string ENDPOINT = "https://play.min.io:9000";
    private static AmazonS3Config s3Config = new AmazonS3Config
    {
        ServiceURL = ENDPOINT,
        ForcePathStyle = true
    };
    private static AmazonS3Client s3Client = new AmazonS3Client(
        ACCESS_KEY,
        SECRET_KEY,
        s3Config
    );

    public class DataToSend
        {
          public string[] args { get; set; }
        }
    private static string OUTPUT_FOLDER;
    private static string [] FILES_TO_MERGE = new string[0];
    private static string FILENAME;
    private static string BUCKET;
    public async Task<APIGatewayProxyResponse> FunctionHandler(JsonElement req, ILambdaContext context)
    {

        DataToSend data = JsonSerializer.Deserialize<DataToSend>(req.GetRawText());
              string[] args = data.args;

      foreach(string arg in args)
      {
        if (arg.Contains("--files")) {
            string filesString = arg.Split("=")[1];
            FILES_TO_MERGE = filesString.Split(",");
        }
        if (arg.Contains("--outdir")) {
            string outdir = arg.Split("=")[1];
            OUTPUT_FOLDER = outdir;
        }
        if (arg.Contains("--filename")) {
            string filename = arg.Split("=")[1];
            FILENAME = filename;
        }
        if (arg.Contains("--bucket")) {
          string bucket = arg.Split("=")[1];
          BUCKET = bucket;
        }
      }

      if (String.IsNullOrEmpty(OUTPUT_FOLDER) || FILES_TO_MERGE?.Length < 1 || String.IsNullOrEmpty(FILENAME) || String.IsNullOrEmpty(BUCKET) || args.Length != 4) {
        Console.WriteLine("Merge PDF failed. Please provide all of required arguments.");
        Environment.Exit(1);
      }

      PdfWriter writer = new PdfWriter(OUTPUT_FOLDER + "/" + FILENAME);
      writer.SetSmartMode(true);
      PdfDocument pdfDocument = new PdfDocument(writer);
      PdfMerger merger = new PdfMerger(pdfDocument);

      foreach (string item in FILES_TO_MERGE)
      {
        GetObjectRequest getRequest = new GetObjectRequest();
        getRequest.BucketName = BUCKET;
        getRequest.Key        = item;
        GetObjectResponse fileResponse = await s3Client.GetObjectAsync(getRequest);
        string fileSavePath = OUTPUT_FOLDER + item;
        await fileResponse.WriteResponseStreamToFileAsync(fileSavePath, false, default(System.Threading.CancellationToken)).ConfigureAwait(false);
        PdfDocument mergingDocument = new PdfDocument(new PdfReader(fileSavePath));
        merger.Merge(mergingDocument, 1, mergingDocument.GetNumberOfPages());
        mergingDocument.Close();
      }

      pdfDocument.Close();

      PutObjectRequest request = new PutObjectRequest();
      request.BucketName  = BUCKET;
      request.Key         = FILENAME;
      request.FilePath = OUTPUT_FOLDER + FILENAME;

      await s3Client.PutObjectAsync(request);

      var body = new Dictionary<string, string>
            {
                { "message", "Merge PDF success." },
            };

      return new APIGatewayProxyResponse
      {
        Body = JsonSerializer.Serialize(body),
        StatusCode = 200,
        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
      };
    }
  }
}